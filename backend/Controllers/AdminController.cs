using Hunger.Api.Data;
using Hunger.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;

namespace Hunger.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin")]
public sealed class AdminController(HungerDbContext db) : ControllerBase
{
    [HttpGet("accounts")]
    public async Task<IActionResult> GetAccounts(CancellationToken cancellationToken)
    {
        var accounts = new List<AdminAccountView>();
        await foreach (var account in HungerQueries.ReviewableAccounts(db).WithCancellation(cancellationToken))
        {
            accounts.Add(new AdminAccountView(
                account.Id,
                account.Name,
                account.Email,
                account.OrganizationName,
                Enum.Parse<UserRole>(account.Role),
                Enum.Parse<VerificationStatus>(account.VerificationStatus),
                account.CreatedAt,
                account.HasVerificationDocument));
        }

        return Ok(accounts);
    }

    [HttpPost("accounts/{id:guid}/review")]
    public async Task<IActionResult> ReviewAccount(Guid id, ModerateRequest? body, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<VerificationStatus>(body?.Status, true, out var status) ||
            status == VerificationStatus.Pending || body?.Notes?.Length > 500)
        {
            return BadRequest(new { error = "Choose Approved or Rejected and provide no more than 500 note characters." });
        }

        var account = await db.Accounts.FirstOrDefaultAsync(
            candidate => candidate.Id == id && (candidate.Role == "Donor" || candidate.Role == "Recipient"),
            cancellationToken);
        if (account is null)
        {
            return NotFound(new { error = "Account not found." });
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        account.VerificationStatus = status.ToString();
        await db.SaveChangesAsync(cancellationToken);

        if (status == VerificationStatus.Approved && account.Role == UserRole.Donor.ToString())
        {
            await db.Donations
                .Where(donation => donation.CreatedBy == id)
                .ExecuteUpdateAsync(updates => updates.SetProperty(donation => donation.ReviewStatus, ReviewStatus.Approved.ToString()), cancellationToken);
            await db.NeedContributions
                .Where(contribution => contribution.DonorId == id &&
                    (contribution.Status == ContributionStatus.PendingReview.ToString() || contribution.Status == ContributionStatus.Rejected.ToString()) &&
                    contribution.Need.Recipient.VerificationStatus == VerificationStatus.Approved.ToString() &&
                    contribution.Need.ReviewStatus == ReviewStatus.Approved.ToString())
                .ExecuteUpdateAsync(updates => updates.SetProperty(contribution => contribution.Status, ContributionStatus.Approved.ToString()), cancellationToken);
        }
        else if (status == VerificationStatus.Approved && account.Role == UserRole.Recipient.ToString())
        {
            await db.CommunityNeeds
                .Where(need => need.RecipientId == id)
                .ExecuteUpdateAsync(updates => updates.SetProperty(need => need.ReviewStatus, ReviewStatus.Approved.ToString()), cancellationToken);
            await db.NeedContributions
                .Where(contribution => contribution.Need.RecipientId == id &&
                    (contribution.Status == ContributionStatus.PendingReview.ToString() || contribution.Status == ContributionStatus.Rejected.ToString()) &&
                    contribution.Donor.VerificationStatus == VerificationStatus.Approved.ToString())
                .ExecuteUpdateAsync(updates => updates.SetProperty(contribution => contribution.Status, ContributionStatus.Approved.ToString()), cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("accounts/{id:guid}/verification-document")]
    public async Task<IActionResult> GetVerificationDocument(Guid id, CancellationToken cancellationToken)
    {
        VerificationDocumentRecord? document = null;
        await foreach (var record in HungerQueries.VerificationDocumentByAccountId(db, id).WithCancellation(cancellationToken))
        {
            document = record;
            break;
        }

        if (document?.Contents is null || document.ContentType is null)
        {
            return NotFound(new { error = "Verification document not found." });
        }

        Response.Headers.XContentTypeOptions = "nosniff";
        var extension = document.ContentType switch
        {
            "application/pdf" => ".pdf",
            "image/jpeg" => ".jpg",
            "image/png" => ".png",
            _ => throw new InvalidOperationException("The stored verification document content type is not supported."),
        };
        return File(document.Contents, document.ContentType, $"organization-proof{extension}");
    }
}
