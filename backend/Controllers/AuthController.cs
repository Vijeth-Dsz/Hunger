using Hunger.Api.Data;
using Hunger.Api.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Security.Cryptography;

namespace Hunger.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(HungerDbContext db) : ControllerBase
{
    private const long MaximumVerificationDocumentBytes = 5 * 1024 * 1024;

    [AllowAnonymous]
    [HttpPost("register")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaximumVerificationDocumentBytes + 64 * 1024)]
    public async Task<IActionResult> Register([FromForm] RegisterRequest? body, CancellationToken cancellationToken)
    {
        if (body is null || string.IsNullOrWhiteSpace(body.Name) || body.Name.Trim().Length > 120 ||
            string.IsNullOrWhiteSpace(body.Email) || body.Email.Trim().Length > 254 || !body.Email.Contains('@') ||
            string.IsNullOrWhiteSpace(body.Password) || body.Password.Length < 10 || body.Password.Length > 200 ||
            string.IsNullOrWhiteSpace(body.Role) || string.IsNullOrWhiteSpace(body.OrganizationName) || body.OrganizationName.Trim().Length > 160)
        {
            return BadRequest(new { error = "Enter your name, valid email, organization, an available role, and a password of at least 10 characters." });
        }

        var document = body.VerificationDocument;
        if (document is null || document.Length == 0 || document.Length > MaximumVerificationDocumentBytes)
        {
            return BadRequest(new { error = "Upload an organization proof document smaller than 5 MB." });
        }

        var role = await FindRegistrationRole(body.Role.Trim(), cancellationToken);
        if (role is null)
        {
            return BadRequest(new { error = "Choose an available registration role." });
        }

        if (!Enum.TryParse<UserRole>(role.RoleKey, true, out var userRole))
        {
            throw new InvalidOperationException("The configured registration role is not supported by the API.");
        }

        await using var documentStream = new MemoryStream((int)document.Length);
        await document.CopyToAsync(documentStream, cancellationToken);
        var documentBytes = documentStream.ToArray();
        if (documentBytes.Length == 0 || documentBytes.Length > MaximumVerificationDocumentBytes ||
            !TryGetVerificationDocumentContentType(document.FileName, documentBytes, out var documentContentType))
        {
            return BadRequest(new { error = "Upload a valid PDF, JPEG, or PNG organization proof document smaller than 5 MB." });
        }

        var salt = RandomNumberGenerator.GetBytes(16);
        var account = new AccountEntity
        {
            Id = Guid.NewGuid(),
            Name = body.Name.Trim(),
            Email = body.Email.Trim().ToLowerInvariant(),
            OrganizationName = body.OrganizationName.Trim(),
            Role = role.RoleKey,
            VerificationStatus = VerificationStatus.Pending.ToString(),
            PasswordSalt = salt,
            PasswordHash = Passwords.Hash(body.Password, salt),
            CreatedAt = DateTimeOffset.UtcNow,
            VerificationDocumentRecord = new VerificationDocumentEntity
            {
                Contents = documentBytes,
                ContentType = documentContentType,
            },
        };

        db.Accounts.Add(account);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (exception.GetBaseException() is SqlException { Number: 2601 or 2627 })
        {
            return Conflict(new { error = "An account with this email already exists." });
        }

        return Created("/api/auth/me", ContractMappings.ToAccount(account));
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest? body, CancellationToken cancellationToken)
    {
        if (body is null || string.IsNullOrWhiteSpace(body.Email) || string.IsNullOrEmpty(body.Password))
        {
            return Unauthorized();
        }

        var account = await FindAccount(body.Email.Trim().ToLowerInvariant(), cancellationToken);
        if (account is null || !Passwords.Verify(body.Password, account.PasswordSalt, account.PasswordHash))
        {
            return Unauthorized();
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, account.Id.ToString()),
            new Claim(ClaimTypes.Name, account.Name),
            new Claim(ClaimTypes.Email, account.Email),
            new Claim(ClaimTypes.Role, account.Role),
            new Claim("organization", account.OrganizationName),
            new Claim("verification", account.VerificationStatus),
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));

        return Ok(ContractMappings.ToAccount(account));
    }

    [Authorize]
    [HttpGet("me")]
    public IActionResult CurrentUser()
    {
        var account = new Account(
            Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!),
            User.FindFirstValue(ClaimTypes.Name)!,
            User.FindFirstValue(ClaimTypes.Email)!,
            User.FindFirstValue("organization")!,
            Enum.Parse<UserRole>(User.FindFirstValue(ClaimTypes.Role)!),
            Enum.Parse<VerificationStatus>(User.FindFirstValue("verification")!));
        return Ok(account);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    private static bool TryGetVerificationDocumentContentType(string fileName, byte[] contents, out string contentType)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        contentType = string.Empty;
        if (extension == ".pdf" && contents.Length >= 5 && contents[0] == '%' && contents[1] == 'P' && contents[2] == 'D' && contents[3] == 'F' && contents[4] == '-')
        {
            contentType = "application/pdf";
            return true;
        }

        if (extension == ".png" && contents.Length >= 8 && contents[0] == 0x89 && contents[1] == 0x50 && contents[2] == 0x4E && contents[3] == 0x47 &&
            contents[4] == 0x0D && contents[5] == 0x0A && contents[6] == 0x1A && contents[7] == 0x0A)
        {
            contentType = "image/png";
            return true;
        }

        if (extension is ".jpg" or ".jpeg" && contents.Length >= 3 && contents[0] == 0xFF && contents[1] == 0xD8 && contents[2] == 0xFF)
        {
            contentType = "image/jpeg";
            return true;
        }

        return false;
    }

    private async Task<RoleEntity?> FindRegistrationRole(string roleKey, CancellationToken cancellationToken)
    {
        await foreach (var role in HungerQueries.RegistrationRole(db, roleKey).WithCancellation(cancellationToken))
        {
            return role;
        }

        return null;
    }

    private async Task<AccountEntity?> FindAccount(string email, CancellationToken cancellationToken)
    {
        await foreach (var account in HungerQueries.AccountByEmail(db, email).WithCancellation(cancellationToken))
        {
            return account;
        }

        return null;
    }
}
