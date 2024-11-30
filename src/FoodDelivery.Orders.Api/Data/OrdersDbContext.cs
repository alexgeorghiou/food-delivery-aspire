using FoodDelivery.Domain.Events;
using FoodDelivery.Domain.Orders;
using FoodDelivery.Messaging;
using Microsoft.EntityFrameworkCore;

namespace FoodDelivery.Orders.Api.Data;

public sealed class OrdersDbContext(DbContextOptions<OrdersDbContext> options, TimeProvider clock) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();

    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();

    /// <summary>
    /// Moves events raised by tracked orders into the outbox so they commit atomically with the
    /// state change. This is what makes "status changed but event lost" impossible.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var raised = ChangeTracker.Entries<Order>()
            .SelectMany(e => e.Entity.DequeueEvents())
            .ToArray();

        foreach (var integrationEvent in raised)
        {
            Outbox.Add(new OutboxMessage
            {
                Id = Guid.NewGuid(),
                OrderId = integrationEvent.OrderId,
                RoutingKey = EventRouting.RoutingKeyFor(integrationEvent),
                Payload = EventSerializer.SerializeToString(integrationEvent),
                OccurredAt = clock.GetUtcNow(),
            });
        }

        return await base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(e =>
        {
            e.ToTable("orders");
            e.HasKey(o => o.Id);
            e.Property(o => o.RestaurantName).HasMaxLength(120);
            e.Property(o => o.CustomerName).HasMaxLength(120);
            e.Property(o => o.DeliveryAddress).HasMaxLength(250);
            e.Property(o => o.CourierName).HasMaxLength(120);
            e.Property(o => o.Status).HasConversion<string>().HasMaxLength(32);
            e.Property(o => o.Subtotal).HasPrecision(10, 2);
            e.Property(o => o.DeliveryFee).HasPrecision(10, 2);
            e.Property(o => o.SmallOrderSurcharge).HasPrecision(10, 2);
            e.Property(o => o.ServiceFee).HasPrecision(10, 2);
            e.Property(o => o.Total).HasPrecision(10, 2);
            e.Property(o => o.Version).IsConcurrencyToken();
            e.Ignore(o => o.PickupLocation);
            e.Ignore(o => o.DeliveryLocation);
            e.HasIndex(o => new { o.RestaurantId, o.Status });
            e.HasIndex(o => o.PlacedAt);

            e.HasMany(o => o.Lines).WithOne().HasForeignKey("OrderId").OnDelete(DeleteBehavior.Cascade);
            e.Navigation(o => o.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);

            e.HasMany(o => o.History).WithOne().HasForeignKey("OrderId").OnDelete(DeleteBehavior.Cascade);
            e.Navigation(o => o.History).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<OrderLine>(e =>
        {
            e.ToTable("order_lines");
            e.HasKey(l => l.Id);
            e.Property(l => l.Name).HasMaxLength(120);
            e.Property(l => l.UnitPrice).HasPrecision(10, 2);
            e.Ignore(l => l.LineTotal);
        });

        modelBuilder.Entity<OrderStatusChange>(e =>
        {
            e.ToTable("order_status_changes");
            e.HasKey(h => h.Id);
            e.Property(h => h.Status).HasConversion<string>().HasMaxLength(32);
            e.Property(h => h.Actor).HasConversion<string>().HasMaxLength(32);
            e.Property(h => h.Note).HasMaxLength(500);
        });

        modelBuilder.Entity<OutboxMessage>(e =>
        {
            e.ToTable("outbox_messages");
            e.HasKey(m => m.Id);
            e.Property(m => m.RoutingKey).HasMaxLength(64);
            e.Property(m => m.Payload).HasColumnType("jsonb");
            e.Property(m => m.LastError).HasMaxLength(1000);
            e.HasIndex(m => m.PublishedAt).HasFilter("\"PublishedAt\" IS NULL");
        });

        modelBuilder.Entity<ProcessedMessage>(e =>
        {
            e.ToTable("processed_messages");
            e.HasKey(m => m.MessageKey);
            e.Property(m => m.MessageKey).HasMaxLength(128);
        });
    }
}
