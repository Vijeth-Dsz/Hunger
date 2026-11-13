using Hunger.Api.Data;
using Hunger.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;
using System.Security.Claims;

namespace Hunger.Api.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public sealed class DonationsController(HungerDbContext db) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        var query = User.IsInRole("Admin")
            ? HungerQueries.AdminSummary(db)
            : User.IsInRole("Donor")
                ? HungerQueries.DonorSummary(db, userId)
                : HungerQueries.RecipientSummary(db, userId);
        var summary = await FirstOrDefault(query, cancellationToken) ?? new DashboardSummary(0, 0, 0, 0);
        return Ok(summary);
    }

    [HttpGet("donations")]
    public async Task<IActionResult> GetDonations(CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        var query = User.IsInRole("Admin")
            ? HungerQueries.AllDonations(db)
            : User.IsInRole("Donor")
                ? HungerQueries.DonorDonations(db, userId)
                : HungerQueries.RecipientDonations(db, userId);
        var recipientView = User.IsInRole("Recipient");
        var donations = new List<DonationView>();
        await foreach (var donation in query.WithCancellation(cancellationToken))
        {
            donations.Add(ContractMappings.ToDonation(donation, recipientView));
        }

        return Ok(donations);
    }

    [HttpGet("donations/{id:guid}")]
    public async Task<IActionResult> GetDonation(Guid id, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        var query = User.IsInRole("Admin")
            ? HungerQueries.AdminDonationById(db, id)
            : User.IsInRole("Donor")
                ? HungerQueries.DonorDonationById(db, userId, id)
                : HungerQueries.RecipientDonationById(db, userId, id);
        var donation = await FirstOrDefault(query, cancellationToken);
        return donation is null
            ? NotFound(new { error = "Donation not found." })
            : Ok(ContractMappings.ToDonation(donation, User.IsInRole("Recipient")));
    }

    [Authorize(Roles = "Donor")]
    [HttpPost("donations")]
    public async Task<IActionResult> CreateDonation(CreateDonationRequest? body, CancellationToken cancellationToken)
    {
        if (!IsApproved() || !TryValidateDonation(body, out var type))
        {
            return BadRequest(new { error = "Enter valid donation details, a future pickup window, and a use-by date after pickup for food or groceries." });
        }

        var donation = new DonationEntity
        {
            Id = Guid.NewGuid(),
            CreatedBy = CurrentUserId(),
            Title = body!.Title!.Trim(),
            Description = body.Description!.Trim(),
            Type = type.ToString(),
            Quantity = body.Quantity!.Trim(),
            Unit = string.IsNullOrWhiteSpace(body.Unit) ? null : body.Unit.Trim(),
            PickupAddress = body.PickupAddress!.Trim(),
            PickupFrom = body.PickupFrom,
            PickupUntil = body.PickupUntil,
            StorageInstructions = string.IsNullOrWhiteSpace(body.StorageInstructions) ? null : body.StorageInstructions.Trim(),
            Allergens = string.IsNullOrWhiteSpace(body.Allergens) ? null : body.Allergens.Trim(),
            ExpiresAt = body.ExpiresAt,
            ReviewStatus = ReviewStatus.Approved.ToString(),
            Status = DonationStatus.Available.ToString(),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        db.Donations.Add(donation);
        AddHistory(donation.Id, DonationStatus.Available);
        await db.SaveChangesAsync(cancellationToken);
        return Created($"/api/donations/{donation.Id}", new { id = donation.Id, status = donation.Status });
    }

    [Authorize(Roles = "Donor")]
    [HttpPut("donations/{id:guid}")]
    public async Task<IActionResult> UpdateDonation(Guid id, CreateDonationRequest? body, CancellationToken cancellationToken)
    {
        if (!IsApproved() || !TryValidateDonation(body, out var type))
        {
            return BadRequest(new { error = "Enter valid donation details and a use-by date after pickup for food or groceries." });
        }

        var donation = await db.Donations.FirstOrDefaultAsync(
            candidate => candidate.Id == id && candidate.CreatedBy == CurrentUserId() && candidate.Status == "Available",
            cancellationToken);
        if (donation is null)
        {
            return Conflict(new { error = "Only your available donations can be edited." });
        }

        donation.Title = body!.Title!.Trim();
        donation.Description = body.Description!.Trim();
        donation.Type = type.ToString();
        donation.Quantity = body.Quantity!.Trim();
        donation.Unit = string.IsNullOrWhiteSpace(body.Unit) ? null : body.Unit.Trim();
        donation.PickupAddress = body.PickupAddress!.Trim();
        donation.PickupFrom = body.PickupFrom;
        donation.PickupUntil = body.PickupUntil;
        donation.StorageInstructions = string.IsNullOrWhiteSpace(body.StorageInstructions) ? null : body.StorageInstructions.Trim();
        donation.Allergens = string.IsNullOrWhiteSpace(body.Allergens) ? null : body.Allergens.Trim();
        donation.ExpiresAt = body.ExpiresAt;
        donation.ReviewStatus = ReviewStatus.Approved.ToString();
        donation.ReviewNotes = null;
        donation.ReviewedBy = null;
        donation.ReviewedAt = null;
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [Authorize(Roles = "Donor")]
    [HttpDelete("donations/{id:guid}")]
    public async Task<IActionResult> DeleteDonation(Guid id, CancellationToken cancellationToken)
    {
        var donation = await db.Donations.FirstOrDefaultAsync(
            candidate => candidate.Id == id && candidate.CreatedBy == CurrentUserId(),
            cancellationToken);
        if (donation is null)
        {
            return NotFound(new { error = "Donation not found." });
        }
        if (donation.Status != DonationStatus.Available.ToString())
        {
            return Conflict(new { error = "Only available donations can be deleted. Cancel or complete donations that already have a request." });
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var requests = await db.DonationRequests.Where(request => request.DonationId == id).ToListAsync(cancellationToken);
        var history = await db.DonationStatusHistory.Where(item => item.DonationId == id).ToListAsync(cancellationToken);
        db.DonationRequests.RemoveRange(requests);
        db.DonationStatusHistory.RemoveRange(history);
        db.Donations.Remove(donation);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return NoContent();
    }

    [Authorize(Roles = "Recipient")]
    [HttpPost("donations/{id:guid}/request")]
    public async Task<IActionResult> RequestDonation(Guid id, CancellationToken cancellationToken)
    {
        if (!IsApproved()) return Forbid();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var donation = await db.Donations.FirstOrDefaultAsync(
            candidate => candidate.Id == id && candidate.Status == "Available" && candidate.ReviewStatus == "Approved",
            cancellationToken);
        if (donation is null)
        {
            return Conflict(new { error = "This donation is no longer available." });
        }

        donation.Status = DonationStatus.Requested.ToString();
        db.DonationRequests.Add(new DonationRequestEntity
        {
            Id = Guid.NewGuid(),
            DonationId = id,
            RecipientId = CurrentUserId(),
            Status = "Pending",
            RequestedAt = DateTimeOffset.UtcNow,
        });
        AddHistory(id, DonationStatus.Requested);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Ok(new { id, status = donation.Status });
    }

    [Authorize(Roles = "Donor")]
    [HttpPost("donations/{id:guid}/accept")]
    public async Task<IActionResult> AcceptDonation(Guid id, CancellationToken cancellationToken)
    {
        if (!IsApproved()) return Forbid();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var donation = await db.Donations.FirstOrDefaultAsync(
            candidate => candidate.Id == id && candidate.CreatedBy == CurrentUserId() && candidate.Status == "Requested",
            cancellationToken);
        var request = await db.DonationRequests.FirstOrDefaultAsync(
            candidate => candidate.DonationId == id && candidate.Status == "Pending",
            cancellationToken);
        if (donation is null || request is null)
        {
            return Conflict(new { error = "No pending request exists for this donation." });
        }

        donation.Status = DonationStatus.Accepted.ToString();
        donation.RecipientId = request.RecipientId;
        request.Status = "Accepted";
        request.RespondedAt = DateTimeOffset.UtcNow;
        var otherRequests = await db.DonationRequests.Where(
            candidate => candidate.DonationId == id && candidate.Id != request.Id && candidate.Status == "Pending").ToListAsync(cancellationToken);
        foreach (var otherRequest in otherRequests)
        {
            otherRequest.Status = "Declined";
            otherRequest.RespondedAt = DateTimeOffset.UtcNow;
        }

        AddHistory(id, DonationStatus.Accepted);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Ok(new { id, status = donation.Status });
    }

    [HttpPatch("donations/{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, UpdateDonationStatusRequest? body, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<DonationStatus>(body?.Status, true, out var status) ||
            status is not (DonationStatus.PickedUp or DonationStatus.Delivered or DonationStatus.Cancelled))
        {
            return BadRequest(new { error = "Status must be PickedUp, Delivered, or Cancelled." });
        }

        var userId = CurrentUserId();
        var isRecipientTransition = status is DonationStatus.PickedUp or DonationStatus.Delivered;
        if (isRecipientTransition && !User.IsInRole("Recipient") ||
            status == DonationStatus.Cancelled && !User.IsInRole("Donor"))
        {
            return Forbid();
        }
        if (!IsApproved()) return Forbid();

        var donation = await db.Donations.FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (donation is null)
        {
            return NotFound(new { error = "Donation not found." });
        }

        var ownsDonation = donation.CreatedBy == userId;
        var isRecipient = donation.RecipientId == userId;
        var validTransition = status switch
        {
            DonationStatus.PickedUp => isRecipient && donation.Status == "Accepted",
            DonationStatus.Delivered => isRecipient && donation.Status == "PickedUp",
            DonationStatus.Cancelled => ownsDonation && donation.Status is "Available" or "Requested" or "Accepted",
            _ => false,
        };
        if (!validTransition)
        {
            return Conflict(new { error = "This donation cannot be moved to that status." });
        }

        donation.Status = status.ToString();
        AddHistory(id, status);
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { id, status = donation.Status });
    }

    [HttpGet("donations/{id:guid}/history")]
    public async Task<IActionResult> GetHistory(Guid id, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        var donation = await db.Donations.AsNoTracking().FirstOrDefaultAsync(
            candidate => candidate.Id == id &&
                (User.IsInRole("Admin") || candidate.CreatedBy == userId || candidate.RecipientId == userId),
            cancellationToken);
        if (donation is null)
        {
            return NotFound(new { error = "Donation not found." });
        }

        var history = await db.DonationStatusHistory.AsNoTracking()
            .Where(item => item.DonationId == id)
            .OrderBy(item => item.ChangedAt)
            .Select(item => new StatusHistoryItem(item.Status, item.ChangedAt, item.ChangedByAccount.Name))
            .ToListAsync(cancellationToken);
        return Ok(history);
    }

    private void AddHistory(Guid donationId, DonationStatus status)
    {
        db.DonationStatusHistory.Add(new DonationStatusHistoryEntity
        {
            Id = Guid.NewGuid(),
            DonationId = donationId,
            Status = status.ToString(),
            ChangedBy = CurrentUserId(),
            ChangedAt = DateTimeOffset.UtcNow,
        });
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private bool IsApproved() => User.FindFirstValue("verification") == VerificationStatus.Approved.ToString();

    private static bool TryValidateDonation(CreateDonationRequest? body, out DonationType type)
    {
        type = default;
        if (body is null || !Enum.TryParse(body.Type, true, out type)) return false;

        var validDetails = !string.IsNullOrWhiteSpace(body.Title) && body.Title.Trim().Length <= 160 &&
            !string.IsNullOrWhiteSpace(body.Description) && body.Description.Trim().Length <= 1200 &&
            !string.IsNullOrWhiteSpace(body.Quantity) && body.Quantity.Trim().Length <= 80 &&
            !string.IsNullOrWhiteSpace(body.PickupAddress) && body.PickupAddress.Trim().Length <= 300 &&
            body.PickupFrom > DateTimeOffset.UtcNow && body.PickupUntil > body.PickupFrom &&
            body.Unit?.Length <= 40 && body.StorageInstructions?.Length <= 500 && body.Allergens?.Length <= 500;
        var validUseBy = type == DonationType.Stationery
            ? body.ExpiresAt is null
            : body.ExpiresAt >= body.PickupUntil;

        return validDetails && validUseBy;
    }

    private static async Task<T?> FirstOrDefault<T>(IAsyncEnumerable<T> results, CancellationToken cancellationToken)
    {
        await foreach (var result in results.WithCancellation(cancellationToken))
        {
            return result;
        }

        return default;
    }
}
