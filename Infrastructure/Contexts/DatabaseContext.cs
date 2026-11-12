
using Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Contexts;

public partial class DatabaseContext(DbContextOptions<DatabaseContext> options)
    : DbContext(options)
{
    public DbSet<BrandEntity> Brands { get; set; }
    public DbSet<CurrencyEntity> Currencies { get; set; }
    public DbSet<GroupEntity> Groups { get; set; }
    public DbSet<ProductEntity> Products { get; set; }
    public DbSet<CustomerEntity> Customers { get; set; }
    public DbSet<OfficialMonthlyPlanEntity> OfficialMonthlyPlans { get; set; }
    public DbSet<OfficialMonthlyPlanSettingsEntity> OfficialMonthlyPlanSettings { get; set; }

    public DbSet<UserEntity> Users { get; set; }
    public DbSet<RoleEntity> Roles { get; set; }
    public DbSet<UserRoleEntity> UserRoles { get; set; }
    public DbSet<RolePermissionEntity> RolePermissions { get; set; }
    public DbSet<PermissionEntity> Permissions { get; set; }

    public DbSet<OrderEntity> Orders { get; set; }
    public DbSet<OrderDetailEntity> OrderDetails { get; set; }
    public DbSet<CustomerPaymentEntity> Payments { get; set; }

    public DbSet<ExchangeRateEntity> ExchangeRates { get; set; }
    public DbSet<PriceLevelEntity> PriceLevels { get; set; }

    public DbSet<SalesManagerEntity> SalesManagers { get; set; }
    public DbSet<ManagerBrandEntity> ManagerBrands { get; set; }
    public DbSet<ManagerCustomerEntity> ManagerCustomers { get; set; }

    public DbSet<CourierEntity> Couriers { get; set; }
    public DbSet<StorekeeperEntity> Storekeepers { get; set; }

    public DbSet<ReturnEntity> Returns { get; set; }
    public DbSet<ReturnDetailEntity> ReturnDetails { get; set; }
    public DbSet<ReturnReasonEntity> ReturnReasons { get; set; }

    public DbSet<RaschetKoefficentaEntity> RaschetKoefficenta { get; set; }
    public DbSet<OrganizationInfoEntity> OrganizationInfo { get; set; }
    public DbSet<StockUpdateLogEntity> StockUpdateLog { get; set; }
    public DbSet<StockMovementEntity> StockMovements { get; set; }
    public DbSet<StockImportErrorEntity> StockImportErrors { get; set; }
    public DbSet<StoreEntity> Stores { get; set; }
    public DbSet<StoreExchangeEntity> StoreExchanges { get; set; }
    public DbSet<CategoryEntity> Categories { get; set; }
    public DbSet<ExpenseEntity> Expenses { get; set; }
    public DbSet<CourierPaymentEntity> CourierPayments { get; set; }
    public DbSet<StoreTransferSummaryEntity> StoreTransferSummaries { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ConfigureProductRequests(modelBuilder);
        modelBuilder.Entity<UserEntity>().Property(u => u.IsManager).HasDefaultValue(false);
        modelBuilder.Entity<SalesManagerEntity>().HasOne(m => m.User).WithOne(u => u.SalesManager)
            .HasForeignKey<SalesManagerEntity>(m => m.UserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<OfficialMonthlyPlanSettingsEntity>(entity =>
        {
            entity.ToTable("OfficialMonthlyPlanSettings", table =>
            {
                table.HasCheckConstraint("CK_OfficialMonthlyPlanSettings_Id", "[Id] = 1");
                table.HasCheckConstraint("CK_OfficialMonthlyPlanSettings_Threshold", "[NonOfficialThreshold] >= 0");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).ValueGeneratedNever();
            entity.Property(x => x.NonOfficialThreshold).HasPrecision(18, 2);
            entity.HasData(new OfficialMonthlyPlanSettingsEntity { Id = 1, NonOfficialThreshold = 0 });
        });
        modelBuilder.Entity<OfficialMonthlyPlanEntity>(entity =>
        {
            entity.ToTable("OfficialMonthlyPlans", table =>
            {
                table.HasCheckConstraint("CK_OfficialMonthlyPlans_Amount", "[Amount] > 0");
                table.HasCheckConstraint("CK_OfficialMonthlyPlans_Month", "DAY([Month]) = 1");
            });
            entity.HasKey(x => new { x.CustomerId, x.Month });
            entity.Property(x => x.CustomerId).HasMaxLength(450);
            entity.Property(x => x.Month).HasColumnType("date");
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.HasIndex(x => x.Month);
            entity.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        });

        // Global decimal precision
        foreach (var property in modelBuilder.Model
            .GetEntityTypes()
            .SelectMany(t => t.GetProperties())
            .Where(p => p.ClrType == typeof(decimal)))
        {
            property.SetColumnType("decimal(18,2)");
        }

        // =========================
        // INDEXES
        // =========================
        modelBuilder.Entity<BrandEntity>().HasIndex(x => x.BrandName).IsUnique();
        modelBuilder.Entity<GroupEntity>().HasIndex(x => x.GroupName).IsUnique();
        modelBuilder.Entity<CurrencyEntity>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<StockUpdateLogEntity>().HasIndex(x => x.UpdateDate).IsUnique();

        // =========================
        // PRODUCT
        // =========================
        modelBuilder.Entity<ProductEntity>()
            .HasOne(p => p.Brand)
            .WithMany(b => b.Products)
            .HasForeignKey(p => p.BrandId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ProductEntity>()
            .HasOne(p => p.Group)
            .WithMany(g => g.Products)
            .HasForeignKey(p => p.GroupId)
            .OnDelete(DeleteBehavior.Restrict);

        // =========================
        // ORDER
        // =========================
        modelBuilder.Entity<OrderEntity>()
            .HasOne(o => o.Courier)
            .WithMany(c => c.Orders)
            .HasForeignKey(o => o.CourierId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<OrderEntity>()
            .HasOne(o => o.Storekeeper)
            .WithMany(s => s.Orders)
            .HasForeignKey(o => o.StorekeeperId)
            .OnDelete(DeleteBehavior.SetNull);

        // Order → OrderDetails (CASCADE)
        modelBuilder.Entity<OrderDetailEntity>()
            .HasKey(od => new { od.OrderId, od.ArticleNumber });

        modelBuilder.Entity<OrderDetailEntity>()
            .HasOne(od => od.Order)
            .WithMany(o => o.OrderDetails)
            .HasForeignKey(od => od.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<OrderDetailEntity>()
            .HasOne(od => od.Product)
            .WithMany(p => p.OrderDetails)
            .HasForeignKey(od => od.ArticleNumber)
            .OnDelete(DeleteBehavior.Restrict);

        // Order → Payments (CASCADE)
        modelBuilder.Entity<CustomerPaymentEntity>(b =>
        {
            b.ToTable("Payments");
            b.HasKey(x => x.Id);
            b.Property(p => p.ReceiptNumber).HasMaxLength(9);
            b.HasIndex(p => p.ReceiptNumber).IsUnique().HasFilter("[ReceiptNumber] IS NOT NULL");

            b.HasOne(p => p.Courier).WithMany().HasForeignKey(p => p.CourierId)
                .OnDelete(DeleteBehavior.Restrict);

            b.HasOne(p => p.Order)
                .WithMany(o => o.Payments)
                .HasForeignKey(p => p.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(p => p.Customer)
                .WithMany(c => c.Payments)
                .HasForeignKey(p => p.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // =========================
        // CUSTOMER
        // =========================
        modelBuilder.Entity<CustomerEntity>()
            .HasOne(c => c.SalesManager)
            .WithMany(m => m.Customers)
            .HasForeignKey(c => c.SalesManagerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<CustomerEntity>()
            .Property(c => c.BuyLimit)
            .HasColumnType("decimal(18,2)");

        modelBuilder.Entity<CustomerEntity>()
            .Property(c => c.UnpaidPaymentLimit)
            .HasColumnType("decimal(18,2)");

        // =========================
        // RETURNS
        // =========================
        modelBuilder.Entity<ReturnEntity>(entity =>
        {
            entity.HasOne(r => r.Customer)
                .WithMany(c => c.Returns)
                .HasForeignKey(r => r.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(r => r.ReturnReason)
                .WithMany(rr => rr.Returns)
                .HasForeignKey(r => r.ReturnReasonId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(r => r.ReturnDetails)
                .WithOne(d => d.Return)
                .HasForeignKey(d => d.ReturnId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ReturnDetailEntity>()
            .HasOne(d => d.Product)
            .WithMany()
            .HasForeignKey(d => d.ArticleNumber)
            .OnDelete(DeleteBehavior.Restrict);

        // =========================
        // STORE
        // =========================
        modelBuilder.Entity<StoreEntity>()
            .HasKey(s => s.StoreCode);

        modelBuilder.Entity<StoreExchangeEntity>()
            .HasOne(se => se.Store)
            .WithMany(s => s.StoreExchanges)
            .HasForeignKey(se => se.StoreCode);

        // =========================
        // MANY-TO-MANY
        // =========================
        modelBuilder.Entity<UserRoleEntity>()
            .HasKey(x => new { x.UserId, x.RoleId });

        modelBuilder.Entity<ManagerBrandEntity>()
            .HasKey(x => new { x.ManagerId, x.BrandId });

        modelBuilder.Entity<ManagerCustomerEntity>()
            .HasKey(x => new { x.ManagerId, x.CustomerId });


        modelBuilder.Entity<CourierPaymentEntity>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasOne(x => x.CustomerPayment).WithMany().HasForeignKey(x => x.CustomerPaymentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => x.CustomerPaymentId).IsUnique()
                .HasFilter("[CustomerPaymentId] IS NOT NULL");

            entity.Property(x => x.AmountInEuro).HasColumnType("decimal(18,2)");
            entity.Property(x => x.AmountInTJS).HasColumnType("decimal(18,2)");

            entity.HasOne(x => x.Order)
                .WithMany()
                .HasForeignKey(x => x.OrderId)
                .HasPrincipalKey(o => o.Id)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(x => x.Courier)
                .WithMany()
                .HasForeignKey(x => x.CourierId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
