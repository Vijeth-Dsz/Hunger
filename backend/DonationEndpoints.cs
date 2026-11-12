using Hunger.Api.Models;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Security.Claims;

namespace Hunger.Api;

public static class DonationEndpoints
{
    public static void Map(WebApplication app, string connectionString)
    {
        app.MapGet("/api/dashboard", async (ClaimsPrincipal user, CancellationToken ct) =>
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(ct);
            if (user.IsInRole("Admin"))
            {
                await using var admin = new SqlCommand("SELECT (SELECT COUNT(*) FROM dbo.Accounts WHERE Role='Recipient' AND VerificationStatus='Pending'),(SELECT COUNT(*) FROM dbo.Donations WHERE Status NOT IN ('Delivered','Cancelled')),(SELECT COUNT(*) FROM dbo.Donations WHERE Status='Delivered')", connection);
                await using var reader = await admin.ExecuteReaderAsync(ct);
                await reader.ReadAsync(ct);
                return Results.Ok(new DashboardSummary(reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2)));
            }
            await using var command = new SqlCommand("SELECT COUNT(DISTINCT d.Id),COUNT(DISTINCT CASE WHEN d.Status NOT IN ('Delivered','Cancelled') THEN d.Id END),COUNT(DISTINCT CASE WHEN d.Status='Delivered' THEN d.Id END) FROM dbo.Donations d LEFT JOIN dbo.DonationRequests r ON r.DonationId=d.Id WHERE d.CreatedBy=@UserId OR d.RecipientId=@UserId OR r.RecipientId=@UserId", connection);
            command.Parameters.Add("@UserId", SqlDbType.UniqueIdentifier).Value = UserId(user);
            await using var result = await command.ExecuteReaderAsync(ct);
            await result.ReadAsync(ct);
            return Results.Ok(new DashboardSummary(result.GetInt32(0), result.IsDBNull(1) ? 0 : result.GetInt32(1), result.IsDBNull(2) ? 0 : result.GetInt32(2)));
        }).RequireAuthorization();

        app.MapGet("/api/donations/{id:guid}", async (Guid id, ClaimsPrincipal user, CancellationToken ct) =>
        {
            var where = user.IsInRole("Donor") ? "WHERE d.CreatedBy=@UserId AND d.Id=@Id" : user.IsInRole("Recipient") ? "WHERE (d.Status='Available' OR d.RecipientId=@UserId OR EXISTS(SELECT 1 FROM dbo.DonationRequests mine WHERE mine.DonationId=d.Id AND mine.RecipientId=@UserId)) AND a.VerificationStatus='Approved' AND d.Id=@Id" : "WHERE d.Id=@Id";
            var address = user.IsInRole("Recipient") ? "CASE WHEN d.Status IN ('Accepted','PickedUp','Delivered') THEN d.PickupAddress ELSE N'Available after request acceptance' END" : "d.PickupAddress";
            var sql = $"SELECT d.Id,d.CreatedBy,d.RecipientId,d.Title,d.Description,d.Type,d.Quantity,d.Unit,{address},d.PickupFrom,d.PickupUntil,d.StorageInstructions,d.Allergens,d.Status,d.CreatedAt,a.OrganizationName,COALESCE(r.OrganizationName,N'') FROM dbo.Donations d JOIN dbo.Accounts a ON a.Id=d.CreatedBy LEFT JOIN dbo.Accounts r ON r.Id=COALESCE(d.RecipientId,(SELECT TOP(1) req.RecipientId FROM dbo.DonationRequests req WHERE req.DonationId=d.Id AND req.Status='Pending' ORDER BY req.RequestedAt)) {where}";
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(ct);
            await using var command = new SqlCommand(sql, connection);
            command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id;
            if (!user.IsInRole("Admin")) command.Parameters.Add("@UserId", SqlDbType.UniqueIdentifier).Value = UserId(user);
            await using var reader = await command.ExecuteReaderAsync(ct);
            return await reader.ReadAsync(ct) ? Results.Ok(ReadDonation(reader)) : Results.NotFound(new { error = "Donation not found." });
        }).RequireAuthorization();

        app.MapGet("/api/donations", async (ClaimsPrincipal user, CancellationToken ct) =>
        {
            var where = user.IsInRole("Donor") ? "WHERE d.CreatedBy=@UserId" : user.IsInRole("Recipient") ? "WHERE (d.Status='Available' OR d.RecipientId=@UserId OR EXISTS(SELECT 1 FROM dbo.DonationRequests mine WHERE mine.DonationId=d.Id AND mine.RecipientId=@UserId)) AND a.VerificationStatus='Approved'" : "";
            var address = user.IsInRole("Recipient") ? "CASE WHEN d.Status IN ('Accepted','PickedUp','Delivered') THEN d.PickupAddress ELSE N'Available after request acceptance' END" : "d.PickupAddress";
            var sql = $"SELECT d.Id,d.CreatedBy,d.RecipientId,d.Title,d.Description,d.Type,d.Quantity,d.Unit,{address},d.PickupFrom,d.PickupUntil,d.StorageInstructions,d.Allergens,d.Status,d.CreatedAt,a.OrganizationName,COALESCE(r.OrganizationName,N'') FROM dbo.Donations d JOIN dbo.Accounts a ON a.Id=d.CreatedBy LEFT JOIN dbo.Accounts r ON r.Id=COALESCE(d.RecipientId,(SELECT TOP(1) req.RecipientId FROM dbo.DonationRequests req WHERE req.DonationId=d.Id AND req.Status='Pending' ORDER BY req.RequestedAt)) {where} ORDER BY d.CreatedAt DESC";
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(ct);
            await using var command = new SqlCommand(sql, connection);
            if (!user.IsInRole("Admin")) command.Parameters.Add("@UserId", SqlDbType.UniqueIdentifier).Value = UserId(user);
            await using var reader = await command.ExecuteReaderAsync(ct);
            var donations = new List<DonationView>();
            while (await reader.ReadAsync(ct)) donations.Add(ReadDonation(reader));
            return Results.Ok(donations);
        }).RequireAuthorization();

        app.MapPost("/api/donations", async (CreateDonationRequest? body, ClaimsPrincipal user, CancellationToken ct) =>
        {
            if (!user.IsInRole("Donor")) return Results.Forbid();
            if (!IsValidDonation(body, out var type))
                return Results.BadRequest(new { error = "Enter a title, description, donation type, quantity, pickup address, and valid future pickup window." });

            var id = Guid.NewGuid();
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(ct);
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(ct);
            await using var command = new SqlCommand("INSERT INTO dbo.Donations (Id,CreatedBy,Title,Description,Type,Quantity,Unit,PickupAddress,PickupFrom,PickupUntil,StorageInstructions,Allergens,Status,CreatedAt) VALUES (@Id,@UserId,@Title,@Description,@Type,@Quantity,@Unit,@Address,@From,@Until,@Storage,@Allergens,'Available',SYSUTCDATETIME()); INSERT INTO dbo.DonationStatusHistory (Id,DonationId,Status,ChangedBy,ChangedAt) VALUES (NEWID(),@Id,'Available',@UserId,SYSUTCDATETIME())", connection, transaction);
            command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id;
            command.Parameters.Add("@UserId", SqlDbType.UniqueIdentifier).Value = UserId(user);
            AddDonationParameters(command, body!, type);
            await command.ExecuteNonQueryAsync(ct);
            await transaction.CommitAsync(ct);
            return Results.Created($"/api/donations/{id}", new { id, status = "Available" });
        }).RequireAuthorization();

        app.MapPut("/api/donations/{id:guid}", async (Guid id, CreateDonationRequest? body, ClaimsPrincipal user, CancellationToken ct) =>
        {
            if (!user.IsInRole("Donor")) return Results.Forbid();
            if (!IsValidDonation(body, out var type))
                return Results.BadRequest(new { error = "Enter a title, description, donation type, quantity, pickup address, and valid future pickup window." });

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(ct);
            await using var command = new SqlCommand("UPDATE dbo.Donations SET Title=@Title,Description=@Description,Type=@Type,Quantity=@Quantity,Unit=@Unit,PickupAddress=@Address,PickupFrom=@From,PickupUntil=@Until,StorageInstructions=@Storage,Allergens=@Allergens WHERE Id=@Id AND CreatedBy=@UserId AND Status='Available'", connection);
            command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id;
            command.Parameters.Add("@UserId", SqlDbType.UniqueIdentifier).Value = UserId(user);
            AddDonationParameters(command, body!, type);
            if (await command.ExecuteNonQueryAsync(ct) == 0)
                return Results.Conflict(new { error = "Only your available donations can be edited." });
            return Results.NoContent();
        }).RequireAuthorization();

        app.MapDelete("/api/donations/{id:guid}", async (Guid id, ClaimsPrincipal user, CancellationToken ct) =>
        {
            if (!user.IsInRole("Donor")) return Results.Forbid();
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(ct);
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(ct);
            await using var find = new SqlCommand("SELECT Status FROM dbo.Donations WITH (UPDLOCK,HOLDLOCK) WHERE Id=@Id AND CreatedBy=@UserId", connection, transaction);
            find.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id;
            find.Parameters.Add("@UserId", SqlDbType.UniqueIdentifier).Value = UserId(user);
            var currentStatus = await find.ExecuteScalarAsync(ct);
            if (currentStatus is null)
                return Results.NotFound(new { error = "Donation not found." });
            if (!string.Equals(currentStatus.ToString(), "Available", StringComparison.Ordinal))
                return Results.Conflict(new { error = "Only available donations can be deleted. Cancel or complete donations that already have a request." });

            await using var deleteHistory = new SqlCommand("DELETE FROM dbo.DonationStatusHistory WHERE DonationId=@Id", connection, transaction);
            deleteHistory.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id;
            await deleteHistory.ExecuteNonQueryAsync(ct);
            await using var deleteDonation = new SqlCommand("DELETE FROM dbo.Donations WHERE Id=@Id AND CreatedBy=@UserId AND Status='Available'", connection, transaction);
            deleteDonation.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id;
            deleteDonation.Parameters.Add("@UserId", SqlDbType.UniqueIdentifier).Value = UserId(user);
            if (await deleteDonation.ExecuteNonQueryAsync(ct) == 0)
                return Results.Conflict(new { error = "This donation is no longer available for deletion." });
            await transaction.CommitAsync(ct);
            return Results.NoContent();
        }).RequireAuthorization();

        app.MapPost("/api/donations/{id:guid}/request", async (Guid id, ClaimsPrincipal user, CancellationToken ct) =>
        {
            if (!user.IsInRole("Recipient") || user.FindFirstValue("verification") != "Approved") return Results.Forbid();
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(ct);
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(ct);
            await using var update = new SqlCommand("UPDATE dbo.Donations SET Status='Requested' WHERE Id=@Id AND Status='Available'", connection, transaction);
            update.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id;
            if (await update.ExecuteNonQueryAsync(ct) == 0) return Results.Conflict(new { error = "This donation is no longer available." });
            await using var insert = new SqlCommand("INSERT INTO dbo.DonationRequests (Id,DonationId,RecipientId,Status,RequestedAt) VALUES (NEWID(),@Id,@Recipient,'Pending',SYSUTCDATETIME()); INSERT INTO dbo.DonationStatusHistory (Id,DonationId,Status,ChangedBy,ChangedAt) VALUES (NEWID(),@Id,'Requested',@Recipient,SYSUTCDATETIME())", connection, transaction);
            insert.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id;
            insert.Parameters.Add("@Recipient", SqlDbType.UniqueIdentifier).Value = UserId(user);
            await insert.ExecuteNonQueryAsync(ct);
            await transaction.CommitAsync(ct);
            return Results.Ok(new { id, status = "Requested" });
        }).RequireAuthorization();

        app.MapPost("/api/donations/{id:guid}/accept", async (Guid id, ClaimsPrincipal user, CancellationToken ct) =>
        {
            if (!user.IsInRole("Donor")) return Results.Forbid();
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(ct);
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(ct);
            await using var update = new SqlCommand("UPDATE d SET Status='Accepted',RecipientId=r.RecipientId FROM dbo.Donations d JOIN dbo.DonationRequests r ON r.DonationId=d.Id AND r.Status='Pending' WHERE d.Id=@Id AND d.CreatedBy=@UserId AND d.Status='Requested'", connection, transaction);
            update.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id;
            update.Parameters.Add("@UserId", SqlDbType.UniqueIdentifier).Value = UserId(user);
            if (await update.ExecuteNonQueryAsync(ct) == 0) return Results.Conflict(new { error = "No pending request exists for this donation." });
            await using var requests = new SqlCommand("UPDATE r SET Status=CASE WHEN r.RecipientId=d.RecipientId THEN 'Accepted' ELSE 'Declined' END,RespondedAt=SYSUTCDATETIME() FROM dbo.DonationRequests r JOIN dbo.Donations d ON d.Id=r.DonationId WHERE r.DonationId=@Id; INSERT INTO dbo.DonationStatusHistory (Id,DonationId,Status,ChangedBy,ChangedAt) VALUES (NEWID(),@Id,'Accepted',@UserId,SYSUTCDATETIME())", connection, transaction);
            requests.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id;
            requests.Parameters.Add("@UserId", SqlDbType.UniqueIdentifier).Value = UserId(user);
            await requests.ExecuteNonQueryAsync(ct);
            await transaction.CommitAsync(ct);
            return Results.Ok(new { id, status = "Accepted" });
        }).RequireAuthorization();

        app.MapPatch("/api/donations/{id:guid}/status", async (Guid id, UpdateDonationStatusRequest? body, ClaimsPrincipal user, CancellationToken ct) =>
        {
            if (!Enum.TryParse<DonationStatus>(body?.Status, true, out var status) || status is not (DonationStatus.PickedUp or DonationStatus.Delivered or DonationStatus.Cancelled))
                return Results.BadRequest(new { error = "Status must be PickedUp, Delivered, or Cancelled." });
            var role = Enum.Parse<UserRole>(user.FindFirstValue(ClaimTypes.Role)!);
            if (status == DonationStatus.PickedUp && role != UserRole.Recipient || status == DonationStatus.Delivered && role is not (UserRole.Recipient or UserRole.Donor) || status == DonationStatus.Cancelled && role != UserRole.Donor) return Results.Forbid();
            var sql = status switch
            {
                DonationStatus.PickedUp => "UPDATE dbo.Donations SET Status='PickedUp' WHERE Id=@Id AND RecipientId=@UserId AND Status='Accepted'",
                DonationStatus.Delivered => "UPDATE dbo.Donations SET Status='Delivered' WHERE Id=@Id AND (RecipientId=@UserId OR CreatedBy=@UserId) AND Status='PickedUp'",
                DonationStatus.Cancelled => "UPDATE dbo.Donations SET Status='Cancelled' WHERE Id=@Id AND CreatedBy=@UserId AND Status IN ('Available','Requested','Accepted')",
                _ => ""
            };
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(ct);
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(ct);
            await using var update = new SqlCommand(sql, connection, transaction);
            update.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id;
            update.Parameters.Add("@UserId", SqlDbType.UniqueIdentifier).Value = UserId(user);
            if (await update.ExecuteNonQueryAsync(ct) == 0) return Results.Conflict(new { error = "This donation cannot be moved to that status." });
            await using var history = new SqlCommand("INSERT INTO dbo.DonationStatusHistory (Id,DonationId,Status,ChangedBy,ChangedAt) VALUES (NEWID(),@Id,@Status,@UserId,SYSUTCDATETIME())", connection, transaction);
            history.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id;
            history.Parameters.Add("@Status", SqlDbType.NVarChar, 20).Value = status.ToString();
            history.Parameters.Add("@UserId", SqlDbType.UniqueIdentifier).Value = UserId(user);
            await history.ExecuteNonQueryAsync(ct);
            await transaction.CommitAsync(ct);
            return Results.Ok(new { id, status = status.ToString() });
        }).RequireAuthorization();

        app.MapGet("/api/donations/{id:guid}/history", async (Guid id, ClaimsPrincipal user, CancellationToken ct) =>
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(ct);
            await using var command = new SqlCommand("SELECT h.Status,h.ChangedAt,a.Name FROM dbo.DonationStatusHistory h JOIN dbo.Accounts a ON a.Id=h.ChangedBy WHERE h.DonationId=@Id AND EXISTS(SELECT 1 FROM dbo.Donations d WHERE d.Id=@Id AND (d.CreatedBy=@UserId OR d.RecipientId=@UserId OR @Admin=1)) ORDER BY h.ChangedAt", connection);
            command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id;
            command.Parameters.Add("@UserId", SqlDbType.UniqueIdentifier).Value = UserId(user);
            command.Parameters.Add("@Admin", SqlDbType.Bit).Value = user.IsInRole("Admin");
            await using var reader = await command.ExecuteReaderAsync(ct);
            var items = new List<StatusHistoryItem>();
            while (await reader.ReadAsync(ct)) items.Add(new(reader.GetString(0), reader.GetFieldValue<DateTimeOffset>(1), reader.GetString(2)));
            return Results.Ok(items);
        }).RequireAuthorization();
    }

    private static bool IsValidDonation(CreateDonationRequest? body, out DonationType type)
    {
        type = default;
        return body is not null &&
            !string.IsNullOrWhiteSpace(body.Title) && body.Title.Trim().Length <= 160 &&
            !string.IsNullOrWhiteSpace(body.Description) && body.Description.Trim().Length <= 1200 &&
            Enum.TryParse(body.Type, true, out type) &&
            !string.IsNullOrWhiteSpace(body.Quantity) && body.Quantity.Trim().Length <= 80 &&
            !string.IsNullOrWhiteSpace(body.PickupAddress) && body.PickupAddress.Trim().Length <= 300 &&
            body.PickupFrom > DateTimeOffset.UtcNow && body.PickupUntil > body.PickupFrom &&
            body.Unit?.Length <= 40 && body.StorageInstructions?.Length <= 500 && body.Allergens?.Length <= 500;
    }

    private static void AddDonationParameters(SqlCommand command, CreateDonationRequest body, DonationType type)
    {
        command.Parameters.Add("@Title", SqlDbType.NVarChar, 160).Value = body.Title!.Trim();
        command.Parameters.Add("@Description", SqlDbType.NVarChar, 1200).Value = body.Description!.Trim();
        command.Parameters.Add("@Type", SqlDbType.NVarChar, 20).Value = type.ToString();
        command.Parameters.Add("@Quantity", SqlDbType.NVarChar, 80).Value = body.Quantity!.Trim();
        command.Parameters.Add("@Unit", SqlDbType.NVarChar, 40).Value = (object?)body.Unit?.Trim() ?? DBNull.Value;
        command.Parameters.Add("@Address", SqlDbType.NVarChar, 300).Value = body.PickupAddress!.Trim();
        command.Parameters.Add("@From", SqlDbType.DateTimeOffset).Value = body.PickupFrom;
        command.Parameters.Add("@Until", SqlDbType.DateTimeOffset).Value = body.PickupUntil;
        command.Parameters.Add("@Storage", SqlDbType.NVarChar, 500).Value = (object?)body.StorageInstructions?.Trim() ?? DBNull.Value;
        command.Parameters.Add("@Allergens", SqlDbType.NVarChar, 500).Value = (object?)body.Allergens?.Trim() ?? DBNull.Value;
    }

    private static Guid UserId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private static DonationView ReadDonation(SqlDataReader reader) => new(
        reader.GetGuid(0), reader.GetGuid(1), reader.IsDBNull(2) ? null : reader.GetGuid(2),
        reader.GetString(3), reader.GetString(4), Enum.Parse<DonationType>(reader.GetString(5)),
        reader.GetString(6), reader.IsDBNull(7) ? null : reader.GetString(7), reader.GetString(8),
        reader.GetFieldValue<DateTimeOffset>(9), reader.GetFieldValue<DateTimeOffset>(10),
        reader.IsDBNull(11) ? null : reader.GetString(11), reader.IsDBNull(12) ? null : reader.GetString(12),
        Enum.Parse<DonationStatus>(reader.GetString(13)), reader.GetFieldValue<DateTimeOffset>(14), reader.GetString(15), reader.GetString(16));
}
