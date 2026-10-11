using FluentValidation;
using SmartParking.Application.Contracts;

namespace SmartParking.Application.Validation;

public sealed class UserLoginValidator : AbstractValidator<UserLogin>
{
    public UserLoginValidator()
    {
        RuleFor(x => x.EmailId).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
    }
}

public sealed class UserRegisterValidator : AbstractValidator<UserMasterViewClient>
{
    public UserRegisterValidator()
    {
        RuleFor(x => x.EmailId).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(4).MaximumLength(128);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.MobileNo).NotEmpty().MaximumLength(25);
    }
}

public sealed class ClientValidator : AbstractValidator<GetWClients>
{
    public ClientValidator()
    {
        RuleFor(x => x.ClientName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.BusinessName).MaximumLength(200);
        RuleFor(x => x.ContactPerson).MaximumLength(150);
        RuleFor(x => x.ContactNo).MaximumLength(25);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

public sealed class SiteValidator : AbstractValidator<GetVSiteMasterView>
{
    public SiteValidator()
    {
        RuleFor(x => x.ClientId).GreaterThan(0);
        RuleFor(x => x.SiteName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.SiteCity).MaximumLength(100);
        RuleFor(x => x.SitePinCode).MaximumLength(20);
    }
}

public sealed class FloorValidator : AbstractValidator<GetVFloors>
{
    public FloorValidator()
    {
        RuleFor(x => x.BuildingId).GreaterThan(0);
        RuleFor(x => x.FloorNo).NotEmpty().MaximumLength(50);
        RuleFor(x => x.TotalParkingSpots).GreaterThanOrEqualTo(0);
    }
}

public sealed class AddParkingValidator : AbstractValidator<GetVParkings>
{
    public AddParkingValidator()
    {
        RuleFor(x => x.FloorId).GreaterThan(0);
        RuleFor(x => x.VehicleNo).NotEmpty().MaximumLength(50);
        RuleFor(x => x.ParkSpotNo).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Amount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ExtraCharge).GreaterThanOrEqualTo(0);
    }
}

public sealed class ExitParkingValidator : AbstractValidator<ExitParking>
{
    public ExitParkingValidator()
    {
        RuleFor(x => x.ParkId).GreaterThan(0);
        RuleFor(x => x.ExtraCharge).GreaterThanOrEqualTo(0);
    }
}
