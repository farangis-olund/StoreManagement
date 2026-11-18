using Infrastructure.Constants;
using Infrastructure.Contexts;
using Infrastructure.Dtos;
using Infrastructure.Security;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;

namespace Infrastructure.Services;

public class WarehousePlaceService(IDbContextFactory<DatabaseContext> factory, IPermissionChecker permissions)
{
    public virtual async Task<IReadOnlyList<string>> GetArticlesAsync(CancellationToken ct = default)
    {
        EnsureAccess();
        await using var db = await factory.CreateDbContextAsync(ct);
        var articles = await db.Products.AsNoTracking().OrderBy(p => p.Numbering).ThenBy(p => p.ArticleNumber)
            .Select(p => p.ArticleNumber).ToArrayAsync(ct);
        EnsureAccess();
        return articles;
    }

    public virtual async Task<Product?> GetProductAsync(string article, CancellationToken ct = default)
    {
        EnsureAccess();
        await using var db = await factory.CreateDbContextAsync(ct);
        var product = await db.Products.AsNoTracking().Include(p => p.Brand).Include(p => p.Group)
            .SingleOrDefaultAsync(p => p.ArticleNumber == article, ct);
        EnsureAccess();
        return product == null ? null : (Product)product;
    }

    public virtual async Task<int> UpdatePlaceAsync(string article, string place, CancellationToken ct = default)
    {
        EnsureAccess();
        Validate(new(1, article, place));
        await using var db = await factory.CreateDbContextAsync(ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        EnsureAccess();
        var updated = await db.Products.Where(p => p.ArticleNumber == article.Trim())
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.WarehousePlace, place.Trim()), ct);
        EnsureAccess();
        await transaction.CommitAsync(ct);
        return updated;
    }

    public virtual async Task<WarehousePlaceImportResult> ImportAsync(IReadOnlyList<WarehousePlaceImportRow> rows,
        CancellationToken ct = default)
    {
        EnsureAccess();
        if (rows.Count == 0) return new(0, 0, []);
        if (rows.Select(r => r.RowNumber).Distinct().Count() != rows.Count)
            throw new ArgumentException("Повторяются номера строк Excel.");
        using var data = new DataTable();
        data.Columns.Add("RowNumber", typeof(int));
        data.Columns.Add("ArticleNumber", typeof(string));
        data.Columns.Add("WarehousePlace", typeof(string));
        var originals = new Dictionary<int, WarehousePlaceImportRow>();
        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();
            Validate(row);
            var trimmed = row with { ArticleNumber = row.ArticleNumber.Trim(), WarehousePlace = row.WarehousePlace.Trim() };
            originals.Add(row.RowNumber, trimmed);
            data.Rows.Add(trimmed.RowNumber, trimmed.ArticleNumber, trimmed.WarehousePlace);
        }
        await using var db = await factory.CreateDbContextAsync(ct);
        EnsureAccess();
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var connection = (SqlConnection)db.Database.GetDbConnection();
        var sqlTransaction = (SqlTransaction)transaction.GetDbTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = sqlTransaction;
        command.CommandTimeout = 120;
        // Unicode staging preserves unknown articles exactly, including characters outside the DB code page.
        command.CommandText = """
            CREATE TABLE #WarehousePlaceInput (
                RowNumber int NOT NULL,
                ArticleNumber nvarchar(50) COLLATE DATABASE_DEFAULT NOT NULL,
                WarehousePlace nvarchar(max) NOT NULL);
            """;
        await command.ExecuteNonQueryAsync(ct);
        using (var bulk = new SqlBulkCopy(connection, SqlBulkCopyOptions.Default, sqlTransaction))
        {
            bulk.DestinationTableName = "#WarehousePlaceInput";
            bulk.BatchSize = 5000;
            bulk.BulkCopyTimeout = 120;
            foreach (DataColumn column in data.Columns) bulk.ColumnMappings.Add(column.ColumnName, column.ColumnName);
            await bulk.WriteToServerAsync(data, ct);
        }
        EnsureAccess();
        // Deduplicate using the database collation. The last Excel row wins; update only WarehousePlace.
        command.CommandText = """
            SET NOCOUNT ON;
            SELECT RowNumber, ArticleNumber, WarehousePlace INTO #WarehousePlaceEffective
            FROM (
                SELECT *, ROW_NUMBER() OVER (PARTITION BY ArticleNumber ORDER BY RowNumber DESC) AS Position
                FROM #WarehousePlaceInput
            ) AS Ranked WHERE Position = 1;

            SELECT i.RowNumber, p.ArticleNumber AS MatchedArticle, i.WarehousePlace
            INTO #WarehousePlaceMatched
            FROM #WarehousePlaceEffective AS i
            LEFT JOIN dbo.Products AS p WITH (UPDLOCK, HOLDLOCK) ON p.ArticleNumber = i.ArticleNumber;

            UPDATE p SET WarehousePlace = m.WarehousePlace
            FROM dbo.Products AS p JOIN #WarehousePlaceMatched AS m ON p.ArticleNumber = m.MatchedArticle;

            SELECT COUNT(*) AS MatchedCount FROM #WarehousePlaceMatched WHERE MatchedArticle IS NOT NULL;
            SELECT RowNumber FROM #WarehousePlaceMatched WHERE MatchedArticle IS NULL ORDER BY RowNumber;
            SELECT (SELECT COUNT(*) FROM #WarehousePlaceInput) - COUNT(*) AS DuplicateCount FROM #WarehousePlaceEffective;

            SELECT p.ArticleNumber, p.ProductName, g.GroupName, b.BrandName, p.Marka, p.Model, p.WarehousePlace
            FROM dbo.Products AS p
            JOIN #WarehousePlaceMatched AS m ON p.ArticleNumber = m.MatchedArticle
            LEFT JOIN dbo.Groups AS g ON g.Id = p.GroupId
            LEFT JOIN dbo.Brands AS b ON b.Id = p.BrandId
            ORDER BY m.RowNumber;
            """;
        int updated, duplicates;
        var missing = new List<WarehousePlaceImportIssue>();
        var products = new List<Product>();
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            await reader.ReadAsync(ct);
            updated = reader.GetInt32(0);
            await reader.NextResultAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var row = originals[reader.GetInt32(0)];
                missing.Add(new(row.RowNumber, row.ArticleNumber, row.WarehousePlace,
                    "Артикул не найден в базе.", WarehousePlaceImportIssueKind.ArticleNotFound));
            }
            await reader.NextResultAsync(ct);
            await reader.ReadAsync(ct);
            duplicates = reader.GetInt32(0);
            await reader.NextResultAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                products.Add(new Product
                {
                    ArticleNumber = reader.GetString(0), ProductName = reader.GetString(1),
                    GroupName = reader.IsDBNull(2) ? "" : reader.GetString(2),
                    BrandName = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    Marka = reader.GetString(4), Model = reader.GetString(5), WarehousePlace = reader.GetString(6)
                });
            }
        }
        EnsureAccess();
        await transaction.CommitAsync(ct);
        return new(updated, duplicates, missing) { UpdatedProducts = products };
    }

    private static void Validate(WarehousePlaceImportRow row)
    {
        if (row.RowNumber <= 0 || string.IsNullOrWhiteSpace(row.ArticleNumber) || row.ArticleNumber.Trim().Length > 50 ||
            string.IsNullOrWhiteSpace(row.WarehousePlace))
            throw new ArgumentException($"Некорректные данные в строке {row.RowNumber}.");
    }

    private void EnsureAccess()
    {
        if (!permissions.Has(PermissionCatalog.Inventory))
            throw new UnauthorizedAccessException("Нет права доступа к инвентарю.");
    }
}
