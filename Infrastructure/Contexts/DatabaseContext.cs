
using Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Contexts;

public partial class DatabaseContext(DbContextOptions<DatabaseContext> options) : DbContext(options)
{
	public virtual DbSet<BrandEntity> Brands { get; set; }
    public virtual DbSet<CurrencyEntity> Currencies { get; set; }
    public virtual DbSet<GroupEntity> Groups { get; set; }
    public virtual DbSet<ProductEntity> Products { get; set; }
	public virtual DbSet<CustomerEntity> Customers { get; set; }
	public virtual DbSet<UserEntity> Users { get; set; }
	public virtual DbSet<RoleEntity> Roles { get; set; }
	public virtual DbSet<UserRoleEntity> UserRoles { get; set; }
	public virtual DbSet<OrderDetailEntity> OrderDetails { get; set; }
	public virtual DbSet<OrderEntity> Orders { get; set; }

	public virtual DbSet<CustomerPaymentEntity> Payments { get; set; }

	public virtual DbSet<ExchangeRateEntity> ExchangeRates { get; set; }

	public virtual DbSet<PriceLevelEntity> PriceLevels { get; set; }
	public virtual DbSet<ManagerBrandEntity> ManagerBrands { get; set; }
	public virtual DbSet<SalesManagerEntity> SalesManagers { get; set; }
	public virtual DbSet<CourierEntity> Couriers { get; set; }
	public virtual DbSet<StorekeeperEntity> Storekeepers { get; set; }

	public virtual DbSet<ReturnEntity> Returns { get; set; }

	public virtual DbSet<ReturnDetailEntity> ReturnDetails { get; set; }


	protected override void OnModelCreating(ModelBuilder modelBuilder)
    {

		foreach (var property in modelBuilder.Model
		.GetEntityTypes()
		.SelectMany(t => t.GetProperties())
		.Where(p => p.ClrType == typeof(decimal)))
		{
			property.SetColumnType("decimal(18,2)");
		}


		modelBuilder.Entity<CustomerPaymentEntity>(b =>
		{
			b.ToTable("Payments"); // or your actual table name
			b.HasKey(x => x.Id);
			b.Property(x => x.Amount).HasColumnType("decimal(18,2)");

			b.HasOne(x => x.Customer)
			 .WithMany(c => c.Payments)
			 .HasForeignKey(x => x.CustomerId)
			 .HasPrincipalKey(c => c.Id)
			 .OnDelete(DeleteBehavior.Restrict);
						
		});
			

		modelBuilder.Entity<CustomerEntity>()
		.HasOne(c => c.SalesManager)
		.WithMany(m => m.Customers)
		.HasForeignKey(c => c.SalesManagerId)
		.OnDelete(DeleteBehavior.Restrict); // or SetNull

		modelBuilder.Entity<CustomerEntity>()
		.HasMany(c => c.Returns)
		.WithOne(r => r.Customer)
		.HasForeignKey(r => r.CustomerId);

		modelBuilder.Entity<ManagerBrandEntity>()
	.HasKey(mb => new { mb.ManagerId, mb.Brand });

		modelBuilder.Entity<ManagerBrandEntity>()
			.HasOne(mb => mb.Manager)
			.WithMany(m => m.ManagerBrands)
			.HasForeignKey(mb => mb.ManagerId);
	

		modelBuilder.Entity<BrandEntity>()
            .HasIndex(x => x.BrandName)
            .IsUnique();

        modelBuilder.Entity<GroupEntity>()
            .HasIndex(x => x.GroupName)
            .IsUnique();

        modelBuilder.Entity<CurrencyEntity>()
            .HasIndex(x => x.Code)
            .IsUnique();
		        

        modelBuilder.Entity<ProductEntity>(entity =>
        {
            entity.HasOne(c => c.Brand)
                .WithMany(z => z.Products)
                .HasForeignKey(d => d.BrandId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(c => c.Group)
                .WithMany(z => z.Products)
                .HasForeignKey(d => d.GroupId)
                .OnDelete(DeleteBehavior.Cascade);
        });

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


		modelBuilder.Entity<OrderDetailEntity>()
	.HasKey(od => new { od.OrderId, od.ArticleNumber }); // Composite PK

		modelBuilder.Entity<OrderDetailEntity>()
			.Property(od => od.ArticleNumber)
			.IsRequired()
			.HasColumnType("varchar(50)");

		modelBuilder.Entity<OrderDetailEntity>()
			.Property(od => od.Price)
			.HasPrecision(18, 2);

		modelBuilder.Entity<OrderDetailEntity>()
			.HasOne(od => od.Order)
			.WithMany(o => o.OrderDetails)
			.HasForeignKey(od => od.OrderId);

		modelBuilder.Entity<OrderDetailEntity>()
			.HasOne(od => od.Product)
			.WithMany(p => p.OrderDetails)
			.HasForeignKey(od => od.ArticleNumber);


		modelBuilder.Entity<UserRoleEntity>()
			.HasKey(ur => new { ur.UserId, ur.RoleId });

		
		modelBuilder.Entity<UserRoleEntity>()
			.HasOne(ur => ur.User)
			.WithMany(u => u.UserRoles)
			.HasForeignKey(ur => ur.UserId);

		
		modelBuilder.Entity<UserRoleEntity>()
			.HasOne(ur => ur.Role)
			.WithMany(r => r.UserRoles)
			.HasForeignKey(ur => ur.RoleId);

		// ==== Returns ====
		modelBuilder.Entity<ReturnEntity>(entity =>
		{
			entity.HasOne(e => e.Customer)
				  .WithMany(c => c.Returns)   
				  .HasForeignKey(e => e.CustomerId)
				  .OnDelete(DeleteBehavior.Restrict);

			entity.HasMany(e => e.ReturnDetails)
				  .WithOne(d => d.Return)
				  .HasForeignKey(d => d.ReturnId)
				  .OnDelete(DeleteBehavior.Cascade);
		});

		modelBuilder.Entity<ReturnDetailEntity>(entity =>
		{
			entity.HasOne(d => d.Return)
				  .WithMany(r => r.ReturnDetails)
				  .HasForeignKey(d => d.ReturnId);

			entity.HasOne(d => d.Product)
				  .WithMany() // if ProductEntity does not expose ReturnDetails
				  .HasForeignKey(d => d.ArticleNumber)
				  .HasPrincipalKey(p => p.ArticleNumber);
		});
	}
}
