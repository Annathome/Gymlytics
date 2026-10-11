/*
    Standalone schema for the proposed Gym ERD.
    Run in SQL Server Management Studio. This creates Gym_ERD_Complete and does not
    modify the application's existing Gym database.
*/
IF DB_ID(N'Gym_ERD_Complete') IS NULL
    CREATE DATABASE [Gym_ERD_Complete];
GO

USE [Gym_ERD_Complete];
GO

CREATE TABLE dbo.Users (
    UserId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
    Email nvarchar(100) NOT NULL,
    PasswordHash nvarchar(255) NOT NULL,
    FirstName nvarchar(100) NOT NULL,
    LastName nvarchar(100) NOT NULL,
    PhoneNumber nvarchar(20) NOT NULL,
    ProfileImageUrl nvarchar(500) NULL,
    Role nvarchar(30) NOT NULL,
    IsActive bit NOT NULL CONSTRAINT DF_Users_IsActive DEFAULT (1),
    CreatedAt datetime2(7) NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT UQ_Users_Email UNIQUE (Email)
);
GO

CREATE TABLE dbo.MembershipPlans (
    MembershipPlanId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_MembershipPlans PRIMARY KEY,
    Name nvarchar(100) NOT NULL,
    Price decimal(18,2) NOT NULL,
    DurationMonths int NOT NULL,
    IsActive bit NOT NULL CONSTRAINT DF_MembershipPlans_IsActive DEFAULT (1),
    CONSTRAINT CK_MembershipPlans_Price CHECK (Price > 0),
    CONSTRAINT CK_MembershipPlans_Duration CHECK (DurationMonths > 0)
);
GO

CREATE TABLE dbo.Members (
    UserId int NOT NULL CONSTRAINT PK_Members PRIMARY KEY,
    MembershipNumber nvarchar(50) NOT NULL,
    Status nvarchar(30) NOT NULL CONSTRAINT DF_Members_Status DEFAULT (N'Active'),
    CONSTRAINT UQ_Members_MembershipNumber UNIQUE (MembershipNumber),
    CONSTRAINT FK_Members_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId)
);
GO

CREATE TABLE dbo.Memberships (
    MembershipId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Memberships PRIMARY KEY,
    MemberId int NOT NULL,
    MembershipPlanId int NOT NULL,
    StartDate date NOT NULL,
    EndDate date NOT NULL,
    Status nvarchar(30) NOT NULL CONSTRAINT DF_Memberships_Status DEFAULT (N'Active'),
    CreatedAt datetime2(7) NOT NULL CONSTRAINT DF_Memberships_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_Memberships_Members FOREIGN KEY (MemberId) REFERENCES dbo.Members(UserId),
    CONSTRAINT FK_Memberships_MembershipPlans FOREIGN KEY (MembershipPlanId) REFERENCES dbo.MembershipPlans(MembershipPlanId),
    CONSTRAINT CK_Memberships_Dates CHECK (EndDate >= StartDate)
);
GO

CREATE TABLE dbo.Staff (
    UserId int NOT NULL CONSTRAINT PK_Staff PRIMARY KEY,
    StaffRole nvarchar(30) NOT NULL,
    StaffStatus nvarchar(30) NOT NULL CONSTRAINT DF_Staff_Status DEFAULT (N'Active'),
    LastLoginAt datetime2(7) NULL,
    CONSTRAINT FK_Staff_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId)
);
GO

CREATE TABLE dbo.StaffActivityLog (
    StaffActivityLogId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_StaffActivityLog PRIMARY KEY,
    StaffId int NOT NULL,
    ActionType nvarchar(30) NOT NULL,
    OccurredAt datetime2(7) NOT NULL CONSTRAINT DF_StaffActivityLog_OccurredAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_StaffActivityLog_Staff FOREIGN KEY (StaffId) REFERENCES dbo.Staff(UserId) ON DELETE CASCADE
);
GO

CREATE TABLE dbo.StaffAttendanceLogs (
    StaffAttendanceLogId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_StaffAttendanceLogs PRIMARY KEY,
    StaffId int NOT NULL,
    AttendanceDate date NOT NULL,
    AttendanceStatus nvarchar(20) NOT NULL,
    CheckedInAt datetime2(7) NULL,
    MarkedAbsentAt datetime2(7) NULL,
    MarkedAbsentByStaffId int NULL,
    CONSTRAINT FK_StaffAttendanceLogs_Staff FOREIGN KEY (StaffId) REFERENCES dbo.Staff(UserId) ON DELETE CASCADE,
    CONSTRAINT FK_StaffAttendanceLogs_MarkedAbsentBy FOREIGN KEY (MarkedAbsentByStaffId) REFERENCES dbo.Staff(UserId),
    CONSTRAINT UQ_StaffAttendanceLogs_StaffDate UNIQUE (StaffId, AttendanceDate),
    CONSTRAINT CK_StaffAttendanceLogs_Status CHECK (AttendanceStatus IN (N'Present', N'Absent'))
);
GO

CREATE TABLE dbo.FrontDeskEmailNotifications (
    Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_FrontDeskEmailNotifications PRIMARY KEY,
    StaffId int NOT NULL,
    Title nvarchar(200) NOT NULL,
    Message nvarchar(1000) NOT NULL,
    Status nvarchar(20) NOT NULL,
    EmailType nvarchar(80) NOT NULL,
    DeliveryKey nvarchar(200) NOT NULL,
    CreatedAt datetime2(7) NOT NULL CONSTRAINT DF_FrontDeskEmailNotifications_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_FrontDeskEmailNotifications_Staff FOREIGN KEY (StaffId) REFERENCES dbo.Staff(UserId)
);
GO

CREATE TABLE dbo.SystemSettings (
    SettingKey nvarchar(100) NOT NULL CONSTRAINT PK_SystemSettings PRIMARY KEY,
    SettingValue nvarchar(500) NOT NULL
);
GO

CREATE TABLE dbo.Trainers (
    UserId int NOT NULL CONSTRAINT PK_Trainers PRIMARY KEY,
    Bio nvarchar(500) NULL,
    TotalClients int NOT NULL CONSTRAINT DF_Trainers_TotalClients DEFAULT (0),
    AverageRating decimal(3,2) NOT NULL CONSTRAINT DF_Trainers_AverageRating DEFAULT (0),
    CONSTRAINT FK_Trainers_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId),
    CONSTRAINT CK_Trainers_AverageRating CHECK (AverageRating >= 0 AND AverageRating <= 5)
);
GO

CREATE TABLE dbo.TrainerAvailability (
    TrainerId int NOT NULL CONSTRAINT PK_TrainerAvailability PRIMARY KEY,
    AvailableDays nvarchar(100) NOT NULL,
    StartTime time(0) NOT NULL,
    EndTime time(0) NOT NULL,
    WeeklyMemberLimit int NOT NULL,
    UpdatedAt datetime2(7) NOT NULL CONSTRAINT DF_TrainerAvailability_UpdatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_TrainerAvailability_Trainers FOREIGN KEY (TrainerId) REFERENCES dbo.Trainers(UserId),
    CONSTRAINT CK_TrainerAvailability_Times CHECK (EndTime > StartTime),
    CONSTRAINT CK_TrainerAvailability_Limit CHECK (WeeklyMemberLimit > 0)
);
GO

CREATE TABLE dbo.Sessions (
    SessionId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Sessions PRIMARY KEY,
    TrainerId int NOT NULL,
    SessionType nvarchar(100) NOT NULL,
    StartTime datetime2(7) NOT NULL,
    EndTime datetime2(7) NOT NULL,
    Location nvarchar(100) NULL,
    Room nvarchar(50) NULL,
    MaxCapacity int NOT NULL,
    Status nvarchar(30) NOT NULL CONSTRAINT DF_Sessions_Status DEFAULT (N'Scheduled'),
    Description nvarchar(500) NULL,
    CreatedAt datetime2(7) NOT NULL CONSTRAINT DF_Sessions_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_Sessions_Trainers FOREIGN KEY (TrainerId) REFERENCES dbo.Trainers(UserId),
    CONSTRAINT CK_Sessions_Time CHECK (EndTime > StartTime),
    CONSTRAINT CK_Sessions_Capacity CHECK (MaxCapacity > 0)
);
GO

CREATE TABLE dbo.SessionAttendees (
    AttendeeId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_SessionAttendees PRIMARY KEY,
    SessionId int NOT NULL,
    MemberId int NOT NULL,
    IsConfirmed bit NOT NULL CONSTRAINT DF_SessionAttendees_IsConfirmed DEFAULT (0),
    IsAttended bit NOT NULL CONSTRAINT DF_SessionAttendees_IsAttended DEFAULT (0),
    BookedAt datetime2(7) NOT NULL CONSTRAINT DF_SessionAttendees_BookedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_SessionAttendees_Sessions FOREIGN KEY (SessionId) REFERENCES dbo.Sessions(SessionId) ON DELETE CASCADE,
    CONSTRAINT FK_SessionAttendees_Members FOREIGN KEY (MemberId) REFERENCES dbo.Members(UserId),
    CONSTRAINT UQ_SessionAttendees_SessionMember UNIQUE (SessionId, MemberId)
);
GO

CREATE TABLE dbo.ClientPrograms (
    ProgramId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_ClientPrograms PRIMARY KEY,
    MemberId int NOT NULL,
    TrainerId int NOT NULL,
    ProgramName nvarchar(100) NOT NULL,
    Description nvarchar(500) NULL,
    StartDate date NOT NULL,
    EndDate date NOT NULL,
    SessionsAllowed int NOT NULL CONSTRAINT DF_ClientPrograms_SessionsAllowed DEFAULT (0),
    SessionsCompleted int NOT NULL CONSTRAINT DF_ClientPrograms_SessionsCompleted DEFAULT (0),
    IsActive bit NOT NULL CONSTRAINT DF_ClientPrograms_IsActive DEFAULT (1),
    CONSTRAINT FK_ClientPrograms_Members FOREIGN KEY (MemberId) REFERENCES dbo.Members(UserId),
    CONSTRAINT FK_ClientPrograms_Trainers FOREIGN KEY (TrainerId) REFERENCES dbo.Trainers(UserId),
    CONSTRAINT CK_ClientPrograms_Dates CHECK (EndDate >= StartDate),
    CONSTRAINT CK_ClientPrograms_Sessions CHECK (SessionsAllowed >= 0 AND SessionsCompleted >= 0)
);
GO

CREATE TABLE dbo.Payments (
    PaymentId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Payments PRIMARY KEY,
    MemberId int NOT NULL,
    MembershipId int NULL,
    Amount decimal(18,2) NOT NULL,
    Status nvarchar(30) NOT NULL,
    TransactionCode nvarchar(50) NULL,
    PaymentDate datetime2(7) NULL,
    DueDate datetime2(7) NULL,
    Notes nvarchar(500) NULL,
    CONSTRAINT FK_Payments_Members FOREIGN KEY (MemberId) REFERENCES dbo.Members(UserId),
    CONSTRAINT FK_Payments_Memberships FOREIGN KEY (MembershipId) REFERENCES dbo.Memberships(MembershipId),
    CONSTRAINT CK_Payments_Amount CHECK (Amount >= 0)
);
GO

CREATE TABLE dbo.MemberAttendance (
    AttendanceId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_MemberAttendance PRIMARY KEY,
    MemberId int NOT NULL,
    CheckInAt datetime2(7) NOT NULL CONSTRAINT DF_MemberAttendance_CheckInAt DEFAULT (SYSUTCDATETIME()),
    CheckOutAt datetime2(7) NULL,
    CONSTRAINT FK_MemberAttendance_Members FOREIGN KEY (MemberId) REFERENCES dbo.Members(UserId),
    CONSTRAINT CK_MemberAttendance_Times CHECK (CheckOutAt IS NULL OR CheckOutAt >= CheckInAt)
);
GO

CREATE TABLE dbo.Notifications (
    NotificationId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Notifications PRIMARY KEY,
    UserId int NOT NULL,
    Title nvarchar(200) NOT NULL,
    Message nvarchar(1000) NOT NULL,
    IsRead bit NOT NULL CONSTRAINT DF_Notifications_IsRead DEFAULT (0),
    CreatedAt datetime2(7) NOT NULL CONSTRAINT DF_Notifications_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_Notifications_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId) ON DELETE CASCADE
);
GO

CREATE TABLE dbo.WalkInPaymentTypes (
    WalkInPaymentTypeId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_WalkInPaymentTypes PRIMARY KEY,
    Name nvarchar(100) NOT NULL,
    Amount decimal(18,2) NOT NULL,
    IsActive bit NOT NULL CONSTRAINT DF_WalkInPaymentTypes_IsActive DEFAULT (1),
    CONSTRAINT UQ_WalkInPaymentTypes_Name UNIQUE (Name),
    CONSTRAINT CK_WalkInPaymentTypes_Amount CHECK (Amount > 0)
);
GO

CREATE TABLE dbo.WalkInClients (
    WalkInClientId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_WalkInClients PRIMARY KEY,
    UserId int NULL,
    MembershipId int NULL,
    WalkInPaymentTypeId int NULL,
    FirstName nvarchar(100) NOT NULL,
    LastName nvarchar(100) NOT NULL,
    ContactNumber nvarchar(20) NOT NULL,
    Email nvarchar(100) NULL,
    ReasonForVisit nvarchar(50) NOT NULL,
    AmountPaid decimal(18,2) NOT NULL,
    IssueDigitalReceipt bit NOT NULL CONSTRAINT DF_WalkInClients_IssueDigitalReceipt DEFAULT (0),
    LiabilityAccepted bit NOT NULL CONSTRAINT DF_WalkInClients_LiabilityAccepted DEFAULT (0),
    Status nvarchar(20) NOT NULL CONSTRAINT DF_WalkInClients_Status DEFAULT (N'Checked-in'),
    RegisteredAt datetime2(7) NOT NULL CONSTRAINT DF_WalkInClients_RegisteredAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_WalkInClients_Users FOREIGN KEY (UserId) REFERENCES dbo.Users(UserId),
    CONSTRAINT FK_WalkInClients_Memberships FOREIGN KEY (MembershipId) REFERENCES dbo.Memberships(MembershipId),
    CONSTRAINT FK_WalkInClients_PaymentTypes FOREIGN KEY (WalkInPaymentTypeId) REFERENCES dbo.WalkInPaymentTypes(WalkInPaymentTypeId),
    CONSTRAINT CK_WalkInClients_Amount CHECK (AmountPaid >= 0)
);
GO

CREATE TABLE dbo.EquipmentNameCatalog (
    EquipmentNameCatalogId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_EquipmentNameCatalog PRIMARY KEY,
    Name nvarchar(100) NOT NULL,
    PurchaseDate date NULL,
    GymArrivalDate date NULL,
    CreatedAt datetime2(7) NOT NULL CONSTRAINT DF_EquipmentNameCatalog_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT UQ_EquipmentNameCatalog_Name UNIQUE (Name)
);
GO

CREATE TABLE dbo.Equipment (
    EquipmentId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Equipment PRIMARY KEY,
    EquipmentNameCatalogId int NULL,
    EquipmentName nvarchar(100) NOT NULL,
    Description nvarchar(500) NULL,
    Quantity int NOT NULL CONSTRAINT DF_Equipment_Quantity DEFAULT (0),
    AvailableQuantity int NOT NULL CONSTRAINT DF_Equipment_AvailableQuantity DEFAULT (0),
    Status nvarchar(50) NOT NULL CONSTRAINT DF_Equipment_Status DEFAULT (N'Available'),
    Category nvarchar(50) NOT NULL CONSTRAINT DF_Equipment_Category DEFAULT (N'General'),
    AddedAt datetime2(7) NOT NULL CONSTRAINT DF_Equipment_AddedAt DEFAULT (SYSUTCDATETIME()),
    PurchaseDate date NULL,
    GymArrivalDate date NULL,
    LastMaintenanceDate datetime2(7) NULL,
    NextMaintenanceDate datetime2(7) NULL,
    EvidencePhotoPath nvarchar(300) NULL,
    CONSTRAINT FK_Equipment_EquipmentNameCatalog FOREIGN KEY (EquipmentNameCatalogId) REFERENCES dbo.EquipmentNameCatalog(EquipmentNameCatalogId),
    CONSTRAINT CK_Equipment_Quantities CHECK (Quantity >= 0 AND AvailableQuantity >= 0 AND AvailableQuantity <= Quantity)
);
GO

CREATE TABLE dbo.EquipmentAssets (
    EquipmentAssetId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_EquipmentAssets PRIMARY KEY,
    EquipmentId int NOT NULL,
    AssetTag nvarchar(100) NOT NULL,
    Location nvarchar(200) NULL,
    Status nvarchar(30) NOT NULL CONSTRAINT DF_EquipmentAssets_Status DEFAULT (N'Available'),
    PurchaseDate date NULL,
    CreatedAt datetime2(7) NOT NULL CONSTRAINT DF_EquipmentAssets_CreatedAt DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT UQ_EquipmentAssets_AssetTag UNIQUE (AssetTag),
    CONSTRAINT FK_EquipmentAssets_Equipment FOREIGN KEY (EquipmentId) REFERENCES dbo.Equipment(EquipmentId)
);
GO

CREATE TABLE dbo.EquipmentInspections (
    InspectionId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_EquipmentInspections PRIMARY KEY,
    EquipmentAssetId int NOT NULL,
    InspectedByStaffId int NOT NULL,
    InspectedAt datetime2(7) NOT NULL CONSTRAINT DF_EquipmentInspections_InspectedAt DEFAULT (SYSUTCDATETIME()),
    Condition nvarchar(30) NOT NULL,
    Notes nvarchar(2000) NULL,
    CONSTRAINT FK_EquipmentInspections_Assets FOREIGN KEY (EquipmentAssetId) REFERENCES dbo.EquipmentAssets(EquipmentAssetId),
    CONSTRAINT FK_EquipmentInspections_Staff FOREIGN KEY (InspectedByStaffId) REFERENCES dbo.Staff(UserId)
);
GO

CREATE TABLE dbo.EquipmentIssues (
    IssueId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_EquipmentIssues PRIMARY KEY,
    EquipmentAssetId int NOT NULL,
    ReportedByStaffId int NOT NULL,
    ReportedAt datetime2(7) NOT NULL CONSTRAINT DF_EquipmentIssues_ReportedAt DEFAULT (SYSUTCDATETIME()),
    Description nvarchar(2000) NOT NULL,
    Severity nvarchar(20) NOT NULL,
    Status nvarchar(30) NOT NULL CONSTRAINT DF_EquipmentIssues_Status DEFAULT (N'Open'),
    ResolvedAt datetime2(7) NULL,
    CONSTRAINT FK_EquipmentIssues_Assets FOREIGN KEY (EquipmentAssetId) REFERENCES dbo.EquipmentAssets(EquipmentAssetId),
    CONSTRAINT FK_EquipmentIssues_Staff FOREIGN KEY (ReportedByStaffId) REFERENCES dbo.Staff(UserId)
);
GO

CREATE TABLE dbo.EquipmentMaintenance (
    MaintenanceId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_EquipmentMaintenance PRIMARY KEY,
    EquipmentAssetId int NOT NULL,
    RecordedByStaffId int NOT NULL,
    MaintenanceType nvarchar(100) NOT NULL,
    ScheduledAt datetime2(7) NOT NULL,
    CompletedAt datetime2(7) NULL,
    Status nvarchar(30) NOT NULL CONSTRAINT DF_EquipmentMaintenance_Status DEFAULT (N'Scheduled'),
    Cost decimal(18,2) NULL,
    Notes nvarchar(2000) NULL,
    CONSTRAINT FK_EquipmentMaintenance_Assets FOREIGN KEY (EquipmentAssetId) REFERENCES dbo.EquipmentAssets(EquipmentAssetId),
    CONSTRAINT FK_EquipmentMaintenance_Staff FOREIGN KEY (RecordedByStaffId) REFERENCES dbo.Staff(UserId),
    CONSTRAINT CK_EquipmentMaintenance_Cost CHECK (Cost IS NULL OR Cost >= 0)
);
GO

CREATE INDEX IX_Sessions_TrainerId ON dbo.Sessions(TrainerId);
CREATE INDEX IX_Payments_MemberId ON dbo.Payments(MemberId);
CREATE INDEX IX_MemberAttendance_Member_CheckIn ON dbo.MemberAttendance(MemberId, CheckInAt);
CREATE INDEX IX_Notifications_UserId ON dbo.Notifications(UserId);
CREATE INDEX IX_EquipmentAssets_EquipmentId ON dbo.EquipmentAssets(EquipmentId);
CREATE INDEX IX_StaffActivityLog_Staff_OccurredAt ON dbo.StaffActivityLog(StaffId, OccurredAt);
CREATE INDEX IX_StaffAttendanceLogs_Staff_Date ON dbo.StaffAttendanceLogs(StaffId, AttendanceDate);
CREATE INDEX IX_FrontDeskEmailNotifications_Staff_CreatedAt ON dbo.FrontDeskEmailNotifications(StaffId, CreatedAt);
CREATE INDEX IX_FrontDeskEmailNotifications_DeliveryKey ON dbo.FrontDeskEmailNotifications(DeliveryKey);
GO
