namespace SmartParking.Application.Common;

public static class FeatureFlags
{
    public const string EnableDynamicSurgePricing = "EnableDynamicSurgePricing";
    public const string EnableStrictSpotValidation = "EnableStrictSpotValidation";
    public const string EnableDetailedAdminAnalytics = "EnableDetailedAdminAnalytics";
}

public static class CacheTags
{
    public const string Clients = "clients";
    public const string Sites = "sites";
    public const string Buildings = "buildings";
    public const string Floors = "floors";
    public const string ParkingsAll = "parkings-all";
    public const string ParkingsActive = "parkings-active";

    public static string SiteById(int siteId) => $"site:{siteId}";
    public static string ClientSites(int clientId) => $"client-sites:{clientId}";
    public static string BuildingFloors(int buildingId) => $"building-floors:{buildingId}";
    public static string SiteFloors(int siteId) => $"site-floors:{siteId}";
}

public static class RateLimitPolicies
{
    public const string Auth = "auth-policy";
    public const string Write = "write-policy";
    public const string Read = "read-policy";
}
