using Infrastructure.Contexts;
using Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public sealed class ExpiryBatchStockException(string message) : InvalidOperationException(message);

// Runs inside the order/return transaction, after the article's stock row has been locked.
public static class ExpiryBatchStock
{
    public static async Task AllocateAsync(DatabaseContext db, OrderEntity order)
    {
        foreach (var detail in order.OrderDetails.OrderBy(x => x.ArticleNumber, StringComparer.OrdinalIgnoreCase))
        {
            if (detail.Quentity <= 0) throw new ExpiryBatchStockException("Количество продажи должно быть больше 0.");
            var article = detail.ArticleNumber;
            var product = await db.Products.AsNoTracking().SingleAsync(x => x.ArticleNumber == article);
            detail.WarehousePlaceSnapshot = product.WarehousePlace?.Trim() ?? "";
            if (order.DirectFromStock) continue; // This order type does not consume the shop's stock.
            var batches = await db.ProductExpiryBatches.Where(x => x.ArticleNumber == article && x.Quantity > 0)
                .OrderBy(x => x.ExpiresOn).ThenBy(x => x.ManufacturedOn).ThenBy(x => x.WarehousePlace).ThenBy(x => x.Id).ToListAsync();
            if (batches.Count == 0) continue;
            // Products.Quentity is the only sale limit and was already deducted in this transaction.
            // Batch dates determine allocation order and invoice places, without restricting the sale.
            var remaining = detail.Quentity; var places = new List<string>();
            foreach (var batch in batches)
            {
                if (remaining == 0) break;
                var take = Math.Min(remaining, batch.Quantity);
                batch.Quantity -= take; batch.UpdatedAt = DateTime.UtcNow; remaining -= take;
                places.Add(batch.WarehousePlace.Trim());
                db.OrderExpiryAllocations.Add(new()
                {
                    Id = Guid.NewGuid(), OrderId = order.Id, ArticleNumber = article, BatchId = batch.Id,
                    Quantity = take, OrderDetail = detail
                });
            }
            if (remaining > 0) places.Add(product.WarehousePlace?.Trim() ?? "");
            detail.WarehousePlaceSnapshot = string.Join(", ", places.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase));
        }
    }

    public static async Task RestoreReturnAsync(DatabaseContext db, ReturnEntity document)
    {
        foreach (var detail in document.ReturnDetails.OrderBy(x => x.ArticleNumber, StringComparer.OrdinalIgnoreCase))
        {
            if (detail.Quantity <= 0) throw new ExpiryBatchStockException("Количество возврата должно быть больше 0.");
            var orderId = detail.SourceOrderId ?? document.InvoiceNumber;
            if (document.IsManual || string.IsNullOrWhiteSpace(orderId)) continue;
            detail.SourceOrderId = orderId;
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Products SET Quentity = Quentity WHERE ArticleNumber = {detail.ArticleNumber}");
            var orderDetail = await db.OrderDetails.Include(x => x.Order)
                .SingleOrDefaultAsync(x => x.OrderId == orderId && x.ArticleNumber == detail.ArticleNumber)
                ?? throw new ExpiryBatchStockException("Строка исходной накладной не найдена.");
            if (orderDetail.Order.CustomerId != document.CustomerId ||
                detail.Quantity > orderDetail.Quentity - (orderDetail.ReturnedQty ?? 0))
                throw new ExpiryBatchStockException("Количество возврата превышает остаток по исходной накладной.");
            var allocations = await db.OrderExpiryAllocations.Include(x => x.Batch)
                .Where(x => x.OrderId == orderId && x.ArticleNumber == detail.ArticleNumber)
                .OrderBy(x => x.Batch.ExpiresOn).ThenBy(x => x.Batch.ManufacturedOn).ThenBy(x => x.Batch.WarehousePlace).ThenBy(x => x.BatchId).ToListAsync();
            // New invoices have a place snapshot even when the entire line came from ordinary stock.
            // Older invoices without a snapshot or batch allocations retain their existing stock policy.
            if (!orderDetail.Order.DirectFromStock && (allocations.Count > 0 || orderDetail.WarehousePlaceSnapshot != null))
            {
                var ordinarySold = orderDetail.Quentity - allocations.Sum(x => x.Quantity);
                var ordinaryReturned = (orderDetail.ReturnedQty ?? 0) - allocations.Sum(x => x.ReturnedQuantity);
                var remaining = detail.Quantity;
                foreach (var allocation in allocations)
                {
                    var restore = Math.Min(remaining, allocation.Quantity - allocation.ReturnedQuantity);
                    if (restore <= 0) continue;
                    allocation.Batch.Quantity = checked(allocation.Batch.Quantity + restore);
                    allocation.Batch.UpdatedAt = DateTime.UtcNow; allocation.ReturnedQuantity += restore; remaining -= restore;
                    db.ReturnExpiryAllocations.Add(new()
                    {
                        Id = Guid.NewGuid(), ReturnDetail = detail, SaleAllocation = allocation, Quantity = restore
                    });
                }
                if (remaining > ordinarySold - ordinaryReturned)
                    throw new ExpiryBatchStockException("Количество возврата превышает остаток по исходной накладной.");
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Products SET Quentity = Quentity + {detail.Quantity} WHERE ArticleNumber = {detail.ArticleNumber}");
            }
            orderDetail.ReturnedQty = (orderDetail.ReturnedQty ?? 0) + detail.Quantity;
        }
    }

    public static async Task UndoReturnAsync(DatabaseContext db, ReturnEntity document)
    {
        foreach (var detail in document.ReturnDetails.OrderBy(x => x.ArticleNumber, StringComparer.OrdinalIgnoreCase))
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE Products SET Quentity = Quentity WHERE ArticleNumber = {detail.ArticleNumber}");
            var orderId = detail.SourceOrderId ?? document.InvoiceNumber;
            if (document.IsManual || string.IsNullOrWhiteSpace(orderId)) continue;
            var line = await db.OrderDetails.Include(x => x.Order)
                .SingleOrDefaultAsync(x => x.OrderId == orderId && x.ArticleNumber == detail.ArticleNumber);
            if (line == null) continue;
            var restored = await db.ReturnExpiryAllocations.Include(x => x.SaleAllocation).ThenInclude(x => x.Batch)
                .Where(x => x.ReturnDetailId == detail.Id).ToListAsync();
            var hasBatchAllocations = restored.Count > 0 || await db.OrderExpiryAllocations.AnyAsync(x => x.OrderId == orderId && x.ArticleNumber == detail.ArticleNumber);
            if (!line.Order.DirectFromStock && (line.WarehousePlaceSnapshot != null || hasBatchAllocations))
            {
                var ordinaryQuantity = detail.Quantity - restored.Sum(x => x.Quantity);
                var stockQuantity = await db.Products.Where(x => x.ArticleNumber == detail.ArticleNumber).Select(x => x.Quentity).SingleAsync();
                var articleBatches = await db.ProductExpiryBatches.Where(x => x.ArticleNumber == detail.ArticleNumber).ToListAsync();
                var batchQuantity = articleBatches.Sum(x => (long)x.Quantity);
                if (ordinaryQuantity > Math.Max(0, stockQuantity - batchQuantity))
                    throw new ExpiryBatchStockException("Возвращённый товар уже списан. Удаление возврата невозможно.");
                var affected = await db.Products.Where(x => x.ArticleNumber == detail.ArticleNumber && x.Quentity >= detail.Quantity)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.Quentity, x => x.Quentity - detail.Quantity));
                if (affected != 1 || restored.Any(x => x.SaleAllocation.Batch.Quantity < x.Quantity))
                    throw new ExpiryBatchStockException("Возвращённый товар уже списан. Удаление возврата невозможно.");
                foreach (var row in restored)
                {
                    row.SaleAllocation.Batch.Quantity -= row.Quantity; row.SaleAllocation.Batch.UpdatedAt = DateTime.UtcNow;
                    row.SaleAllocation.ReturnedQuantity -= row.Quantity;
                }
                db.ReturnExpiryAllocations.RemoveRange(restored);
            }
            line.ReturnedQty = Math.Max(0, (line.ReturnedQty ?? 0) - detail.Quantity);
        }
    }
}
