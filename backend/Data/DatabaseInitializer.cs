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
            IF OBJECT_ID(N'dbo.Accounts',N'U') IS NULL CREATE TABLE dbo.Accounts (
              Id uniqueidentifier NOT NULL CONSTRAINT PK_Accounts PRIMARY KEY,
              Name nvarchar(120) NOT NULL, Email nvarchar(254) NOT NULL,
              OrganizationName nvarchar(160) NOT NULL, Role nvarchar(20) NOT NULL,
              VerificationStatus nvarchar(20) NOT NULL, PasswordSalt varbinary(16) NOT NULL,
              PasswordHash varbinary(32) NOT NULL, CreatedAt datetimeoffset(7) NOT NULL,
              CONSTRAINT UQ_Accounts_Email UNIQUE(Email));
            IF OBJECT_ID(N'dbo.Donations',N'U') IS NULL CREATE TABLE dbo.Donations (
              Id uniqueidentifier NOT NULL CONSTRAINT PK_Donations PRIMARY KEY,
              CreatedBy uniqueidentifier NOT NULL, RecipientId uniqueidentifier NULL,
              Title nvarchar(160) NOT NULL, Description nvarchar(1200) NOT NULL,
              Type nvarchar(20) NOT NULL, Quantity nvarchar(80) NOT NULL, Unit nvarchar(40) NULL,
              PickupAddress nvarchar(300) NOT NULL, PickupFrom datetimeoffset(7) NOT NULL,
              PickupUntil datetimeoffset(7) NOT NULL, StorageInstructions nvarchar(500) NULL,
              Allergens nvarchar(500) NULL, Status nvarchar(20) NOT NULL,
              CreatedAt datetimeoffset(7) NOT NULL,
              CONSTRAINT FK_Donations_Donor FOREIGN KEY(CreatedBy) REFERENCES dbo.Accounts(Id),
              CONSTRAINT FK_Donations_Recipient FOREIGN KEY(RecipientId) REFERENCES dbo.Accounts(Id));
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
            """;
        await using var command = new SqlCommand(schema, connection);
        await command.ExecuteNonQueryAsync();
        logger.LogInformation("Ensured Hunger database {DatabaseName} schema exists.", databaseName);
    }
}
