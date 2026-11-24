using Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Contexts;

public partial class DatabaseContext
{
    public DbSet<ClearanceProductEntity> ClearanceProducts => Set<ClearanceProductEntity>();
    public DbSet<ClearanceBonusReportEntity> ClearanceBonusReports => Set<ClearanceBonusReportEntity>();
    public DbSet<ClearanceBonusLineEntity> ClearanceBonusLines => Set<ClearanceBonusLineEntity>();

    private static void ConfigureClearance(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ClearanceProductEntity>(b =>
        {
            b.ToTable("ClearanceProducts");
            b.HasKey(x => x.ArticleNumber);
            b.Property(x => x.ArticleNumber).HasMaxLength(50).IsUnicode(false);
            b.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ArticleNumber).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<ClearanceBonusReportEntity>(b =>
        {
            b.ToTable("ClearanceBonusReports", t =>
            {
                t.HasCheckConstraint("CK_ClearanceBonusReports_Period", "[FromDate] <= [ToDate]");
                t.HasCheckConstraint("CK_ClearanceBonusReports_Percent", "[Percent] > 0 AND [Percent] <= 100");
            });
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.FromDate).HasColumnType("date");
            b.Property(x => x.ToDate).HasColumnType("date");
            b.Property(x => x.Percent).HasPrecision(18, 2);
            b.Property(x => x.TotalAmount).HasPrecision(18, 2);
            b.Property(x => x.TotalBonus).HasPrecision(18, 2);
            b.HasIndex(x => new { x.FromDate, x.ToDate }).IsUnique();
        });
        modelBuilder.Entity<ClearanceBonusLineEntity>(b =>
        {
            b.ToTable("ClearanceBonusLines");
            b.HasKey(x => new { x.ReportId, x.UserId, x.ArticleNumber });
            b.Property(x => x.ArticleNumber).HasMaxLength(50).IsUnicode(false);
            b.Property(x => x.SellerName).HasMaxLength(450);
            b.Property(x => x.Amount).HasPrecision(18, 2);
            b.Property(x => x.Bonus).HasPrecision(18, 2);
            b.HasOne(x => x.Report).WithMany(x => x.Lines).HasForeignKey(x => x.ReportId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
