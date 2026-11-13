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
public sealed class NeedsController(HungerDbContext db) : ControllerBase
{
    [HttpGet("needs")]
    public async Task<IActionResult> GetNeeds(CancellationToken cancellationToken)
    {
        var query = User.IsInRole("Admin")
            ? HungerQueries.AllNeeds(db)
            : User.IsInRole("Recipient")
                ? HungerQueries.RecipientNeeds(db, CurrentUserId())
                : HungerQueries.ApprovedNeeds(db);
        var needs = new List<NeedView>();
        await foreach (var need in query.WithCancellation(cancellationToken))
        {
            needs.Add(ContractMappings.ToNeed(need));
        }

        return Ok(needs);
    }

    [Authorize(Roles = "Recipient")]
    [HttpPost("needs")]
    public async Task<IActionResult> CreateNeed(CreateNeedRequest? body, CancellationToken cancellationToken)
    {
        if (!IsApproved()) return Forbid();
        if (!TryValidateNeed(body, out var type))
        {
            return BadRequest(new { error = "Enter a valid item, positive requested quantity, unit, category, description, and future needed-by date." });
        }

        var need = new CommunityNeedEntity
        {
            Id = Guid.NewGuid(),
            RecipientId = CurrentUserId(),
            Title = body!.Title!.Trim(),
            Description = body.Description!.Trim(),
            Type = type.ToString(),
            Quantity = body.Quantity,
            Unit = body.Unit!.Trim(),
            NeededBy = body.NeededBy,
            Status = NeedStatus.Open.ToString(),
            ReviewStatus = ReviewStatus.Approved.ToString(),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        db.CommunityNeeds.Add(need);
        await db.SaveChangesAsync(cancellationToken);
        return Created($"/api/needs/{need.Id}", new { id = need.Id, reviewStatus = need.ReviewStatus });
    }

    [Authorize(Roles = "Recipient")]
    [HttpPut("needs/{id:guid}")]
    public async Task<IActionResult> UpdateNeed(Guid id, CreateNeedRequest? body, CancellationToken cancellationToken)
    {
        if (!IsApproved()) return Forbid();
        if (!TryValidateNeed(body, out var type))
        {
            return BadRequest(new { error = "Enter a valid item, positive requested quantity, unit, category, description, and future needed-by date." });
        }

        var need = await db.CommunityNeeds
            .Include(candidate => candidate.Contributions)
            .FirstOrDefaultAsync(candidate => candidate.Id == id && candidate.RecipientId == CurrentUserId(), cancellationToken);
        if (need is null)
        {
            return NotFound(new { error = "Recipient request not found." });
        }
        if (need.Status != NeedStatus.Open.ToString() || need.Contributions.Count > 0)
        {
            return Conflict(new { error = "Only requests without contributions can be edited." });
        }

        need.Title = body!.Title!.Trim();
        need.Description = body.Description!.Trim();
        need.Type = type.ToString();
        need.Quantity = body.Quantity;
        need.Unit = body.Unit!.Trim();
        need.NeededBy = body.NeededBy;
        need.ReviewStatus = ReviewStatus.Approved.ToString();
        need.ReviewNotes = null;
        need.ReviewedBy = null;
        need.ReviewedAt = null;
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [Authorize(Roles = "Recipient")]
    [HttpDelete("needs/{id:guid}")]
    public async Task<IActionResult> DeleteNeed(Guid id, CancellationToken cancellationToken)
    {
        if (!IsApproved()) return Forbid();

        var need = await db.CommunityNeeds
            .Include(candidate => candidate.Contributions)
            .FirstOrDefaultAsync(candidate => candidate.Id == id && candidate.RecipientId == CurrentUserId(), cancellationToken);
        if (need is null)
        {
            return NotFound(new { error = "Recipient request not found." });
        }
        if (need.Status != NeedStatus.Open.ToString() || need.Contributions.Count > 0)
        {
            return Conflict(new { error = "Only open requests without contributions can be deleted." });
        }

        db.CommunityNeeds.Remove(need);
        await db.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    [HttpGet("needs/{id:guid}/contributions")]
    public async Task<IActionResult> GetContributions(Guid id, CancellationToken cancellationToken)
    {
        var userId = CurrentUserId();
        var query = db.NeedContributions.AsNoTracking()
            .Include(contribution => contribution.Donor)
            .Include(contribution => contribution.Need)
                .ThenInclude(need => need.Recipient)
            .Where(contribution => contribution.NeedId == id);
        if (User.IsInRole("Recipient"))
        {
            query = query.Where(contribution => contribution.Need.RecipientId == userId);
        }
        else if (User.IsInRole("Donor"))
        {
            query = query.Where(contribution => contribution.DonorId == userId);
        }

        var entities = await query.OrderByDescending(contribution => contribution.CreatedAt)
            .ToListAsync(cancellationToken);
        return Ok(entities.Select(ContractMappings.ToContribution));
    }

    [Authorize(Roles = "Donor")]
    [HttpPost("needs/{id:guid}/contributions")]
    public async Task<IActionResult> CreateContribution(Guid id, CreateContributionRequest? body, CancellationToken cancellationToken)
    {
        if (!IsApproved()) return Forbid();
        if (!TryValidateContribution(body))
        {
            return BadRequest(new { error = "Enter a positive quantity and valid pickup details." });
        }

        var need = await db.CommunityNeeds.FirstOrDefaultAsync(
            candidate => candidate.Id == id && candidate.ReviewStatus == "Approved" &&
                (candidate.Status == "Open" || candidate.Status == "PartiallyFulfilled") &&
                candidate.Recipient.VerificationStatus == "Approved",
            cancellationToken);
        if (need is null)
        {
            return NotFound(new { error = "This approved need is no longer open for contributions." });
        }
        if (!string.Equals(body!.Unit!.Trim(), need.Unit, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { error = "Contribution unit must match the unit requested by the recipient." });
        }

        var stationery = string.Equals(need.Type, DonationType.Stationery.ToString(), StringComparison.Ordinal);
        if (!stationery && (body.ExpiresAt is null || body.ExpiresAt < body.PickupUntil))
        {
            return BadRequest(new { error = "Food and grocery contributions require a use-by date that covers the pickup window." });
        }

        var contribution = new NeedContributionEntity
        {
            Id = Guid.NewGuid(),
            NeedId = id,
            DonorId = CurrentUserId(),
            Quantity = body.Quantity,
            Unit = need.Unit,
            PickupAddress = body.PickupAddress!.Trim(),
            PickupFrom = body.PickupFrom,
            PickupUntil = body.PickupUntil,
            ExpiresAt = stationery ? null : body.ExpiresAt,
            StorageInstructions = string.IsNullOrWhiteSpace(body.StorageInstructions) ? null : body.StorageInstructions.Trim(),
            Allergens = string.IsNullOrWhiteSpace(body.Allergens) ? null : body.Allergens.Trim(),
            Status = ContributionStatus.Approved.ToString(),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        db.NeedContributions.Add(contribution);
        await db.SaveChangesAsync(cancellationToken);
        return Created($"/api/needs/{id}/contributions/{contribution.Id}", new { id = contribution.Id, status = contribution.Status });
    }

    [Authorize(Roles = "Recipient")]
    [HttpPost("contributions/{id:guid}/received")]
    public async Task<IActionResult> ReceiveContribution(Guid id, CancellationToken cancellationToken)
    {
        if (!IsApproved()) return Forbid();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var contribution = await db.NeedContributions
            .Include(item => item.Need)
            .FirstOrDefaultAsync(item => item.Id == id && item.Need.RecipientId == CurrentUserId(), cancellationToken);
        if (contribution is null || contribution.Status != ContributionStatus.Approved.ToString())
        {
            return Conflict(new { error = "Only an approved contribution to your organization can be marked received." });
        }

        contribution.Status = ContributionStatus.Delivered.ToString();
        contribution.ReceivedAt = DateTimeOffset.UtcNow;
        var delivered = await db.NeedContributions
            .Where(item => item.NeedId == contribution.NeedId && item.Status == ContributionStatus.Delivered.ToString())
            .SumAsync(item => (decimal?)item.Quantity, cancellationToken) ?? 0;
        delivered += contribution.Quantity;
        contribution.Need.Status = delivered >= contribution.Need.Quantity
            ? NeedStatus.Fulfilled.ToString()
            : NeedStatus.PartiallyFulfilled.ToString();

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Ok(new { status = ContributionStatus.Delivered, successful = true });
    }

    private Guid CurrentUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private bool IsApproved() => User.FindFirstValue("verification") == VerificationStatus.Approved.ToString();

    private static bool TryValidateNeed(CreateNeedRequest? body, out DonationType type)
    {
        type = default;
        return body is not null &&
            !string.IsNullOrWhiteSpace(body.Title) && body.Title.Trim().Length <= 160 &&
            !string.IsNullOrWhiteSpace(body.Description) && body.Description.Trim().Length <= 1200 &&
            Enum.TryParse(body.Type, true, out type) &&
            body.Quantity > 0 && body.Quantity <= 1_000_000_000 &&
            !string.IsNullOrWhiteSpace(body.Unit) && body.Unit.Trim().Length <= 40 &&
            body.NeededBy > DateTimeOffset.UtcNow;
    }

    private static bool TryValidateContribution(CreateContributionRequest? body) =>
        body is not null && body.Quantity > 0 && body.Quantity <= 1_000_000_000 &&
        !string.IsNullOrWhiteSpace(body.Unit) && body.Unit.Trim().Length <= 40 &&
        !string.IsNullOrWhiteSpace(body.PickupAddress) && body.PickupAddress.Trim().Length <= 300 &&
        body.PickupFrom > DateTimeOffset.UtcNow && body.PickupUntil > body.PickupFrom &&
        (body.ExpiresAt is null || body.ExpiresAt > body.PickupFrom) &&
        body.StorageInstructions?.Length <= 500 && body.Allergens?.Length <= 500;
}
