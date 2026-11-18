using FlashSale.Api.Database;
using Microsoft.EntityFrameworkCore;

namespace FlashSale.Api;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Event> Events => Set<Event>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // 1. EVENT TABLOSU VE TARİH İNDEKSİ
        modelBuilder.Entity<Event>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Price).HasPrecision(18, 2);

            // PostgreSQL xmin sistem sütununu Concurrency Token olarak bağlar
            entity.Property(e => e.Version).IsRowVersion();

            // Flash-Sale listelemesinde tüm tabloyu taramamak (Seq Scan önleme) için B-Tree indeks
            entity.HasIndex(e => e.StartsAtUtc)
                  .HasDatabaseName("IX_Events_StartsAtUtc");
        });

        // 2. USER TABLOSU VE UNIQUE EMAIL İNDEKSİ
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.Property(u => u.Email).IsRequired().HasMaxLength(150);
            entity.Property(u => u.Role).IsRequired().HasMaxLength(30);

            // Login anında milisaniyelik arama ve mükerrer kayıt engeli
            entity.HasIndex(u => u.Email)
                  .IsUnique()
                  .HasDatabaseName("IX_Users_Email_Unique");
        });

        // 3. ORDER TABLOSU VE BİLEŞİK (COMPOSITE) İNDEKS
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(o => o.Id);
            entity.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);

            // KRİTİK KİLİT: Aynı kullanıcının aynı etkinlikten 2. kez bilet almasını DB seviyesinde engeller
            entity.HasIndex(o => new { o.EventId, o.UserId })
                  .IsUnique()
                  .HasDatabaseName("IX_Orders_EventId_UserId_Unique");

            entity.HasOne<Event>()
                  .WithMany()
                  .HasForeignKey(o => o.EventId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<User>()
                  .WithMany()
                  .HasForeignKey(o => o.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
