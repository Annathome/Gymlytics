IF DB_ID(N'Gym') IS NULL
    CREATE DATABASE [Gym];
GO
USE [Gym]
GO
/****** Object:  Table [dbo].[ClientPrograms]    Script Date: 29/08/2026 1:31:26 am ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[ClientPrograms](
	[ProgramId] [int] IDENTITY(1,1) NOT NULL,
	[ClientId] [int] NOT NULL,
	[TrainerId] [int] NOT NULL,
	[ProgramName] [nvarchar](100) NOT NULL,
	[Description] [nvarchar](500) NULL,
	[StartDate] [datetime2](7) NOT NULL,
	[EndDate] [datetime2](7) NOT NULL,
	[SessionsAllowed] [int] NOT NULL,
	[SessionsCompleted] [int] NOT NULL,
	[SessionsRemaining] [int] NOT NULL,
	[NextSessionDate] [datetime2](7) NULL,
	[IsActive] [bit] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[ProgramId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[EquipmentNameCatalog]    Script Date: 29/08/2026 1:31:26 am ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[EquipmentNameCatalog](
	[EquipmentNameCatalogId] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](100) NOT NULL,
	[CreatedAt] [datetime2](7) NOT NULL,
	[PurchaseDate] [date] NOT NULL,
	[GymArrivalDate] [date] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[EquipmentNameCatalogId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_EquipmentNameCatalog_Name] UNIQUE NONCLUSTERED 
(
	[Name] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Equipments]    Script Date: 29/08/2026 1:31:26 am ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Equipments](
	[EquipmentId] [int] IDENTITY(1,1) NOT NULL,
	[EquipmentName] [nvarchar](100) NOT NULL,
	[Description] [nvarchar](500) NULL,
	[Quantity] [int] NOT NULL,
	[AvailableQuantity] [int] NOT NULL,
	[Status] [nvarchar](max) NOT NULL,
	[LastMaintenanceDate] [datetime2](7) NULL,
	[NextMaintenanceDate] [datetime2](7) NULL,
	[Category] [nvarchar](50) NOT NULL,
	[AddedDate] [datetime2](7) NOT NULL,
	[EvidencePhotoPath] [nvarchar](300) NULL,
	[PurchaseDate] [date] NULL,
	[GymArrivalDate] [date] NULL,
PRIMARY KEY CLUSTERED 
(
	[EquipmentId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[FrontDeskEmailNotifications]    Script Date: 29/08/2026 1:31:26 am ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[FrontDeskEmailNotifications](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[StaffId] [int] NOT NULL,
	[Title] [nvarchar](200) NOT NULL,
	[Message] [nvarchar](1000) NOT NULL,
	[Status] [nvarchar](20) NOT NULL,
	[EmailType] [nvarchar](80) NOT NULL,
	[DeliveryKey] [nvarchar](200) NOT NULL,
	[CreatedDate] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[MemberAttendanceLogs]    Script Date: 29/08/2026 1:31:26 am ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[MemberAttendanceLogs](
	[AttendanceLogId] [int] IDENTITY(1,1) NOT NULL,
	[ClientId] [int] NOT NULL,
	[CheckInAt] [datetime2](7) NOT NULL,
	[CheckOutAt] [datetime2](7) NULL,
PRIMARY KEY CLUSTERED 
(
	[AttendanceLogId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[MembershipPlanSettings]    Script Date: 29/08/2026 1:31:26 am ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[MembershipPlanSettings](
	[Plan] [int] NOT NULL,
	[Price] [decimal](18, 2) NOT NULL,
	[UpdatedAt] [datetime] NOT NULL,
	[Name] [nvarchar](100) NOT NULL,
	[DurationMonths] [int] NOT NULL,
	[IsActive] [bit] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[Plan] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Notifications]    Script Date: 29/08/2026 1:31:26 am ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Notifications](
	[NotificationId] [int] IDENTITY(1,1) NOT NULL,
	[UserId] [int] NOT NULL,
	[Title] [nvarchar](200) NOT NULL,
	[Message] [nvarchar](1000) NOT NULL,
	[NotificationType] [nvarchar](max) NULL,
	[IsRead] [bit] NOT NULL,
	[CreatedDate] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[NotificationId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Payments]    Script Date: 29/08/2026 1:31:26 am ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Payments](
	[PaymentId] [int] IDENTITY(1,1) NOT NULL,
	[ClientId] [int] NOT NULL,
	[Amount] [decimal](18, 2) NOT NULL,
	[Status] [int] NOT NULL,
	[TransactionCode] [nvarchar](50) NULL,
	[PaymentDate] [datetime2](7) NULL,
	[DueDate] [datetime2](7) NULL,
	[Notes] [nvarchar](500) NULL,
PRIMARY KEY CLUSTERED 
(
	[PaymentId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[SessionAttendees]    Script Date: 29/08/2026 1:31:26 am ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[SessionAttendees](
	[AttendeeId] [int] IDENTITY(1,1) NOT NULL,
	[SessionId] [int] NOT NULL,
	[ClientId] [int] NOT NULL,
	[IsConfirmed] [bit] NOT NULL,
	[IsAttended] [bit] NOT NULL,
	[BookedDate] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[AttendeeId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Sessions]    Script Date: 29/08/2026 1:31:26 am ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Sessions](
	[SessionId] [int] IDENTITY(1,1) NOT NULL,
	[TrainerId] [int] NOT NULL,
	[SessionType] [nvarchar](100) NOT NULL,
	[StartTime] [datetime2](7) NOT NULL,
	[EndTime] [datetime2](7) NOT NULL,
	[Location] [nvarchar](100) NULL,
	[Room] [nvarchar](50) NULL,
	[MaxCapacity] [int] NOT NULL,
	[CurrentCapacity] [int] NOT NULL,
	[Status] [int] NOT NULL,
	[Description] [nvarchar](500) NULL,
	[CreatedDate] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[SessionId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Staff]    Script Date: 29/08/2026 1:31:26 am ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Staff](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[FullName] [nvarchar](100) NULL,
	[Username] [nvarchar](50) NULL,
	[PasswordHash] [nvarchar](255) NULL,
	[PhoneNumber] [nvarchar](20) NOT NULL,
	[Email] [nvarchar](100) NOT NULL,
	[Birthday] [datetime] NULL,
	[Sex] [nvarchar](10) NULL,
	[Address] [nvarchar](100) NULL,
	[Role] [nvarchar](20) NOT NULL,
	[Status] [nvarchar](20) NOT NULL,
	[LastLoginAt] [datetime] NULL,
	[CreatedAt] [datetime] NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED 
(
	[Username] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
UNIQUE NONCLUSTERED 
(
	[Email] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[StaffActivityLog]    Script Date: 29/08/2026 1:31:27 am ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[StaffActivityLog](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[StaffId] [int] NOT NULL,
	[ActionType] [nvarchar](20) NOT NULL,
	[OccurredAt] [datetime] NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[StaffAttendanceLogs]    Script Date: 29/08/2026 1:31:27 am ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[StaffAttendanceLogs](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[StaffId] [int] NOT NULL,
	[AttendanceDate] [date] NOT NULL,
	[AttendanceStatus] [nvarchar](20) NOT NULL,
	[CheckedInAt] [datetime] NULL,
	[MarkedAbsentAt] [datetime] NULL,
	[MarkedAbsentByStaffId] [int] NULL,
PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY],
 CONSTRAINT [UQ_StaffAttendanceLogs_StaffDate] UNIQUE NONCLUSTERED 
(
	[StaffId] ASC,
	[AttendanceDate] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[SystemSettings]    Script Date: 29/08/2026 1:31:27 am ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[SystemSettings](
	[SettingKey] [varchar](50) NOT NULL,
	[SettingValue] [varchar](50) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[SettingKey] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[TrainerAvailabilities]    Script Date: 29/08/2026 1:31:27 am ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[TrainerAvailabilities](
	[TrainerId] [int] NOT NULL,
	[AvailableDays] [nvarchar](100) NOT NULL,
	[StartTime] [time](7) NOT NULL,
	[EndTime] [time](7) NOT NULL,
	[WeeklyMemberLimit] [int] NOT NULL,
	[UpdatedAt] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[TrainerId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Users]    Script Date: 29/08/2026 1:31:27 am ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Users](
	[UserId] [int] IDENTITY(1,1) NOT NULL,
	[FirstName] [nvarchar](100) NOT NULL,
	[LastName] [nvarchar](100) NOT NULL,
	[Email] [nvarchar](100) NOT NULL,
	[PhoneNumber] [nvarchar](20) NOT NULL,
	[Role] [int] NOT NULL,
	[ProfileImageUrl] [nvarchar](max) NULL,
	[Specialization] [nvarchar](500) NULL,
	[IsActive] [bit] NOT NULL,
	[CreatedDate] [datetime2](7) NOT NULL,
	[UserType] [nvarchar](max) NOT NULL,
	[MembershipNumber] [nvarchar](max) NULL,
	[MembershipPlan] [int] NULL,
	[MembershipStartDate] [datetime2](7) NULL,
	[MembershipEndDate] [datetime2](7) NULL,
	[IsActiveMember] [bit] NULL,
	[Status] [int] NULL,
	[AssignedTrainerId] [int] NULL,
	[PreferredDays] [nvarchar](500) NULL,
	[PreferredTimeSlot] [nvarchar](100) NULL,
	[Notes] [nvarchar](500) NULL,
	[Bio] [nvarchar](500) NULL,
	[TotalClients] [int] NULL,
	[AverageRating] [decimal](3, 2) NULL,
	[GymGoal] [int] NULL,
	[FollowUpDate] [datetime2](7) NULL,
	[FollowUpPurpose] [nvarchar](100) NULL,
PRIMARY KEY CLUSTERED 
(
	[UserId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]
GO
/****** Object:  Table [dbo].[WalkInClients]    Script Date: 29/08/2026 1:31:27 am ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[WalkInClients](
	[WalkInClientId] [int] IDENTITY(1,1) NOT NULL,
	[PaymentTypeName] [nvarchar](100) NULL,
	[FirstName] [nvarchar](100) NOT NULL,
	[LastName] [nvarchar](100) NOT NULL,
	[ContactNumber] [nvarchar](20) NOT NULL,
	[Email] [nvarchar](100) NULL,
	[ReasonForVisit] [nvarchar](50) NOT NULL,
	[PaymentMethod] [nvarchar](30) NOT NULL,
	[AmountPaid] [decimal](18, 2) NOT NULL,
	[IssueDigitalReceipt] [bit] NOT NULL,
	[LiabilityAccepted] [bit] NOT NULL,
	[Status] [nvarchar](20) NOT NULL,
	[RegisteredAt] [datetime2](7) NOT NULL,
PRIMARY KEY CLUSTERED 
(
	[WalkInClientId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
CREATE TABLE [dbo].[WalkInPaymentTypes](
	[Id] [int] IDENTITY(1,1) NOT NULL,
	[Name] [nvarchar](100) NOT NULL,
	[Amount] [decimal](18, 2) NOT NULL,
	[IsActive] [bit] NOT NULL CONSTRAINT [DF_WalkInPaymentTypes_IsActive] DEFAULT ((1)),
PRIMARY KEY CLUSTERED ([Id] ASC),
 CONSTRAINT [CK_WalkInPaymentTypes_Amount] CHECK ([Amount] > 0 AND [Amount] <= 1000000),
 CONSTRAINT [UQ_WalkInPaymentTypes_Name] UNIQUE NONCLUSTERED ([Name] ASC)
)
GO
/****** Object:  Index [IX_ClientPrograms_ClientId]    Script Date: 29/08/2026 1:31:27 am ******/
CREATE NONCLUSTERED INDEX [IX_ClientPrograms_ClientId] ON [dbo].[ClientPrograms]
(
	[ClientId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_ClientPrograms_TrainerId]    Script Date: 29/08/2026 1:31:27 am ******/
CREATE NONCLUSTERED INDEX [IX_ClientPrograms_TrainerId] ON [dbo].[ClientPrograms]
(
	[TrainerId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [IX_FrontDeskEmailNotifications_DeliveryKey]    Script Date: 29/08/2026 1:31:27 am ******/
CREATE NONCLUSTERED INDEX [IX_FrontDeskEmailNotifications_DeliveryKey] ON [dbo].[FrontDeskEmailNotifications]
(
	[DeliveryKey] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_FrontDeskEmailNotifications_StaffId_CreatedDate]    Script Date: 29/08/2026 1:31:27 am ******/
CREATE NONCLUSTERED INDEX [IX_FrontDeskEmailNotifications_StaffId_CreatedDate] ON [dbo].[FrontDeskEmailNotifications]
(
	[StaffId] ASC,
	[CreatedDate] DESC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_MemberAttendanceLogs_ClientId_CheckInAt]    Script Date: 29/08/2026 1:31:27 am ******/
CREATE NONCLUSTERED INDEX [IX_MemberAttendanceLogs_ClientId_CheckInAt] ON [dbo].[MemberAttendanceLogs]
(
	[ClientId] ASC,
	[CheckInAt] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Notifications_UserId]    Script Date: 29/08/2026 1:31:27 am ******/
CREATE NONCLUSTERED INDEX [IX_Notifications_UserId] ON [dbo].[Notifications]
(
	[UserId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Payments_ClientId]    Script Date: 29/08/2026 1:31:27 am ******/
CREATE NONCLUSTERED INDEX [IX_Payments_ClientId] ON [dbo].[Payments]
(
	[ClientId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_SessionAttendees_ClientId]    Script Date: 29/08/2026 1:31:27 am ******/
CREATE NONCLUSTERED INDEX [IX_SessionAttendees_ClientId] ON [dbo].[SessionAttendees]
(
	[ClientId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_SessionAttendees_SessionId]    Script Date: 29/08/2026 1:31:27 am ******/
CREATE NONCLUSTERED INDEX [IX_SessionAttendees_SessionId] ON [dbo].[SessionAttendees]
(
	[SessionId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Sessions_TrainerId]    Script Date: 29/08/2026 1:31:27 am ******/
CREATE NONCLUSTERED INDEX [IX_Sessions_TrainerId] ON [dbo].[Sessions]
(
	[TrainerId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_StaffActivityLog_StaffId]    Script Date: 29/08/2026 1:31:27 am ******/
CREATE NONCLUSTERED INDEX [IX_StaffActivityLog_StaffId] ON [dbo].[StaffActivityLog]
(
	[StaffId] ASC,
	[OccurredAt] DESC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_Users_AssignedTrainerId]    Script Date: 29/08/2026 1:31:27 am ******/
CREATE NONCLUSTERED INDEX [IX_Users_AssignedTrainerId] ON [dbo].[Users]
(
	[AssignedTrainerId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
/****** Object:  Index [IX_WalkInClients_RegisteredAt]    Script Date: 29/08/2026 1:31:27 am ******/
CREATE NONCLUSTERED INDEX [IX_WalkInClients_RegisteredAt] ON [dbo].[WalkInClients]
(
	[RegisteredAt] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
ALTER TABLE [dbo].[ClientPrograms] ADD  DEFAULT ((0)) FOR [SessionsAllowed]
GO
ALTER TABLE [dbo].[ClientPrograms] ADD  DEFAULT ((0)) FOR [SessionsCompleted]
GO
ALTER TABLE [dbo].[ClientPrograms] ADD  DEFAULT ((0)) FOR [SessionsRemaining]
GO
ALTER TABLE [dbo].[ClientPrograms] ADD  DEFAULT ((1)) FOR [IsActive]
GO
ALTER TABLE [dbo].[EquipmentNameCatalog] ADD  DEFAULT (getdate()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[EquipmentNameCatalog] ADD  CONSTRAINT [DF_EquipmentNameCatalog_PurchaseDate]  DEFAULT (CONVERT([date],getdate())) FOR [PurchaseDate]
GO
ALTER TABLE [dbo].[EquipmentNameCatalog] ADD  CONSTRAINT [DF_EquipmentNameCatalog_GymArrivalDate]  DEFAULT (CONVERT([date],getdate())) FOR [GymArrivalDate]
GO
ALTER TABLE [dbo].[Equipments] ADD  DEFAULT ((0)) FOR [Quantity]
GO
ALTER TABLE [dbo].[Equipments] ADD  DEFAULT ((0)) FOR [AvailableQuantity]
GO
ALTER TABLE [dbo].[Equipments] ADD  DEFAULT ('Available') FOR [Status]
GO
ALTER TABLE [dbo].[Equipments] ADD  CONSTRAINT [DF_Equipments_Category]  DEFAULT (N'General') FOR [Category]
GO
ALTER TABLE [dbo].[Equipments] ADD  CONSTRAINT [DF_Equipments_AddedDate]  DEFAULT (getdate()) FOR [AddedDate]
GO
ALTER TABLE [dbo].[FrontDeskEmailNotifications] ADD  DEFAULT (getdate()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[MembershipPlanSettings] ADD  DEFAULT (getdate()) FOR [UpdatedAt]
GO
ALTER TABLE [dbo].[MembershipPlanSettings] ADD  CONSTRAINT [DF_MembershipPlanSettings_IsActive]  DEFAULT ((1)) FOR [IsActive]
GO
ALTER TABLE [dbo].[Notifications] ADD  DEFAULT ((0)) FOR [IsRead]
GO
ALTER TABLE [dbo].[Notifications] ADD  DEFAULT (getdate()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[SessionAttendees] ADD  DEFAULT ((0)) FOR [IsConfirmed]
GO
ALTER TABLE [dbo].[SessionAttendees] ADD  DEFAULT ((0)) FOR [IsAttended]
GO
ALTER TABLE [dbo].[SessionAttendees] ADD  DEFAULT (getdate()) FOR [BookedDate]
GO
ALTER TABLE [dbo].[Sessions] ADD  DEFAULT ((0)) FOR [CurrentCapacity]
GO
ALTER TABLE [dbo].[Sessions] ADD  DEFAULT (getdate()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[Staff] ADD  DEFAULT ('Pending') FOR [Status]
GO
ALTER TABLE [dbo].[Staff] ADD  DEFAULT (getdate()) FOR [CreatedAt]
GO
ALTER TABLE [dbo].[StaffActivityLog] ADD  DEFAULT (getdate()) FOR [OccurredAt]
GO
ALTER TABLE [dbo].[TrainerAvailabilities] ADD  DEFAULT (getdate()) FOR [UpdatedAt]
GO
ALTER TABLE [dbo].[Users] ADD  DEFAULT ((1)) FOR [IsActive]
GO
ALTER TABLE [dbo].[Users] ADD  DEFAULT (getdate()) FOR [CreatedDate]
GO
ALTER TABLE [dbo].[WalkInClients] ADD  DEFAULT (getdate()) FOR [RegisteredAt]
GO
ALTER TABLE [dbo].[ClientPrograms]  WITH CHECK ADD  CONSTRAINT [FK_ClientPrograms_Client] FOREIGN KEY([ClientId])
REFERENCES [dbo].[Users] ([UserId])
GO
ALTER TABLE [dbo].[ClientPrograms] CHECK CONSTRAINT [FK_ClientPrograms_Client]
GO
ALTER TABLE [dbo].[ClientPrograms]  WITH CHECK ADD  CONSTRAINT [FK_ClientPrograms_Trainer] FOREIGN KEY([TrainerId])
REFERENCES [dbo].[Users] ([UserId])
GO
ALTER TABLE [dbo].[ClientPrograms] CHECK CONSTRAINT [FK_ClientPrograms_Trainer]
GO
ALTER TABLE [dbo].[Notifications]  WITH CHECK ADD  CONSTRAINT [FK_Notifications_User] FOREIGN KEY([UserId])
REFERENCES [dbo].[Users] ([UserId])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[Notifications] CHECK CONSTRAINT [FK_Notifications_User]
GO
ALTER TABLE [dbo].[Payments]  WITH CHECK ADD  CONSTRAINT [FK_Payments_Client] FOREIGN KEY([ClientId])
REFERENCES [dbo].[Users] ([UserId])
GO
ALTER TABLE [dbo].[Payments] CHECK CONSTRAINT [FK_Payments_Client]
GO
ALTER TABLE [dbo].[SessionAttendees]  WITH CHECK ADD  CONSTRAINT [FK_SessionAttendees_Client] FOREIGN KEY([ClientId])
REFERENCES [dbo].[Users] ([UserId])
GO
ALTER TABLE [dbo].[SessionAttendees] CHECK CONSTRAINT [FK_SessionAttendees_Client]
GO
ALTER TABLE [dbo].[SessionAttendees]  WITH CHECK ADD  CONSTRAINT [FK_SessionAttendees_Session] FOREIGN KEY([SessionId])
REFERENCES [dbo].[Sessions] ([SessionId])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[SessionAttendees] CHECK CONSTRAINT [FK_SessionAttendees_Session]
GO
ALTER TABLE [dbo].[Sessions]  WITH CHECK ADD  CONSTRAINT [FK_Sessions_Trainer] FOREIGN KEY([TrainerId])
REFERENCES [dbo].[Users] ([UserId])
GO
ALTER TABLE [dbo].[Sessions] CHECK CONSTRAINT [FK_Sessions_Trainer]
GO
ALTER TABLE [dbo].[StaffActivityLog]  WITH CHECK ADD  CONSTRAINT [FK_StaffActivityLog_Staff] FOREIGN KEY([StaffId])
REFERENCES [dbo].[Staff] ([Id])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[StaffActivityLog] CHECK CONSTRAINT [FK_StaffActivityLog_Staff]
GO
ALTER TABLE [dbo].[StaffAttendanceLogs]  WITH CHECK ADD  CONSTRAINT [FK_StaffAttendanceLogs_Staff] FOREIGN KEY([StaffId])
REFERENCES [dbo].[Staff] ([Id])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[StaffAttendanceLogs] CHECK CONSTRAINT [FK_StaffAttendanceLogs_Staff]
GO
ALTER TABLE [dbo].[TrainerAvailabilities]  WITH CHECK ADD  CONSTRAINT [FK_TrainerAvailabilities_Trainer] FOREIGN KEY([TrainerId])
REFERENCES [dbo].[Users] ([UserId])
ON DELETE CASCADE
GO
ALTER TABLE [dbo].[TrainerAvailabilities] CHECK CONSTRAINT [FK_TrainerAvailabilities_Trainer]
GO
ALTER TABLE [dbo].[Users]  WITH CHECK ADD  CONSTRAINT [FK_Users_AssignedTrainer] FOREIGN KEY([AssignedTrainerId])
REFERENCES [dbo].[Users] ([UserId])
GO
ALTER TABLE [dbo].[Users] CHECK CONSTRAINT [FK_Users_AssignedTrainer]
GO
ALTER TABLE [dbo].[MembershipPlanSettings]  WITH CHECK ADD  CONSTRAINT [CK_MembershipPlanSettings_Price] CHECK  (([Price]>(0)))
GO
ALTER TABLE [dbo].[MembershipPlanSettings] CHECK CONSTRAINT [CK_MembershipPlanSettings_Price]
GO
ALTER TABLE [dbo].[Staff]  WITH CHECK ADD  CONSTRAINT [CK_Staff_Role] CHECK  (([Role]='Staff' OR [Role]='Trainer' OR [Role]='Admin' OR [Role]='MainAdmin'))
GO
ALTER TABLE [dbo].[Staff] CHECK CONSTRAINT [CK_Staff_Role]
GO
ALTER TABLE [dbo].[Staff]  WITH CHECK ADD  CONSTRAINT [CK_Staff_Status] CHECK  (([Status]='Suspended' OR [Status]='Active' OR [Status]='Pending'))
GO
ALTER TABLE [dbo].[Staff] CHECK CONSTRAINT [CK_Staff_Status]
GO
ALTER TABLE [dbo].[StaffAttendanceLogs]  WITH CHECK ADD  CONSTRAINT [CK_StaffAttendanceLogs_Status] CHECK  (([AttendanceStatus]='Absent' OR [AttendanceStatus]='Present'))
GO
ALTER TABLE [dbo].[StaffAttendanceLogs] CHECK CONSTRAINT [CK_StaffAttendanceLogs_Status]
GO
/****** Object:  StoredProcedure [dbo].[usp_RegisterMainAdmin]    Script Date: 29/08/2026 1:31:27 am ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- 4. Stored Procedure: Register Main Admin
-- ============================================================
CREATE   PROCEDURE [dbo].[usp_RegisterMainAdmin]
    @FullName NVARCHAR(100),
    @Username NVARCHAR(50),
    @PasswordHash NVARCHAR(255),
    @PhoneNumber NVARCHAR(20),
    @Email NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM Staff WHERE Role IN ('Admin', 'MainAdmin'))
       OR EXISTS (SELECT 1 FROM SystemSettings WHERE SettingKey = 'IsSetupCompleted' AND SettingValue = 'true')
    BEGIN
        RAISERROR ('System Setup Completed: Main Admin account has already been registered.', 16, 1);
        RETURN;
    END

    IF EXISTS (SELECT 1 FROM Staff WHERE Username = @Username OR Email = @Email)
    BEGIN
        RAISERROR ('Username or Email address is already in use.', 16, 1);
        RETURN;
    END

    BEGIN TRANSACTION;
    BEGIN TRY
        INSERT INTO Staff (FullName, Username, PasswordHash, PhoneNumber, Email, Role, Status, CreatedAt)
        VALUES (@FullName, @Username, @PasswordHash, @PhoneNumber, @Email, 'MainAdmin', 'Active', GETDATE());

        UPDATE SystemSettings
        SET SettingValue = 'true'
        WHERE SettingKey = 'IsSetupCompleted';

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO
-- Equipment tracking flow: equipment -> physical assets -> inspections, issues, maintenance.
CREATE TABLE dbo.EquipmentAssets (
    EquipmentAssetId int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    EquipmentId int NOT NULL,
    AssetTag nvarchar(100) NOT NULL,
    Location nvarchar(200) NULL,
    Status nvarchar(30) NOT NULL CONSTRAINT DF_EquipmentAssets_Status DEFAULT ('Available'),
    PurchaseDate date NULL,
    CreatedAt datetime2 NOT NULL CONSTRAINT DF_EquipmentAssets_CreatedAt DEFAULT (sysutcdatetime()),
    CONSTRAINT UQ_EquipmentAssets_AssetTag UNIQUE (AssetTag),
    CONSTRAINT FK_EquipmentAssets_Equipments FOREIGN KEY (EquipmentId) REFERENCES dbo.Equipments(EquipmentId)
);
GO
CREATE INDEX IX_EquipmentAssets_EquipmentId ON dbo.EquipmentAssets(EquipmentId);
GO
CREATE TABLE dbo.EquipmentInspections (
    InspectionId int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    EquipmentAssetId int NOT NULL,
    InspectedByStaffId int NOT NULL,
    InspectedAt datetime2 NOT NULL CONSTRAINT DF_EquipmentInspections_InspectedAt DEFAULT (sysutcdatetime()),
    [Condition] nvarchar(30) NOT NULL,
    Notes nvarchar(2000) NULL,
    CONSTRAINT FK_EquipmentInspections_Asset FOREIGN KEY (EquipmentAssetId) REFERENCES dbo.EquipmentAssets(EquipmentAssetId),
    CONSTRAINT FK_EquipmentInspections_Staff FOREIGN KEY (InspectedByStaffId) REFERENCES dbo.Staff(Id)
);
GO
CREATE INDEX IX_EquipmentInspections_AssetDate ON dbo.EquipmentInspections(EquipmentAssetId, InspectedAt);
GO
CREATE TABLE dbo.EquipmentIssues (
    IssueId int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    EquipmentAssetId int NOT NULL,
    ReportedByStaffId int NOT NULL,
    ReportedAt datetime2 NOT NULL CONSTRAINT DF_EquipmentIssues_ReportedAt DEFAULT (sysutcdatetime()),
    Description nvarchar(2000) NOT NULL,
    Severity nvarchar(20) NOT NULL,
    Status nvarchar(30) NOT NULL CONSTRAINT DF_EquipmentIssues_Status DEFAULT ('Open'),
    ResolvedAt datetime2 NULL,
    CONSTRAINT FK_EquipmentIssues_Asset FOREIGN KEY (EquipmentAssetId) REFERENCES dbo.EquipmentAssets(EquipmentAssetId),
    CONSTRAINT FK_EquipmentIssues_Staff FOREIGN KEY (ReportedByStaffId) REFERENCES dbo.Staff(Id)
);
GO
CREATE INDEX IX_EquipmentIssues_AssetStatus ON dbo.EquipmentIssues(EquipmentAssetId, Status);
GO
CREATE TABLE dbo.EquipmentMaintenance (
    MaintenanceId int IDENTITY(1,1) NOT NULL PRIMARY KEY,
    EquipmentAssetId int NOT NULL,
    RecordedByStaffId int NOT NULL,
    MaintenanceType nvarchar(100) NOT NULL,
    ScheduledAt datetime2 NOT NULL,
    CompletedAt datetime2 NULL,
    Status nvarchar(30) NOT NULL CONSTRAINT DF_EquipmentMaintenance_Status DEFAULT ('Scheduled'),
    Cost decimal(18,2) NULL,
    Notes nvarchar(2000) NULL,
    CONSTRAINT FK_EquipmentMaintenance_Asset FOREIGN KEY (EquipmentAssetId) REFERENCES dbo.EquipmentAssets(EquipmentAssetId),
    CONSTRAINT FK_EquipmentMaintenance_Staff FOREIGN KEY (RecordedByStaffId) REFERENCES dbo.Staff(Id)
);
GO
CREATE INDEX IX_EquipmentMaintenance_AssetSchedule ON dbo.EquipmentMaintenance(EquipmentAssetId, ScheduledAt);
GO

