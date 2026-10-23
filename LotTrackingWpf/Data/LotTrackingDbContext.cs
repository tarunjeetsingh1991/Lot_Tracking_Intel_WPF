
using Microsoft.EntityFrameworkCore;
using LotTrackingWpf.Models;

namespace LotTrackingWpf.Data;

public sealed class LotTrackingDbContext : DbContext
{
    public LotTrackingDbContext(
        DbContextOptions<LotTrackingDbContext> options
    ) : base(options)
    {
    }

    public DbSet<UserAccount> Users => Set<UserAccount>();

    public DbSet<Supplier> Suppliers => Set<Supplier>();

    public DbSet<Wafer> Wafers => Set<Wafer>();

    public DbSet<Lot> Lots => Set<Lot>();

    public DbSet<Plant> Plants => Set<Plant>();

    public DbSet<ProcessLog> ProcessLogs => Set<ProcessLog>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // User Account
        b.Entity<UserAccount>(e =>
        {
            e.ToTable("users");

            e.HasKey(x => x.Id);

            e.Property(x => x.Id)
                .HasColumnName("id");

            e.Property(x => x.Username)
                .HasColumnName("username")
                .HasMaxLength(100)
                .IsRequired();

            e.HasIndex(x => x.Username)
                .IsUnique();

            e.Property(x => x.PasswordHash)
                .HasColumnName("password_hash")
                .HasMaxLength(255)
                .IsRequired();

            e.Property(x => x.Role)
                .HasColumnName("role")
                .HasMaxLength(30)
                .IsRequired();

            e.Property(x => x.CreatedAt)
                .HasColumnName("created_at");
        });

        // Supplier
        b.Entity<Supplier>(e =>
        {
            e.ToTable("suppliers");

            e.HasKey(x => x.Id);

            e.Property(x => x.Id)
                .HasColumnName("id");

            e.Property(x => x.SupplierName)
                .HasColumnName("supplier_name")
                .HasMaxLength(150)
                .IsRequired();

            e.HasIndex(x => x.SupplierName)
                .IsUnique();

            e.Property(x => x.ContactEmail)
                .HasColumnName("contact_email")
                .HasMaxLength(200);

            e.Property(x => x.Location)
                .HasColumnName("location")
                .HasMaxLength(200);

            e.Property(x => x.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("datetime(3)");
        });

        // Lot
        b.Entity<Lot>(e =>
        {
            e.ToTable("lots");

            e.HasKey(x => x.Id);

            e.Property(x => x.Id)
                .HasColumnName("id");

            e.Property(x => x.LotCode)
                .HasColumnName("lot_code")
                .HasMaxLength(100)
                .IsRequired();

            e.HasIndex(x => x.LotCode)
                .IsUnique();

            e.Property(x => x.Status)
                .HasColumnName("status")
                .HasMaxLength(40);

            e.Property(x => x.CurrentPlantNumber)
                .HasColumnName("current_plant_number");

            e.Property(x => x.TotalWafers)
                .HasColumnName("total_wafers");

            e.Property(x => x.ProcessingStarted)
                .HasColumnName("processing_started");

            e.Property(x => x.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("datetime(3)");

            e.Property(x => x.CompletedAt)
                .HasColumnName("completed_at")
                .HasColumnType("datetime(3)");
        });

        // Wafer
        b.Entity<Wafer>(e =>
        {
            e.ToTable("wafers");

            e.HasKey(x => x.Id);

            e.Property(x => x.Id)
                .HasColumnName("id");

            e.Property(x => x.WaferCode)
                .HasColumnName("wafer_code")
                .HasMaxLength(100)
                .IsRequired();

            e.HasIndex(x => x.WaferCode)
                .IsUnique();

            e.Property(x => x.WaferType)
                .HasColumnName("wafer_type")
                .HasMaxLength(100);

            e.Property(x => x.SupplierId)
                .HasColumnName("supplier_id");

            e.Property(x => x.LotId)
                .HasColumnName("lot_id");

            e.Property(x => x.Status)
                .HasColumnName("status")
                .HasMaxLength(40);

            e.Property(x => x.CreatedAt)
                .HasColumnName("created_at")
                .HasColumnType("datetime(3)");

            e.Ignore(x => x.SupplierName);

            e.HasOne<Supplier>()
                .WithMany()
                .HasForeignKey(x => x.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne<Lot>()
                .WithMany()
                .HasForeignKey(x => x.LotId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Plant
        b.Entity<Plant>(e =>
        {
            e.ToTable("plants");

            e.HasKey(x => x.PlantNumber);

            e.Property(x => x.PlantNumber)
                .HasColumnName("plant_number")
                .ValueGeneratedNever();

            e.Property(x => x.PlantName)
                .HasColumnName("plant_name")
                .HasMaxLength(100);

            e.Property(x => x.BaseProcessingTimeMs)
                .HasColumnName("base_processing_time_ms");

            e.Property(x => x.Occupied)
                .HasColumnName("occupied");

            e.Property(x => x.CurrentLotId)
                .HasColumnName("current_lot_id");

            e.Ignore(x => x.CurrentLotCode);

            e.HasOne<Lot>()
                .WithMany()
                .HasForeignKey(x => x.CurrentLotId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Process Log
        b.Entity<ProcessLog>(e =>
        {
            e.ToTable("process_logs");

            e.HasKey(x => x.Id);

            e.Property(x => x.Id)
                .HasColumnName("id");

            e.Property(x => x.LogTime)
                .HasColumnName("log_time")
                .HasColumnType("datetime(3)");

            e.Property(x => x.EntityType)
                .HasColumnName("entity_type")
                .HasMaxLength(30);

            e.Property(x => x.EntityCode)
                .HasColumnName("entity_code")
                .HasMaxLength(100);

            e.Property(x => x.PlantNumber)
                .HasColumnName("plant_number");

            e.Property(x => x.Message)
                .HasColumnName("message")
                .HasColumnType("text");
        });
    }
}
