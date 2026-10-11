using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartParking.Domain.Entities;

namespace SmartParking.Infrastructure.Persistence;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(
        SmartParkingDbContext db,
        IPasswordHasher<User> passwordHasher,
        ILogger logger,
        CancellationToken ct = default)
    {
        if (await db.Clients.AnyAsync(ct))
        {
            return;
        }

        logger.LogInformation("Seeding initial SmartParking reference data...");

        var client = new Client
        {
            ClientName = "Metro Smart Parking Corp",
            BusinessName = "Metro Mobility & Infrastructure Ltd.",
            ContactPerson = "Venkatesh Rao",
            ContactNo = "+91-9876543210",
            AltContactNo = "+91-9123456780",
            Email = "ops@metrosmartparking.com",
            CreatedDate = DateTime.UtcNow,
            Logo = "https://cdn.metrosmartparking.com/logo.png"
        };

        db.Clients.Add(client);
        await db.SaveChangesAsync(ct);

        var adminUser = new User
        {
            ClientId = client.ClientId,
            EmailId = "admin@metrosmartparking.com",
            FullName = "System Administrator",
            MobileNo = "9876543210",
            ProjectName = "SmartParking",
            CreatedDate = DateTime.UtcNow
        };
        adminUser.PasswordHash = passwordHasher.HashPassword(adminUser, "Admin@123");
        db.Users.Add(adminUser);

        var site = new Site
        {
            ClientId = client.ClientId,
            SiteName = "Wroclavia Mall & Tech Park",
            SiteCity = "Mumbai",
            SiteAddress = "LBS Marg, Thane West",
            SitePinCode = "400601",
            TotalBuildings = 2,
            CreatedDate = DateTime.UtcNow,
            Buildings =
            [
                new Building
                {
                    BuildingName = "Budynek A",
                    BuildingManagerName = "Arjun Mehta",
                    ContactNo = "9820011223",
                    Floors =
                    [
                        new Floor { FloorNo = "P-1", IsOperational = true, TotalParkingSpots = 50 },
                        new Floor { FloorNo = "P-2", IsOperational = true, TotalParkingSpots = 50 }
                    ]
                },
                new Building
                {
                    BuildingName = "Budynek B",
                    BuildingManagerName = "Priya Nair",
                    ContactNo = "9820044556",
                    Floors =
                    [
                        new Floor { FloorNo = "B-1", IsOperational = true, TotalParkingSpots = 40 }
                    ]
                }
            ]
        };

        db.Sites.Add(site);
        await db.SaveChangesAsync(ct);

        var firstFloor = site.Buildings.First().Floors.First();
        db.Parkings.Add(new Parking
        {
            FloorId = firstFloor.FloorId,
            CustName = "Gourav Sharma",
            CustMobileNo = "9811122233",
            VehicleNo = "MH-04-AB-1234",
            ParkDate = DateTime.UtcNow.Date,
            ParkSpotNo = 1,
            InTime = DateTime.UtcNow.AddMinutes(-45),
            OutTime = null,
            Amount = 40m,
            ExtraCharge = 0m,
            ParkingNo = "PK-1001"
        });

        await db.SaveChangesAsync(ct);
        logger.LogInformation("SmartParking seed data created successfully.");
    }
}
