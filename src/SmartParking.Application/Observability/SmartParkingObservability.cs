using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;

namespace SmartParking.Application.Observability;

public sealed class SmartParkingMetrics
{
    public const string MeterName = "SmartParking.Api";
    public const string ActivitySourceName = "SmartParking.Domain";

    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, "1.0.0");

    private readonly Counter<long> _checkInCounter;
    private readonly Counter<long> _checkOutCounter;
    private readonly UpDownCounter<long> _activeParkingsGauge;
    private readonly Histogram<double> _parkingDurationMinutes;
    private readonly Counter<double> _revenueCollected;

    public SmartParkingMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName, "1.0.0");

        _checkInCounter = meter.CreateCounter<long>(
            "smartparking.parkings.checkin.count",
            unit: "{ticket}",
            description: "Total number of vehicle parking check-ins.");

        _checkOutCounter = meter.CreateCounter<long>(
            "smartparking.parkings.checkout.count",
            unit: "{ticket}",
            description: "Total number of vehicle parking check-outs.");

        _activeParkingsGauge = meter.CreateUpDownCounter<long>(
            "smartparking.parkings.active",
            unit: "{vehicle}",
            description: "Number of vehicles currently parked.");

        _parkingDurationMinutes = meter.CreateHistogram<double>(
            "smartparking.parkings.duration.minutes",
            unit: "min",
            description: "Duration of completed parking sessions in minutes.");

        _revenueCollected = meter.CreateCounter<double>(
            "smartparking.revenue.collected",
            unit: "{currency}",
            description: "Total parking fees and extra charges collected.");
    }

    public void RecordCheckIn(int floorId, int spotNo, double baseAmount)
    {
        var tags = new TagList
        {
            { "floor_id", floorId },
            { "spot_no", spotNo }
        };
        _checkInCounter.Add(1, tags);
        _activeParkingsGauge.Add(1, new TagList { { "floor_id", floorId } });
        if (baseAmount > 0)
        {
            _revenueCollected.Add(baseAmount, new TagList { { "charge_type", "base" }, { "floor_id", floorId } });
        }
    }

    public void RecordCheckOut(int floorId, double durationMinutes, double extraCharge)
    {
        var tags = new TagList { { "floor_id", floorId } };
        _checkOutCounter.Add(1, tags);
        _activeParkingsGauge.Add(-1, tags);
        _parkingDurationMinutes.Record(Math.Max(0, durationMinutes), tags);
        if (extraCharge > 0)
        {
            _revenueCollected.Add(extraCharge, new TagList { { "charge_type", "extra" }, { "floor_id", floorId } });
        }
    }
}

public static partial class SmartParkingLogMessages
{
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "User registered successfully: UserId={UserId}, Email={EmailId}, ClientId={ClientId}")]
    public static partial void LogUserRegistered(this ILogger logger, int userId, string emailId, int? clientId);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Warning,
        Message = "Failed login attempt for Email={EmailId}")]
    public static partial void LogFailedLogin(this ILogger logger, string emailId);

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Information,
        Message = "Vehicle checked in: ParkId={ParkId}, VehicleNo={VehicleNo}, FloorId={FloorId}, SpotNo={SpotNo}, Amount={Amount}, SurgeApplied={SurgeApplied}")]
    public static partial void LogVehicleCheckedIn(
        this ILogger logger,
        int parkId,
        string vehicleNo,
        int floorId,
        int spotNo,
        decimal amount,
        bool surgeApplied);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Information,
        Message = "Vehicle exited: ParkId={ParkId}, VehicleNo={VehicleNo}, FloorId={FloorId}, DurationMinutes={DurationMinutes:F1}, ExtraCharge={ExtraCharge}")]
    public static partial void LogVehicleExited(
        this ILogger logger,
        int parkId,
        string vehicleNo,
        int floorId,
        double durationMinutes,
        decimal extraCharge);
}
