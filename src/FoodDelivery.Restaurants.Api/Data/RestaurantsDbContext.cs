using Microsoft.EntityFrameworkCore;

namespace FoodDelivery.Restaurants.Api.Data;

public sealed class RestaurantsDbContext(DbContextOptions<RestaurantsDbContext> options) : DbContext(options)
{
    public DbSet<Restaurant> Restaurants => Set<Restaurant>();

    public DbSet<MenuItem> MenuItems => Set<MenuItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Restaurant>(e =>
        {
            e.ToTable("restaurants");
            e.HasKey(r => r.Id);
            e.Property(r => r.Name).HasMaxLength(120).IsRequired();
            e.Property(r => r.Cuisine).HasMaxLength(60).IsRequired();
            e.Property(r => r.Address).HasMaxLength(250).IsRequired();
            e.Property(r => r.Description).HasMaxLength(1000);
            e.Property(r => r.TimeZoneId).HasMaxLength(64);
            e.Property(r => r.Currency).HasMaxLength(3).IsFixedLength();
            e.Ignore(r => r.Location);
            e.HasIndex(r => r.Cuisine);

            e.HasMany(r => r.MenuItems).WithOne().HasForeignKey(m => m.RestaurantId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(r => r.OpeningPeriods).WithOne().HasForeignKey(p => p.RestaurantId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MenuItem>(e =>
        {
            e.ToTable("menu_items");
            e.HasKey(m => m.Id);
            e.Property(m => m.Name).HasMaxLength(120).IsRequired();
            e.Property(m => m.Description).HasMaxLength(500);
            e.Property(m => m.Category).HasMaxLength(60).IsRequired();
            e.Property(m => m.Price).HasPrecision(10, 2);
            e.HasIndex(m => new { m.RestaurantId, m.Category, m.SortOrder });
        });

        modelBuilder.Entity<OpeningPeriodRow>(e =>
        {
            e.ToTable("opening_periods");
            e.HasKey(p => p.Id);
            e.HasIndex(p => new { p.RestaurantId, p.Day });
        });
    }
}
