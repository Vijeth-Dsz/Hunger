using Microsoft.Data.SqlClient;
using System.Data;

namespace Hunger.Api.Data;

public static class DatabaseInitializer
{
    public static async Task InitializeAsync(string connectionString, ILogger logger)
    {
        var settings = new SqlConnectionStringBuilder(connectionString);
        var databaseName = settings.InitialCatalog;
        if (string.IsNullOrWhiteSpace(databaseName)) throw new InvalidOperationException("The SQL connection must specify a database.");

        settings.InitialCatalog = "master";
        await using (var master = new SqlConnection(settings.ConnectionString))
        {
            await master.OpenAsync();
            await using var createDatabase = new SqlCommand("IF DB_ID(@Name) IS NULL BEGIN DECLARE @Sql nvarchar(max)=N'CREATE DATABASE '+QUOTENAME(@Name); EXEC(@Sql); END", master);
            createDatabase.Parameters.Add("@Name", SqlDbType.NVarChar, 128).Value = databaseName;
            await createDatabase.ExecuteNonQueryAsync();
        }

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        const string schema = """
            IF OBJECT_ID(N'dbo.Roles',N'U') IS NULL CREATE TABLE dbo.Roles (
              RoleKey nvarchar(20) NOT NULL CONSTRAINT PK_Roles PRIMARY KEY,
              DisplayName nvarchar(80) NOT NULL, Description nvarchar(200) NOT NULL,
              RegistrationAllowed bit NOT NULL, IsEnabled bit NOT NULL, SortOrder int NOT NULL);
            IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE RoleKey='Donor') INSERT dbo.Roles (RoleKey,DisplayName,Description,RegistrationAllowed,IsEnabled,SortOrder) VALUES ('Donor','Donor','Share surplus food and essential goods.',1,1,1);
            IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE RoleKey='Recipient') INSERT dbo.Roles (RoleKey,DisplayName,Description,RegistrationAllowed,IsEnabled,SortOrder) VALUES ('Recipient','Recipient','Request support for a verified organization.',1,1,2);
            IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE RoleKey='Admin') INSERT dbo.Roles (RoleKey,DisplayName,Description,RegistrationAllowed,IsEnabled,SortOrder) VALUES ('Admin','Admin','Review organizations and donation activity.',0,1,3);
            IF OBJECT_ID(N'dbo.Accounts',N'U') IS NULL CREATE TABLE dbo.Accounts (
              Id uniqueidentifier NOT NULL CONSTRAINT PK_Accounts PRIMARY KEY,
              Name nvarchar(120) NOT NULL, Email nvarchar(254) NOT NULL,
              OrganizationName nvarchar(160) NOT NULL, Role nvarchar(20) NOT NULL,
              VerificationStatus nvarchar(20) NOT NULL, PasswordSalt varbinary(16) NOT NULL,
              PasswordHash varbinary(32) NOT NULL, CreatedAt datetimeoffset(7) NOT NULL,
              CONSTRAINT UQ_Accounts_Email UNIQUE(Email));
            IF OBJECT_ID(N'dbo.VerificationDocuments',N'U') IS NULL CREATE TABLE dbo.VerificationDocuments (
              AccountId uniqueidentifier NOT NULL CONSTRAINT PK_VerificationDocuments PRIMARY KEY,
              Contents varbinary(max) NOT NULL, ContentType nvarchar(100) NOT NULL,
              CONSTRAINT FK_VerificationDocuments_Accounts FOREIGN KEY(AccountId) REFERENCES dbo.Accounts(Id) ON DELETE CASCADE);
            IF COL_LENGTH(N'dbo.Accounts',N'VerificationDocument') IS NOT NULL
              AND COL_LENGTH(N'dbo.Accounts',N'VerificationDocumentContentType') IS NOT NULL
              EXEC(N'INSERT dbo.VerificationDocuments (AccountId,Contents,ContentType)
                SELECT account.Id,account.VerificationDocument,account.VerificationDocumentContentType
                FROM dbo.Accounts account
                WHERE account.VerificationDocument IS NOT NULL AND account.VerificationDocumentContentType IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM dbo.VerificationDocuments document WHERE document.AccountId=account.Id)');
            IF OBJECT_ID(N'dbo.Donations',N'U') IS NULL CREATE TABLE dbo.Donations (
              Id uniqueidentifier NOT NULL CONSTRAINT PK_Donations PRIMARY KEY,
              CreatedBy uniqueidentifier NOT NULL, RecipientId uniqueidentifier NULL,
              Title nvarchar(160) NOT NULL, Description nvarchar(1200) NOT NULL,
              Type nvarchar(20) NOT NULL, Quantity nvarchar(80) NOT NULL, Unit nvarchar(40) NULL,
              PickupAddress nvarchar(300) NOT NULL, PickupFrom datetimeoffset(7) NOT NULL,
              PickupUntil datetimeoffset(7) NOT NULL, StorageInstructions nvarchar(500) NULL,
              Allergens nvarchar(500) NULL, ExpiresAt datetimeoffset(7) NULL,
              ReviewStatus nvarchar(20) NOT NULL CONSTRAINT DF_Donations_ReviewStatus DEFAULT 'Approved',
              ReviewNotes nvarchar(500) NULL, ReviewedBy uniqueidentifier NULL, ReviewedAt datetimeoffset(7) NULL,
              Status nvarchar(20) NOT NULL, CreatedAt datetimeoffset(7) NOT NULL,
              CONSTRAINT FK_Donations_Donor FOREIGN KEY(CreatedBy) REFERENCES dbo.Accounts(Id),
              CONSTRAINT FK_Donations_Recipient FOREIGN KEY(RecipientId) REFERENCES dbo.Accounts(Id));
            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name=N'FK_Accounts_Roles')
              ALTER TABLE dbo.Accounts ADD CONSTRAINT FK_Accounts_Roles FOREIGN KEY(Role) REFERENCES dbo.Roles(RoleKey);
            IF OBJECT_ID(N'dbo.DonationRequests',N'U') IS NULL CREATE TABLE dbo.DonationRequests (
              Id uniqueidentifier NOT NULL CONSTRAINT PK_DonationRequests PRIMARY KEY,
              DonationId uniqueidentifier NOT NULL, RecipientId uniqueidentifier NOT NULL,
              Status nvarchar(20) NOT NULL, RequestedAt datetimeoffset(7) NOT NULL,
              RespondedAt datetimeoffset(7) NULL,
              CONSTRAINT FK_DonationRequests_Donation FOREIGN KEY(DonationId) REFERENCES dbo.Donations(Id),
              CONSTRAINT FK_DonationRequests_Recipient FOREIGN KEY(RecipientId) REFERENCES dbo.Accounts(Id));
            IF OBJECT_ID(N'dbo.DonationStatusHistory',N'U') IS NULL CREATE TABLE dbo.DonationStatusHistory (
              Id uniqueidentifier NOT NULL CONSTRAINT PK_DonationStatusHistory PRIMARY KEY,
              DonationId uniqueidentifier NOT NULL, Status nvarchar(20) NOT NULL,
              ChangedBy uniqueidentifier NOT NULL, ChangedAt datetimeoffset(7) NOT NULL,
              CONSTRAINT FK_DonationHistory_Donation FOREIGN KEY(DonationId) REFERENCES dbo.Donations(Id),
              CONSTRAINT FK_DonationHistory_Account FOREIGN KEY(ChangedBy) REFERENCES dbo.Accounts(Id));
            IF COL_LENGTH(N'dbo.Donations',N'ExpiresAt') IS NULL ALTER TABLE dbo.Donations ADD ExpiresAt datetimeoffset(7) NULL;
            IF COL_LENGTH(N'dbo.Donations',N'ReviewStatus') IS NULL ALTER TABLE dbo.Donations ADD ReviewStatus nvarchar(20) NOT NULL CONSTRAINT DF_Donations_ReviewStatus DEFAULT 'Approved';
            IF COL_LENGTH(N'dbo.Donations',N'ReviewNotes') IS NULL ALTER TABLE dbo.Donations ADD ReviewNotes nvarchar(500) NULL;
            IF COL_LENGTH(N'dbo.Donations',N'ReviewedBy') IS NULL ALTER TABLE dbo.Donations ADD ReviewedBy uniqueidentifier NULL;
            IF COL_LENGTH(N'dbo.Donations',N'ReviewedAt') IS NULL ALTER TABLE dbo.Donations ADD ReviewedAt datetimeoffset(7) NULL;
            IF OBJECT_ID(N'dbo.CommunityNeeds',N'U') IS NULL CREATE TABLE dbo.CommunityNeeds (
              Id uniqueidentifier NOT NULL CONSTRAINT PK_CommunityNeeds PRIMARY KEY,
              RecipientId uniqueidentifier NOT NULL, Title nvarchar(160) NOT NULL,
              Description nvarchar(1200) NOT NULL, Type nvarchar(20) NOT NULL,
              Quantity decimal(18,2) NOT NULL, Unit nvarchar(40) NOT NULL,
              NeededBy datetimeoffset(7) NOT NULL, Status nvarchar(30) NOT NULL,
              ReviewStatus nvarchar(20) NOT NULL, ReviewNotes nvarchar(500) NULL,
              ReviewedBy uniqueidentifier NULL, ReviewedAt datetimeoffset(7) NULL,
              CreatedAt datetimeoffset(7) NOT NULL,
              CONSTRAINT FK_CommunityNeeds_Recipient FOREIGN KEY(RecipientId) REFERENCES dbo.Accounts(Id));
            IF OBJECT_ID(N'dbo.NeedContributions',N'U') IS NULL CREATE TABLE dbo.NeedContributions (
              Id uniqueidentifier NOT NULL CONSTRAINT PK_NeedContributions PRIMARY KEY,
              NeedId uniqueidentifier NOT NULL, DonorId uniqueidentifier NOT NULL,
              Quantity decimal(18,2) NOT NULL, Unit nvarchar(40) NOT NULL,
              PickupAddress nvarchar(300) NOT NULL, PickupFrom datetimeoffset(7) NOT NULL,
              PickupUntil datetimeoffset(7) NOT NULL, ExpiresAt datetimeoffset(7) NULL,
              StorageInstructions nvarchar(500) NULL,
              Allergens nvarchar(500) NULL, Status nvarchar(20) NOT NULL,
              ReviewNotes nvarchar(500) NULL, ReviewedBy uniqueidentifier NULL,
              ReviewedAt datetimeoffset(7) NULL, ReceivedAt datetimeoffset(7) NULL,
              CreatedAt datetimeoffset(7) NOT NULL,
              CONSTRAINT FK_NeedContributions_Need FOREIGN KEY(NeedId) REFERENCES dbo.CommunityNeeds(Id),
              CONSTRAINT FK_NeedContributions_Donor FOREIGN KEY(DonorId) REFERENCES dbo.Accounts(Id));
            UPDATE donation SET ReviewStatus = 'Approved'
              FROM dbo.Donations donation
              INNER JOIN dbo.Accounts donor ON donation.CreatedBy = donor.Id
              WHERE donor.VerificationStatus = 'Approved' AND donation.ReviewStatus <> 'Approved';
            UPDATE need SET ReviewStatus = 'Approved'
              FROM dbo.CommunityNeeds need
              INNER JOIN dbo.Accounts recipient ON need.RecipientId = recipient.Id
              WHERE recipient.VerificationStatus = 'Approved' AND need.ReviewStatus <> 'Approved';
            UPDATE contribution SET Status = 'Approved'
              FROM dbo.NeedContributions contribution
              INNER JOIN dbo.Accounts donor ON contribution.DonorId = donor.Id
              INNER JOIN dbo.CommunityNeeds need ON contribution.NeedId = need.Id
              INNER JOIN dbo.Accounts recipient ON need.RecipientId = recipient.Id
              WHERE donor.VerificationStatus = 'Approved'
                AND recipient.VerificationStatus = 'Approved'
                AND need.ReviewStatus = 'Approved'
                AND contribution.Status IN ('PendingReview', 'Rejected');
            """;
        await using var command = new SqlCommand(schema, connection);
        await command.ExecuteNonQueryAsync();
        logger.LogInformation("Ensured Hunger database {DatabaseName} schema exists.", databaseName);
    }
}
