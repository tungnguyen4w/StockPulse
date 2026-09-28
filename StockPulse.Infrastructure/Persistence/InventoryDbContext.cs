using Microsoft.EntityFrameworkCore;
using StockPulse.Domain.Entities;
using StockPulse.Domain.Enums;

namespace StockPulse.Infrastructure.Persistence;

public class InventoryDbContext(DbContextOptions<InventoryDbContext> options) : DbContext(options)
{
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<VehicleActionLog> VehicleActionLogs => Set<VehicleActionLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Vehicle>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.VIN).IsUnique();
            entity.HasIndex(e => e.IsAging);
            entity.HasIndex(e => e.Make);
            entity.HasIndex(e => e.Model);
            entity.HasIndex(e => e.Year);
            entity.HasIndex(e => e.DealershipId);
            entity.HasIndex(e => e.ArrivedAt);

            entity.Property(e => e.VIN).HasMaxLength(17).IsRequired();
            entity.Property(e => e.Make).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Model).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Colour).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Price).HasColumnType("decimal(18,2)");
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);

            entity.HasMany(e => e.ActionLogs)
                  .WithOne(e => e.Vehicle)
                  .HasForeignKey(e => e.VehicleId);
        });

        modelBuilder.Entity<VehicleActionLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.VehicleId);
            entity.Property(e => e.ActionType).HasConversion<string>().HasMaxLength(50);
        });
    }
}
