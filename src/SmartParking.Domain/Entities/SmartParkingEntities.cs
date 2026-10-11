namespace SmartParking.Domain.Entities;

public sealed class Client
{
    public int ClientId { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string? BusinessName { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactNo { get; set; }
    public string? AltContactNo { get; set; }
    public string? Email { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public string? Logo { get; set; }
    public bool IsDeleted { get; set; }

    public ICollection<Site> Sites { get; set; } = new List<Site>();
    public ICollection<User> Users { get; set; } = new List<User>();
}

public sealed class User
{
    public int UserId { get; set; }
    public int? ClientId { get; set; }
    public string EmailId { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string MobileNo { get; set; } = string.Empty;
    public string? ProjectName { get; set; } = "SmartParking";
    public int? ExtraId { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public Client? Client { get; set; }
}

public sealed class Site
{
    public int SiteId { get; set; }
    public int ClientId { get; set; }
    public string SiteName { get; set; } = string.Empty;
    public string? SiteCity { get; set; }
    public string? SiteAddress { get; set; }
    public string? SitePinCode { get; set; }
    public int TotalBuildings { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public Client Client { get; set; } = null!;
    public ICollection<Building> Buildings { get; set; } = new List<Building>();
}

public sealed class Building
{
    public int BuildingId { get; set; }
    public int SiteId { get; set; }
    public string BuildingName { get; set; } = string.Empty;
    public string? BuildingManagerName { get; set; }
    public string? ContactNo { get; set; }

    public Site Site { get; set; } = null!;
    public ICollection<Floor> Floors { get; set; } = new List<Floor>();
}

public sealed class Floor
{
    public int FloorId { get; set; }
    public int BuildingId { get; set; }
    public string FloorNo { get; set; } = string.Empty;
    public bool IsOperational { get; set; } = true;
    public int TotalParkingSpots { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public Building Building { get; set; } = null!;
    public ICollection<Parking> Parkings { get; set; } = new List<Parking>();
}

public sealed class Parking
{
    public int ParkId { get; set; }
    public int FloorId { get; set; }
    public string? CustName { get; set; }
    public string? CustMobileNo { get; set; }
    public string VehicleNo { get; set; } = string.Empty;
    public DateTime ParkDate { get; set; } = DateTime.UtcNow;
    public int ParkSpotNo { get; set; }
    public DateTime InTime { get; set; } = DateTime.UtcNow;
    public DateTime? OutTime { get; set; }
    public decimal Amount { get; set; }
    public decimal ExtraCharge { get; set; }
    public string? ParkingNo { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public Floor Floor { get; set; } = null!;
}
