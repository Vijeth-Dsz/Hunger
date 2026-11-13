using Hunger.Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace Hunger.Api.Data;

public static class AuthSeeder
{
    public static async Task SeedAdminAsync(
        HungerDbContext db,
        string name,
        string email,
        string organizationName,
        string password,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var account = await db.Accounts.FirstOrDefaultAsync(
            candidate => candidate.Email == normalizedEmail,
            cancellationToken);
        var salt = RandomNumberGenerator.GetBytes(16);

        if (account is null)
        {
            account = new AccountEntity
            {
                Id = Guid.NewGuid(),
                Email = normalizedEmail,
                CreatedAt = DateTimeOffset.UtcNow,
            };
            db.Accounts.Add(account);
        }

        account.Name = name.Trim();
        account.OrganizationName = organizationName.Trim();
        account.Role = UserRole.Admin.ToString();
        account.VerificationStatus = VerificationStatus.Approved.ToString();
        account.PasswordSalt = salt;
        account.PasswordHash = Passwords.Hash(password, salt);
        await db.SaveChangesAsync(cancellationToken);
    }
}
