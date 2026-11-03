using Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Contexts;

public partial class DatabaseContext
{
    public DbSet<ProductRequestEntity> ProductRequests { get; set; }

    internal static void ConfigureProductRequests(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProductRequestEntity>(b =>
        {
            b.ToTable("ProductRequests", t =>
            {
                t.HasCheckConstraint("CK_ProductRequests_Quantity", "[Quantity] > 0");
                t.HasCheckConstraint("CK_ProductRequests_Kind", "[Kind] IN (1, 2, 3)");
            });
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.UserName).HasMaxLength(300);
            b.Property(x => x.CustomerId).HasMaxLength(450);
            b.Property(x => x.CustomerName).HasMaxLength(500);
            b.Property(x => x.ArticleNumber).HasMaxLength(50).IsUnicode(false);
            b.Property(x => x.ProductName).HasMaxLength(100);
            b.Property(x => x.BrandName).HasMaxLength(100);
            b.Property(x => x.Marka).HasMaxLength(300);
            b.Property(x => x.Model).HasMaxLength(500);
            b.Property(x => x.GroupName).HasMaxLength(100);
            b.Property(x => x.Alternative).HasMaxLength(300);
            b.Property(x => x.Notes).HasMaxLength(2000);
            b.HasIndex(x => new { x.Kind, x.IsSubmitted, x.CreatedAt });
            b.HasIndex(x => new { x.UserId, x.IsSubmitted });
            b.HasIndex(x => x.ArticleNumber);
        });

        // SQL Server cannot use OUTPUT without INTO on a table with an enabled trigger.
        modelBuilder.Entity<ProductEntity>().ToTable("Products", t =>
        {
            t.HasTrigger("TR_Products_RemoveNewArticleRequests");
            t.UseSqlOutputClause(false);
        });
    }
}
