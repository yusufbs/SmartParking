-- ============================================================================
-- SmartParking SQL Server Production Schema (DDL)
-- Target: Microsoft SQL Server 2022+ / Azure SQL Database
-- ============================================================================

SET NOCOUNT ON;
GO

IF OBJECT_ID('dbo.Clients', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Clients (
        ClientId        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Clients PRIMARY KEY CLUSTERED,
        ClientName      NVARCHAR(150)     NOT NULL,
        BusinessName    NVARCHAR(200)     NULL,
        ContactPerson   NVARCHAR(150)     NULL,
        ContactNo       VARCHAR(25)       NULL,
        AltContactNo    VARCHAR(25)       NULL,
        Email           NVARCHAR(256)     NULL,
        CreatedDate     DATETIME2(3)      NOT NULL CONSTRAINT DF_Clients_CreatedDate DEFAULT (SYSUTCDATETIME()),
        Logo            NVARCHAR(500)     NULL,
        IsDeleted       BIT               NOT NULL CONSTRAINT DF_Clients_IsDeleted DEFAULT (0)
    );

    CREATE NONCLUSTERED INDEX IX_Clients_IsDeleted ON dbo.Clients (IsDeleted);
END
GO

IF OBJECT_ID('dbo.Users', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Users (
        UserId          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Users PRIMARY KEY CLUSTERED,
        ClientId        INT               NULL CONSTRAINT FK_Users_Clients FOREIGN KEY REFERENCES dbo.Clients(ClientId) ON DELETE SET NULL,
        EmailId         NVARCHAR(256)     NOT NULL,
        PasswordHash    NVARCHAR(512)     NOT NULL,
        FullName        NVARCHAR(150)     NOT NULL,
        MobileNo        VARCHAR(25)       NOT NULL,
        ProjectName     NVARCHAR(100)     NULL CONSTRAINT DF_Users_ProjectName DEFAULT ('SmartParking'),
        ExtraId         INT               NULL,
        CreatedDate     DATETIME2(3)      NOT NULL CONSTRAINT DF_Users_CreatedDate DEFAULT (SYSUTCDATETIME())
    );

    CREATE UNIQUE NONCLUSTERED INDEX UX_Users_EmailId ON dbo.Users (EmailId);
    CREATE NONCLUSTERED INDEX IX_Users_ClientId ON dbo.Users (ClientId) INCLUDE (EmailId, FullName, MobileNo);
END
GO

IF OBJECT_ID('dbo.Sites', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Sites (
        SiteId          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Sites PRIMARY KEY CLUSTERED,
        ClientId        INT               NOT NULL CONSTRAINT FK_Sites_Clients FOREIGN KEY REFERENCES dbo.Clients(ClientId) ON DELETE CASCADE,
        SiteName        NVARCHAR(200)     NOT NULL,
        SiteCity        NVARCHAR(100)     NULL,
        SiteAddress     NVARCHAR(500)     NULL,
        SitePinCode     VARCHAR(20)       NULL,
        TotalBuildings  INT               NOT NULL CONSTRAINT DF_Sites_TotalBuildings DEFAULT (0),
        CreatedDate     DATETIME2(3)      NOT NULL CONSTRAINT DF_Sites_CreatedDate DEFAULT (SYSUTCDATETIME())
    );

    CREATE NONCLUSTERED INDEX IX_Sites_ClientId ON dbo.Sites (ClientId) INCLUDE (SiteName, SiteCity, TotalBuildings);
END
GO

IF OBJECT_ID('dbo.Buildings', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Buildings (
        BuildingId          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Buildings PRIMARY KEY CLUSTERED,
        SiteId              INT               NOT NULL CONSTRAINT FK_Buildings_Sites FOREIGN KEY REFERENCES dbo.Sites(SiteId) ON DELETE CASCADE,
        BuildingName        NVARCHAR(150)     NOT NULL,
        BuildingManagerName NVARCHAR(150)     NULL,
        ContactNo           VARCHAR(25)       NULL
    );

    CREATE NONCLUSTERED INDEX IX_Buildings_SiteId ON dbo.Buildings (SiteId) INCLUDE (BuildingName, BuildingManagerName, ContactNo);
END
GO

IF OBJECT_ID('dbo.Floors', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Floors (
        FloorId             INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Floors PRIMARY KEY CLUSTERED,
        BuildingId          INT               NOT NULL CONSTRAINT FK_Floors_Buildings FOREIGN KEY REFERENCES dbo.Buildings(BuildingId) ON DELETE CASCADE,
        FloorNo             NVARCHAR(50)      NOT NULL,
        IsOperational       BIT               NOT NULL CONSTRAINT DF_Floors_IsOperational DEFAULT (1),
        TotalParkingSpots   INT               NOT NULL CONSTRAINT CK_Floors_TotalParkingSpots CHECK (TotalParkingSpots >= 0),
        RowVersion          ROWVERSION        NOT NULL
    );

    CREATE NONCLUSTERED INDEX IX_Floors_BuildingId ON dbo.Floors (BuildingId) INCLUDE (FloorNo, IsOperational, TotalParkingSpots);
END
GO

IF OBJECT_ID('dbo.Parkings', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Parkings (
        ParkId          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Parkings PRIMARY KEY CLUSTERED,
        FloorId         INT               NOT NULL CONSTRAINT FK_Parkings_Floors FOREIGN KEY REFERENCES dbo.Floors(FloorId) ON DELETE CASCADE,
        CustName        NVARCHAR(150)     NULL,
        CustMobileNo    VARCHAR(25)       NULL,
        VehicleNo       NVARCHAR(50)      NOT NULL,
        ParkDate        DATETIME2(3)      NOT NULL,
        ParkSpotNo      INT               NOT NULL,
        InTime          DATETIME2(3)      NOT NULL,
        OutTime         DATETIME2(3)      NULL,
        Amount          DECIMAL(18,2)     NOT NULL CONSTRAINT DF_Parkings_Amount DEFAULT (0),
        ExtraCharge     DECIMAL(18,2)     NOT NULL CONSTRAINT DF_Parkings_ExtraCharge DEFAULT (0),
        ParkingNo       NVARCHAR(50)      NULL,
        RowVersion      ROWVERSION        NOT NULL
    );

    -- Critical filtered unique index preventing concurrent double-booking of the same active spot
    CREATE UNIQUE NONCLUSTERED INDEX UX_Parkings_ActiveSpot
        ON dbo.Parkings (FloorId, ParkSpotNo)
        WHERE OutTime IS NULL AND ParkSpotNo > 0;

    -- Covering index for GetAdminAllParkings and hierarchy read queries
    CREATE NONCLUSTERED INDEX IX_Parkings_Search
        ON dbo.Parkings (FloorId, ParkDate DESC, VehicleNo)
        INCLUDE (CustName, CustMobileNo, ParkSpotNo, InTime, OutTime, Amount, ExtraCharge, ParkingNo);
END
GO
