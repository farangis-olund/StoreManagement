using Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Contexts;

public partial class DatabaseContext
{
    public DbSet<ProductExpiryBatchEntity> ProductExpiryBatches => Set<ProductExpiryBatchEntity>();
    public DbSet<OrderExpiryAllocationEntity> OrderExpiryAllocations => Set<OrderExpiryAllocationEntity>();
    public DbSet<ReturnExpiryAllocationEntity> ReturnExpiryAllocations => Set<ReturnExpiryAllocationEntity>();

    private static void ConfigureProductExpiry(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProductExpiryBatchEntity>(b =>
        {
            b.ToTable("ProductExpiryBatches", t =>
            {
                t.HasCheckConstraint("CK_ProductExpiryBatches_Dates", "[ManufacturedOn] <= [ExpiresOn]");
                t.HasCheckConstraint("CK_ProductExpiryBatches_Quantity", "[Quantity] >= 0");
            });
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.ArticleNumber).HasMaxLength(50).IsUnicode(false);
            b.Property(x => x.ManufacturedOn).HasColumnType("date");
            b.Property(x => x.ExpiresOn).HasColumnType("date");
            b.Property(x => x.WarehousePlace).HasMaxLength(200);
            b.Property(x => x.Quantity).HasDefaultValue(0);
            b.Property(x => x.Version).IsRowVersion();
            b.HasIndex(x => new { x.ArticleNumber, x.ManufacturedOn, x.ExpiresOn, x.WarehousePlace }).IsUnique();
            b.HasIndex(x => new { x.Quantity, x.ExpiresOn });
            b.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ArticleNumber).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<OrderDetailEntity>().Property(x => x.WarehousePlaceSnapshot).HasColumnType("nvarchar(max)");
        modelBuilder.Entity<ReturnDetailEntity>().Property(x => x.SourceOrderId).HasMaxLength(450);
        modelBuilder.Entity<OrderExpiryAllocationEntity>(b =>
        {
            b.ToTable("OrderExpiryAllocations", t => t.HasCheckConstraint("CK_OrderExpiryAllocations_Quantity",
                "[Quantity] > 0 AND [ReturnedQuantity] >= 0 AND [ReturnedQuantity] <= [Quantity]"));
            b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
            b.HasIndex(x => new { x.OrderId, x.ArticleNumber, x.BatchId }).IsUnique();
            b.HasOne(x => x.OrderDetail).WithMany().HasForeignKey(x => new { x.OrderId, x.ArticleNumber }).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Batch).WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<ReturnExpiryAllocationEntity>(b =>
        {
            b.ToTable("ReturnExpiryAllocations", t => t.HasCheckConstraint("CK_ReturnExpiryAllocations_Quantity", "[Quantity] > 0"));
            b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever();
            b.HasOne(x => x.ReturnDetail).WithMany().HasForeignKey(x => x.ReturnDetailId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.SaleAllocation).WithMany().HasForeignKey(x => x.SaleAllocationId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
