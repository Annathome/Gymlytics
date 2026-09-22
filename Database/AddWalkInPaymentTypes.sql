IF OBJECT_ID(N'dbo.WalkInPaymentTypes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.WalkInPaymentTypes (
        Id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
        Name nvarchar(100) NOT NULL,
        Amount decimal(18,2) NOT NULL CHECK (Amount > 0 AND Amount <= 1000000),
        IsActive bit NOT NULL DEFAULT 1
    );
    CREATE UNIQUE INDEX UX_WalkInPaymentTypes_Name ON dbo.WalkInPaymentTypes(Name);
END;
IF COL_LENGTH('dbo.WalkInClients', 'PaymentTypeName') IS NULL
    ALTER TABLE dbo.WalkInClients ADD PaymentTypeName nvarchar(100) NULL;
