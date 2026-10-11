using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartParking.Domain.Entities;

namespace SmartParking.Infrastructure.Persistence;

public sealed class SmartParkingDbContext(DbContextOptions<SmartParkingDbContext> options) : DbContext(options)
{
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Site> Sites => Set<Site>();
    public DbSet<Building> Buildings => Set<Building>();
    public DbSet<Floor> Floors => Set<Floor>();
    public DbSet<Parking> Parkings => Set<Parking>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("dbo");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SmartParkingDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}

internal sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable("Clients");
        builder.HasKey(x => x.ClientId);
        builder.Property(x => x.ClientId).UseIdentityColumn();
        builder.Property(x => x.ClientName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.BusinessName).HasMaxLength(200);
        builder.Property(x => x.ContactPerson).HasMaxLength(150);
        builder.Property(x => x.ContactNo).HasMaxLength(25).IsUnicode(false);
        builder.Property(x => x.AltContactNo).HasMaxLength(25).IsUnicode(false);
        builder.Property(x => x.Email).HasMaxLength(256);
        builder.Property(x => x.CreatedDate).HasPrecision(3).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.Property(x => x.Logo).HasMaxLength(500);
        builder.Property(x => x.IsDeleted).HasDefaultValue(false);

        builder.HasQueryFilter(x => !x.IsDeleted);
        builder.HasIndex(x => x.IsDeleted).HasDatabaseName("IX_Clients_IsDeleted");
    }
}

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(x => x.UserId);
        builder.Property(x => x.UserId).UseIdentityColumn();
        builder.Property(x => x.EmailId).HasMaxLength(256).IsRequired();
        builder.Property(x => x.PasswordHash).HasMaxLength(512).IsRequired();
        builder.Property(x => x.FullName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.MobileNo).HasMaxLength(25).IsUnicode(false).IsRequired();
        builder.Property(x => x.ProjectName).HasMaxLength(100).HasDefaultValue("SmartParking");
        builder.Property(x => x.CreatedDate).HasPrecision(3).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(x => x.EmailId).IsUnique().HasDatabaseName("UX_Users_EmailId");
        builder.HasIndex(x => x.ClientId)
            .IncludeProperties(x => new { x.EmailId, x.FullName, x.MobileNo })
            .HasDatabaseName("IX_Users_ClientId");

        builder.HasOne(x => x.Client)
            .WithMany(c => c.Users)
            .HasForeignKey(x => x.ClientId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class SiteConfiguration : IEntityTypeConfiguration<Site>
{
    public void Configure(EntityTypeBuilder<Site> builder)
    {
        builder.ToTable("Sites");
        builder.HasKey(x => x.SiteId);
        builder.Property(x => x.SiteId).UseIdentityColumn();
        builder.Property(x => x.SiteName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.SiteCity).HasMaxLength(100);
        builder.Property(x => x.SiteAddress).HasMaxLength(500);
        builder.Property(x => x.SitePinCode).HasMaxLength(20).IsUnicode(false);
        builder.Property(x => x.CreatedDate).HasPrecision(3).HasDefaultValueSql("SYSUTCDATETIME()");

        builder.HasIndex(x => x.ClientId)
            .IncludeProperties(x => new { x.SiteName, x.SiteCity, x.TotalBuildings })
            .HasDatabaseName("IX_Sites_ClientId");

        builder.HasOne(x => x.Client)
            .WithMany(c => c.Sites)
            .HasForeignKey(x => x.ClientId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class BuildingConfiguration : IEntityTypeConfiguration<Building>
{
    public void Configure(EntityTypeBuilder<Building> builder)
    {
        builder.ToTable("Buildings");
        builder.HasKey(x => x.BuildingId);
        builder.Property(x => x.BuildingId).UseIdentityColumn();
        builder.Property(x => x.BuildingName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.BuildingManagerName).HasMaxLength(150);
        builder.Property(x => x.ContactNo).HasMaxLength(25).IsUnicode(false);

        builder.HasIndex(x => x.SiteId)
            .IncludeProperties(x => new { x.BuildingName, x.BuildingManagerName, x.ContactNo })
            .HasDatabaseName("IX_Buildings_SiteId");

        builder.HasOne(x => x.Site)
            .WithMany(s => s.Buildings)
            .HasForeignKey(x => x.SiteId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class FloorConfiguration : IEntityTypeConfiguration<Floor>
{
    public void Configure(EntityTypeBuilder<Floor> builder)
    {
        builder.ToTable("Floors", t => t.HasCheckConstraint("CK_Floors_TotalParkingSpots", "[TotalParkingSpots] >= 0"));
        builder.HasKey(x => x.FloorId);
        builder.Property(x => x.FloorId).UseIdentityColumn();
        builder.Property(x => x.FloorNo).HasMaxLength(50).IsRequired();
        builder.Property(x => x.IsOperational).HasDefaultValue(true);
        builder.Property(x => x.RowVersion).IsRowVersion();

        builder.HasIndex(x => x.BuildingId)
            .IncludeProperties(x => new { x.FloorNo, x.IsOperational, x.TotalParkingSpots })
            .HasDatabaseName("IX_Floors_BuildingId");

        builder.HasOne(x => x.Building)
            .WithMany(b => b.Floors)
            .HasForeignKey(x => x.BuildingId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ParkingConfiguration : IEntityTypeConfiguration<Parking>
{
    public void Configure(EntityTypeBuilder<Parking> builder)
    {
        builder.ToTable("Parkings");
        builder.HasKey(x => x.ParkId);
        builder.Property(x => x.ParkId).UseIdentityColumn();
        builder.Property(x => x.CustName).HasMaxLength(150);
        builder.Property(x => x.CustMobileNo).HasMaxLength(25).IsUnicode(false);
        builder.Property(x => x.VehicleNo).HasMaxLength(50).IsRequired();
        builder.Property(x => x.ParkDate).HasPrecision(3);
        builder.Property(x => x.InTime).HasPrecision(3);
        builder.Property(x => x.OutTime).HasPrecision(3);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.ExtraCharge).HasPrecision(18, 2);
        builder.Property(x => x.ParkingNo).HasMaxLength(50);
        builder.Property(x => x.RowVersion).IsRowVersion();

        // Filtered unique index: prevents two active vehicles from occupying the same spot (>0) on the same floor
        builder.HasIndex(x => new { x.FloorId, x.ParkSpotNo })
            .IsUnique()
            .HasFilter("[OutTime] IS NULL AND [ParkSpotNo] > 0")
            .HasDatabaseName("UX_Parkings_ActiveSpot");

        // Covering index for admin searches and hierarchy filtering
        builder.HasIndex(x => new { x.FloorId, x.ParkDate, x.VehicleNo })
            .IncludeProperties(x => new
            {
                x.CustName,
                x.CustMobileNo,
                x.ParkSpotNo,
                x.InTime,
                x.OutTime,
                x.Amount,
                x.ExtraCharge,
                x.ParkingNo
            })
            .HasDatabaseName("IX_Parkings_Search");

        builder.HasOne(x => x.Floor)
            .WithMany(f => f.Parkings)
            .HasForeignKey(x => x.FloorId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
