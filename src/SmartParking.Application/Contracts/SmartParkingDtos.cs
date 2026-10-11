using System.Text.Json.Serialization;

namespace SmartParking.Application.Contracts;

public sealed class ApiResponse
{
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    [JsonPropertyName("result")]
    public bool Result { get; set; }

    [JsonPropertyName("data")]
    public object? Data { get; set; }

    public static ApiResponse Success(object? data = null, string message = "Success") =>
        new() { Result = true, Message = message, Data = data };

    public static ApiResponse Failure(string message, object? data = null) =>
        new() { Result = false, Message = message, Data = data };
}

public sealed class UserLogin
{
    [JsonPropertyName("emailId")]
    public string EmailId { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;
}

public sealed class UserMasterView
{
    [JsonPropertyName("userId")]
    public int UserId { get; set; }

    [JsonPropertyName("emailId")]
    public string EmailId { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; set; } = "***";

    [JsonPropertyName("fullName")]
    public string FullName { get; set; } = string.Empty;

    [JsonPropertyName("mobileNo")]
    public string MobileNo { get; set; } = string.Empty;
}

public sealed class UserMasterViewClient
{
    [JsonPropertyName("userId")]
    public int UserId { get; set; }

    [JsonPropertyName("emailId")]
    public string EmailId { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; set; } = string.Empty;

    [JsonPropertyName("fullName")]
    public string FullName { get; set; } = string.Empty;

    [JsonPropertyName("mobileNo")]
    public string MobileNo { get; set; } = string.Empty;

    [JsonPropertyName("clientId")]
    public int ClientId { get; set; }
}

public sealed class UserMasterNew
{
    [JsonPropertyName("userId")]
    public int UserId { get; set; }

    [JsonPropertyName("emailId")]
    public string EmailId { get; set; } = string.Empty;

    [JsonPropertyName("password")]
    public string Password { get; set; } = "***";

    [JsonPropertyName("createdDate")]
    public DateTime? CreatedDate { get; set; }

    [JsonPropertyName("projectName")]
    public string? ProjectName { get; set; }

    [JsonPropertyName("fullName")]
    public string FullName { get; set; } = string.Empty;

    [JsonPropertyName("mobileNo")]
    public string MobileNo { get; set; } = string.Empty;

    [JsonPropertyName("extraId")]
    public int? ExtraId { get; set; }
}

public sealed class GetWClients
{
    [JsonPropertyName("clientId")]
    public int ClientId { get; set; }

    [JsonPropertyName("clientName")]
    public string? ClientName { get; set; }

    [JsonPropertyName("businessName")]
    public string? BusinessName { get; set; }

    [JsonPropertyName("contactPerson")]
    public string? ContactPerson { get; set; }

    [JsonPropertyName("contactNo")]
    public string? ContactNo { get; set; }

    [JsonPropertyName("altContactNo")]
    public string? AltContactNo { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("createdDate")]
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("logo")]
    public string? Logo { get; set; }
}

public sealed class GetVBuildings
{
    [JsonPropertyName("buildingId")]
    public int BuildingId { get; set; }

    [JsonPropertyName("siteId")]
    public int SiteId { get; set; }

    [JsonPropertyName("buildingName")]
    public string? BuildingName { get; set; }

    [JsonPropertyName("buildingManagerName")]
    public string? BuildingManagerName { get; set; }

    [JsonPropertyName("contactNo")]
    public string? ContactNo { get; set; }
}

public sealed class GetVSiteMasterView
{
    [JsonPropertyName("siteId")]
    public int SiteId { get; set; }

    [JsonPropertyName("clientId")]
    public int ClientId { get; set; }

    [JsonPropertyName("siteName")]
    public string? SiteName { get; set; }

    [JsonPropertyName("siteCity")]
    public string? SiteCity { get; set; }

    [JsonPropertyName("siteAddress")]
    public string? SiteAddress { get; set; }

    [JsonPropertyName("sitePinCode")]
    public string? SitePinCode { get; set; }

    [JsonPropertyName("totalBuildings")]
    public int TotalBuildings { get; set; }

    [JsonPropertyName("createdDate")]
    public DateTime? CreatedDate { get; set; }

    // Note: Exact double-'s' property name matching the official SmartParking OpenAPI specification
    [JsonPropertyName("getVBuildingss")]
    public List<GetVBuildings>? GetVBuildingss { get; set; }
}

public sealed class SiteListItemDto
{
    [JsonPropertyName("siteId")]
    public int SiteId { get; set; }

    [JsonPropertyName("clientId")]
    public int ClientId { get; set; }

    [JsonPropertyName("clientName")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ClientName { get; set; }

    [JsonPropertyName("siteName")]
    public string? SiteName { get; set; }

    [JsonPropertyName("siteCity")]
    public string? SiteCity { get; set; }

    [JsonPropertyName("siteAddress")]
    public string? SiteAddress { get; set; }

    [JsonPropertyName("sitePinCode")]
    public string? SitePinCode { get; set; }

    [JsonPropertyName("totalBuildings")]
    public int TotalBuildings { get; set; }

    [JsonPropertyName("createdDate")]
    public DateTime? CreatedDate { get; set; }
}

public sealed class GetVFloors
{
    [JsonPropertyName("floorId")]
    public int FloorId { get; set; }

    [JsonPropertyName("buildingId")]
    public int BuildingId { get; set; }

    [JsonPropertyName("floorNo")]
    public string? FloorNo { get; set; }

    [JsonPropertyName("isOperational")]
    public bool IsOperational { get; set; } = true;

    [JsonPropertyName("totalParkingSpots")]
    public int TotalParkingSpots { get; set; }
}

public sealed class FloorListItemDto
{
    [JsonPropertyName("floorId")]
    public int FloorId { get; set; }

    [JsonPropertyName("buildingId")]
    public int BuildingId { get; set; }

    [JsonPropertyName("floorNo")]
    public string? FloorNo { get; set; }

    [JsonPropertyName("isOperational")]
    public bool IsOperational { get; set; }

    [JsonPropertyName("totalParkingSpots")]
    public int TotalParkingSpots { get; set; }

    [JsonPropertyName("buildingName")]
    public string? BuildingName { get; set; }

    [JsonPropertyName("siteName")]
    public string? SiteName { get; set; }
}

public sealed class GetVParkings
{
    [JsonPropertyName("parkId")]
    public int ParkId { get; set; }

    [JsonPropertyName("floorId")]
    public int FloorId { get; set; }

    [JsonPropertyName("custName")]
    public string? CustName { get; set; }

    [JsonPropertyName("custMobileNo")]
    public string? CustMobileNo { get; set; }

    [JsonPropertyName("vehicleNo")]
    public string? VehicleNo { get; set; }

    [JsonPropertyName("parkDate")]
    public DateTime ParkDate { get; set; }

    [JsonPropertyName("parkSpotNo")]
    public int ParkSpotNo { get; set; }

    [JsonPropertyName("inTime")]
    public DateTime InTime { get; set; }

    [JsonPropertyName("outTime")]
    public DateTime? OutTime { get; set; }

    [JsonPropertyName("amount")]
    public double Amount { get; set; }

    [JsonPropertyName("extraCharge")]
    public double ExtraCharge { get; set; }

    [JsonPropertyName("parkingNo")]
    public string? ParkingNo { get; set; }
}

public sealed class ParkingListItemDto
{
    [JsonPropertyName("parkId")]
    public int ParkId { get; set; }

    [JsonPropertyName("floorId")]
    public int FloorId { get; set; }

    [JsonPropertyName("custName")]
    public string? CustName { get; set; }

    [JsonPropertyName("custMobileNo")]
    public string? CustMobileNo { get; set; }

    [JsonPropertyName("vehicleNo")]
    public string? VehicleNo { get; set; }

    [JsonPropertyName("parkDate")]
    public DateTime ParkDate { get; set; }

    [JsonPropertyName("parkSpotNo")]
    public int ParkSpotNo { get; set; }

    [JsonPropertyName("inTime")]
    public DateTime InTime { get; set; }

    [JsonPropertyName("outTime")]
    public DateTime? OutTime { get; set; }

    [JsonPropertyName("amount")]
    public double Amount { get; set; }

    [JsonPropertyName("extraCharge")]
    public double ExtraCharge { get; set; }

    [JsonPropertyName("floorNo")]
    public string? FloorNo { get; set; }

    [JsonPropertyName("buildingName")]
    public string? BuildingName { get; set; }

    [JsonPropertyName("siteName")]
    public string? SiteName { get; set; }

    [JsonPropertyName("parkingNo")]
    public string? ParkingNo { get; set; }

    [JsonPropertyName("clientName")]
    public string? ClientName { get; set; }
}

public sealed class ExitParking
{
    [JsonPropertyName("parkId")]
    public int ParkId { get; set; }

    [JsonPropertyName("outTime")]
    public DateTime OutTime { get; set; }

    [JsonPropertyName("extraCharge")]
    public double ExtraCharge { get; set; }
}

public sealed class AllParkingSearch
{
    [JsonPropertyName("clientId")]
    public int ClientId { get; set; }

    [JsonPropertyName("siteId")]
    public int SiteId { get; set; }

    [JsonPropertyName("buildingId")]
    public int BuildingId { get; set; }

    [JsonPropertyName("floorId")]
    public int FloorId { get; set; }

    [JsonPropertyName("fromDate")]
    public DateTime? FromDate { get; set; }

    [JsonPropertyName("toDate")]
    public DateTime? ToDate { get; set; }

    [JsonPropertyName("vehicleNo")]
    public string? VehicleNo { get; set; }
}
