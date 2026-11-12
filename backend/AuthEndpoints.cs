using Hunger.Api.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;

namespace Hunger.Api;

public static class AuthEndpoints
{
    public static void Map(WebApplication app, string connectionString)
    {
        app.MapPost("/api/auth/register", async (RegisterRequest? body, CancellationToken ct) =>
        {
            if (body is null || string.IsNullOrWhiteSpace(body.Name) || body.Name.Trim().Length > 120 ||
                string.IsNullOrWhiteSpace(body.Email) || body.Email.Trim().Length > 254 || !body.Email.Contains('@') ||
                string.IsNullOrWhiteSpace(body.Password) || body.Password.Length < 10 || body.Password.Length > 200 ||
                !Enum.TryParse<UserRole>(body.Role, true, out var role) || role == UserRole.Admin ||
                string.IsNullOrWhiteSpace(body.OrganizationName) || body.OrganizationName.Trim().Length > 160)
                return Results.BadRequest(new { error = "Enter your name, valid email, organization, donor or recipient role, and a password of at least 10 characters." });

            var account = new Account(Guid.NewGuid(), body.Name.Trim(), body.Email.Trim().ToLowerInvariant(), body.OrganizationName.Trim(), role,
                role == UserRole.Recipient ? VerificationStatus.Pending : VerificationStatus.Approved);
            var salt = RandomNumberGenerator.GetBytes(16);
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(ct);
            await using var command = new SqlCommand("INSERT INTO dbo.Accounts (Id,Name,Email,OrganizationName,Role,VerificationStatus,PasswordSalt,PasswordHash,CreatedAt) VALUES (@Id,@Name,@Email,@Organization,@Role,@Verification,@Salt,@Hash,SYSUTCDATETIME())", connection);
            command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = account.Id;
            command.Parameters.Add("@Name", SqlDbType.NVarChar, 120).Value = account.Name;
            command.Parameters.Add("@Email", SqlDbType.NVarChar, 254).Value = account.Email;
            command.Parameters.Add("@Organization", SqlDbType.NVarChar, 160).Value = account.OrganizationName;
            command.Parameters.Add("@Role", SqlDbType.NVarChar, 20).Value = account.Role.ToString();
            command.Parameters.Add("@Verification", SqlDbType.NVarChar, 20).Value = account.VerificationStatus.ToString();
            command.Parameters.Add("@Salt", SqlDbType.VarBinary, 16).Value = salt;
            command.Parameters.Add("@Hash", SqlDbType.VarBinary, 32).Value = Passwords.Hash(body.Password, salt);
            try { await command.ExecuteNonQueryAsync(ct); }
            catch (SqlException error) when (error.Number is 2601 or 2627)
            { return Results.Conflict(new { error = "An account with this email already exists." }); }
            return Results.Created("/api/auth/me", account);
        });

        app.MapPost("/api/auth/login", async (LoginRequest? body, HttpContext context, CancellationToken ct) =>
        {
            if (body is null || string.IsNullOrWhiteSpace(body.Email) || string.IsNullOrEmpty(body.Password)) return Results.Unauthorized();
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(ct);
            await using var command = new SqlCommand("SELECT Id,Name,Email,OrganizationName,Role,VerificationStatus,PasswordSalt,PasswordHash FROM dbo.Accounts WHERE Email=@Email", connection);
            command.Parameters.Add("@Email", SqlDbType.NVarChar, 254).Value = body.Email.Trim().ToLowerInvariant();
            await using var reader = await command.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct) || !Passwords.Verify(body.Password, (byte[])reader[6], (byte[])reader[7])) return Results.Unauthorized();
            var account = new Account(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), Enum.Parse<UserRole>(reader.GetString(4)), Enum.Parse<VerificationStatus>(reader.GetString(5)));
            await reader.CloseAsync();
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, account.Id.ToString()), new Claim(ClaimTypes.Name, account.Name),
                new Claim(ClaimTypes.Email, account.Email), new Claim(ClaimTypes.Role, account.Role.ToString()),
                new Claim("organization", account.OrganizationName), new Claim("verification", account.VerificationStatus.ToString())
            };
            await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
            return Results.Ok(account);
        });

        app.MapGet("/api/auth/me", (ClaimsPrincipal user) => Results.Ok(CurrentAccount(user))).RequireAuthorization();
        app.MapPost("/api/auth/logout", async (HttpContext context) =>
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.NoContent();
        }).RequireAuthorization();

        app.MapGet("/api/admin/recipients", async (CancellationToken ct) =>
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(ct);
            await using var command = new SqlCommand("SELECT Id,Name,Email,OrganizationName,CreatedAt FROM dbo.Accounts WHERE Role='Recipient' AND VerificationStatus='Pending' ORDER BY CreatedAt", connection);
            await using var reader = await command.ExecuteReaderAsync(ct);
            var recipients = new List<PendingRecipient>();
            while (await reader.ReadAsync(ct)) recipients.Add(new(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetFieldValue<DateTimeOffset>(4)));
            return Results.Ok(recipients);
        }).RequireAuthorization(policy => policy.RequireRole("Admin"));

        app.MapPost("/api/admin/recipients/{id:guid}/approve", async (Guid id, CancellationToken ct) =>
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(ct);
            await using var command = new SqlCommand("UPDATE dbo.Accounts SET VerificationStatus='Approved' WHERE Id=@Id AND Role='Recipient' AND VerificationStatus='Pending'", connection);
            command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id;
            return await command.ExecuteNonQueryAsync(ct) == 0 ? Results.NotFound(new { error = "Pending recipient not found." }) : Results.NoContent();
        }).RequireAuthorization(policy => policy.RequireRole("Admin"));
    }

    public static async Task SeedAdminAsync(string connectionString, string name, string email, string organizationName, string password)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand("IF EXISTS (SELECT 1 FROM dbo.Accounts WHERE Email=@Email) UPDATE dbo.Accounts SET Name=@Name,OrganizationName=@OrganizationName,Role='Admin',VerificationStatus='Approved',PasswordSalt=@Salt,PasswordHash=@Hash WHERE Email=@Email ELSE INSERT INTO dbo.Accounts (Id,Name,Email,OrganizationName,Role,VerificationStatus,PasswordSalt,PasswordHash,CreatedAt) VALUES (@Id,@Name,@Email,@OrganizationName,'Admin','Approved',@Salt,@Hash,SYSUTCDATETIME())", connection);
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = Guid.NewGuid();
        command.Parameters.Add("@Name", SqlDbType.NVarChar, 120).Value = name.Trim();
        command.Parameters.Add("@Email", SqlDbType.NVarChar, 254).Value = email.Trim().ToLowerInvariant();
        command.Parameters.Add("@OrganizationName", SqlDbType.NVarChar, 160).Value = organizationName.Trim();
        var salt = RandomNumberGenerator.GetBytes(16);
        command.Parameters.Add("@Salt", SqlDbType.VarBinary, 16).Value = salt;
        command.Parameters.Add("@Hash", SqlDbType.VarBinary, 32).Value = Passwords.Hash(password, salt);
        await command.ExecuteNonQueryAsync();
    }

    private static Account CurrentAccount(ClaimsPrincipal user) => new(
        Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!), user.Identity!.Name!,
        user.FindFirstValue(ClaimTypes.Email)!, user.FindFirstValue("organization")!,
        Enum.Parse<UserRole>(user.FindFirstValue(ClaimTypes.Role)!),
        Enum.Parse<VerificationStatus>(user.FindFirstValue("verification")!));
}
