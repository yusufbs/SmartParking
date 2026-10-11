using Microsoft.EntityFrameworkCore;
using SmartParking.Application.Contracts;

namespace SmartParking.Infrastructure.Persistence;

public static class SmartParkingCompiledQueries
{
    public static readonly Func<SmartParkingDbContext, IAsyncEnumerable<GetWClients>> GetAllClientsAsync =
        EF.CompileAsyncQuery((SmartParkingDbContext db) =>
            db.Clients
                .AsNoTracking()
                .OrderByDescending(c => c.ClientId)
                .Select(c => new GetWClients
                {
                    ClientId = c.ClientId,
                    ClientName = c.ClientName,
                    BusinessName = c.BusinessName,
                    ContactPerson = c.ContactPerson,
                    ContactNo = c.ContactNo,
                    AltContactNo = c.AltContactNo,
                    Email = c.Email,
                    CreatedDate = c.CreatedDate,
                    Logo = c.Logo
                }));

    public static readonly Func<SmartParkingDbContext, IAsyncEnumerable<FloorListItemDto>> GetAllFloorsAsync =
        EF.CompileAsyncQuery((SmartParkingDbContext db) =>
            db.Floors
                .AsNoTracking()
                .OrderBy(f => f.FloorId)
                .Select(f => new FloorListItemDto
                {
                    FloorId = f.FloorId,
                    BuildingId = f.BuildingId,
                    FloorNo = f.FloorNo,
                    IsOperational = f.IsOperational,
                    TotalParkingSpots = f.TotalParkingSpots,
                    BuildingName = f.Building.BuildingName,
                    SiteName = f.Building.Site.SiteName
                }));

    public static readonly Func<SmartParkingDbContext, IAsyncEnumerable<ParkingListItemDto>> GetAllActiveParkingsAsync =
        EF.CompileAsyncQuery((SmartParkingDbContext db) =>
            db.Parkings
                .AsNoTracking()
                .Where(p => p.OutTime == null)
                .OrderByDescending(p => p.InTime)
                .Select(p => new ParkingListItemDto
                {
                    ParkId = p.ParkId,
                    FloorId = p.FloorId,
                    CustName = p.CustName,
                    CustMobileNo = p.CustMobileNo,
                    VehicleNo = p.VehicleNo,
                    ParkDate = p.ParkDate,
                    ParkSpotNo = p.ParkSpotNo,
                    InTime = p.InTime,
                    OutTime = p.OutTime,
                    Amount = (double)p.Amount,
                    ExtraCharge = (double)p.ExtraCharge,
                    ParkingNo = p.ParkingNo,
                    FloorNo = p.Floor.FloorNo,
                    BuildingName = p.Floor.Building.BuildingName,
                    SiteName = p.Floor.Building.Site.SiteName,
                    ClientName = p.Floor.Building.Site.Client.ClientName
                }));
}
