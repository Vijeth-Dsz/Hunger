using Hunger.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Hunger.Api.Data;

public static class HungerQueries
{
    public static readonly Func<HungerDbContext, IAsyncEnumerable<RoleOption>> EnabledRoles =
        EF.CompileAsyncQuery((HungerDbContext db) => db.Roles
            .AsNoTracking()
            .Where(role => role.IsEnabled)
            .OrderBy(role => role.SortOrder)
            .Select(role => new RoleOption(role.RoleKey, role.DisplayName, role.Description, role.RegistrationAllowed)));

    public static readonly Func<HungerDbContext, string, IAsyncEnumerable<RoleEntity>> RegistrationRole =
        EF.CompileAsyncQuery((HungerDbContext db, string roleKey) => db.Roles
            .AsNoTracking()
            .Where(role => role.RoleKey == roleKey && role.IsEnabled && role.RegistrationAllowed));

    public static readonly Func<HungerDbContext, string, IAsyncEnumerable<AccountEntity>> AccountByEmail =
        EF.CompileAsyncQuery((HungerDbContext db, string email) => db.Accounts
            .AsNoTracking()
            .Where(account => account.Email == email));

    public static readonly Func<HungerDbContext, Guid, IAsyncEnumerable<DonationEntity>> DonorDonations =
        EF.CompileAsyncQuery((HungerDbContext db, Guid donorId) => db.Donations
            .AsNoTracking()
            .Include(donation => donation.Donor)
            .Include(donation => donation.Recipient)
            .Include(donation => donation.Requests)
                .ThenInclude(request => request.Recipient)
            .Where(donation => donation.CreatedBy == donorId)
            .OrderByDescending(donation => donation.CreatedAt)
            .Select(donation => donation));

    public static readonly Func<HungerDbContext, Guid, IAsyncEnumerable<DonationEntity>> RecipientDonations =
        EF.CompileAsyncQuery((HungerDbContext db, Guid recipientId) => db.Donations
            .AsNoTracking()
            .Include(donation => donation.Donor)
            .Include(donation => donation.Recipient)
            .Include(donation => donation.Requests)
                .ThenInclude(request => request.Recipient)
            .Where(donation =>
                ((donation.Status == "Available" && donation.ReviewStatus == "Approved") ||
                 donation.RecipientId == recipientId ||
                 donation.Requests.Any(request => request.RecipientId == recipientId)) &&
                donation.Donor.VerificationStatus == "Approved")
            .OrderByDescending(donation => donation.CreatedAt)
            .Select(donation => donation));

    public static readonly Func<HungerDbContext, IAsyncEnumerable<DonationEntity>> AllDonations =
        EF.CompileAsyncQuery((HungerDbContext db) => db.Donations
            .AsNoTracking()
            .Include(donation => donation.Donor)
            .Include(donation => donation.Recipient)
            .Include(donation => donation.Requests)
                .ThenInclude(request => request.Recipient)
            .OrderByDescending(donation => donation.CreatedAt)
            .Select(donation => donation));

    public static readonly Func<HungerDbContext, Guid, IAsyncEnumerable<CommunityNeedEntity>> RecipientNeeds =
        EF.CompileAsyncQuery((HungerDbContext db, Guid recipientId) => db.CommunityNeeds
            .AsNoTracking()
            .Include(need => need.Recipient)
            .Include(need => need.Contributions)
            .Where(need => need.RecipientId == recipientId)
            .OrderByDescending(need => need.CreatedAt)
            .Select(need => need));

    public static readonly Func<HungerDbContext, IAsyncEnumerable<CommunityNeedEntity>> ApprovedNeeds =
        EF.CompileAsyncQuery((HungerDbContext db) => db.CommunityNeeds
            .AsNoTracking()
            .Include(need => need.Recipient)
            .Include(need => need.Contributions)
            .Where(need => need.ReviewStatus == "Approved" && (need.Status == "Open" || need.Status == "PartiallyFulfilled"))
            .Where(need => need.Recipient.VerificationStatus == "Approved")
            .OrderByDescending(need => need.CreatedAt)
            .Select(need => need));

    public static readonly Func<HungerDbContext, IAsyncEnumerable<CommunityNeedEntity>> AllNeeds =
        EF.CompileAsyncQuery((HungerDbContext db) => db.CommunityNeeds
            .AsNoTracking()
            .Include(need => need.Recipient)
            .Include(need => need.Contributions)
            .OrderByDescending(need => need.CreatedAt)
            .Select(need => need));

    public static readonly Func<HungerDbContext, IAsyncEnumerable<ReviewableAccountRecord>> ReviewableAccounts =
        EF.CompileAsyncQuery((HungerDbContext db) => db.Accounts
            .AsNoTracking()
            .Where(account => account.Role == "Donor" || account.Role == "Recipient")
            .OrderBy(account => account.VerificationStatus == "Pending" ? 0 : account.VerificationStatus == "Rejected" ? 1 : 2)
            .ThenByDescending(account => account.CreatedAt)
            .Select(account => new ReviewableAccountRecord(
                account.Id,
                account.Name,
                account.Email,
                account.OrganizationName,
                account.Role,
                             account.VerificationStatus,
                             account.CreatedAt,
                             db.VerificationDocuments.Any(document => document.AccountId == account.Id))));

                public static readonly Func<HungerDbContext, Guid, IAsyncEnumerable<VerificationDocumentRecord>> VerificationDocumentByAccountId =
                    EF.CompileAsyncQuery((HungerDbContext db, Guid accountId) => db.VerificationDocuments
                        .AsNoTracking()
                        .Where(document => document.AccountId == accountId && (document.Account.Role == "Donor" || document.Account.Role == "Recipient"))
                        .Select(document => new VerificationDocumentRecord(document.Contents, document.ContentType)));

    public static readonly Func<HungerDbContext, IAsyncEnumerable<DashboardSummary>> AdminSummary =
        EF.CompileAsyncQuery((HungerDbContext db) => db.Donations
            .AsNoTracking()
            .GroupBy(_ => 1)
            .Select(group => new DashboardSummary(
                group.Count(),
                group.Count(donation => donation.Status == "Available" || donation.Status == "Requested" || donation.Status == "Accepted" || donation.Status == "PickedUp"),
                group.Count(donation => donation.Status == "Accepted" || donation.Status == "PickedUp"),
                group.Count(donation => donation.Status == "Delivered"))));

    public static readonly Func<HungerDbContext, Guid, IAsyncEnumerable<DashboardSummary>> DonorSummary =
        EF.CompileAsyncQuery((HungerDbContext db, Guid donorId) => db.Donations
            .AsNoTracking()
            .Where(donation => donation.CreatedBy == donorId)
            .GroupBy(_ => 1)
            .Select(group => new DashboardSummary(
                group.Count(),
                group.Count(donation => donation.Status == "Available" || donation.Status == "Requested" || donation.Status == "Accepted" || donation.Status == "PickedUp"),
                group.Count(donation => donation.Status == "Accepted" || donation.Status == "PickedUp"),
                group.Count(donation => donation.Status == "Delivered"))));

    public static readonly Func<HungerDbContext, Guid, IAsyncEnumerable<DashboardSummary>> RecipientSummary =
        EF.CompileAsyncQuery((HungerDbContext db, Guid recipientId) => db.Donations
            .AsNoTracking()
            .Where(donation => donation.RecipientId == recipientId || donation.Requests.Any(request => request.RecipientId == recipientId))
            .GroupBy(_ => 1)
            .Select(group => new DashboardSummary(
                group.Count(),
                group.Count(donation => donation.Status == "Available" || donation.Status == "Requested" || donation.Status == "Accepted" || donation.Status == "PickedUp"),
                group.Count(donation => donation.Status == "Accepted" || donation.Status == "PickedUp"),
                group.Count(donation => donation.Status == "Delivered"))));

    public static readonly Func<HungerDbContext, Guid, Guid, IAsyncEnumerable<DonationEntity>> DonorDonationById =
        EF.CompileAsyncQuery((HungerDbContext db, Guid donorId, Guid donationId) => db.Donations
            .AsNoTracking()
            .Include(donation => donation.Donor)
            .Include(donation => donation.Recipient)
            .Include(donation => donation.Requests)
                .ThenInclude(request => request.Recipient)
            .Where(donation => donation.CreatedBy == donorId && donation.Id == donationId));

    public static readonly Func<HungerDbContext, Guid, Guid, IAsyncEnumerable<DonationEntity>> RecipientDonationById =
        EF.CompileAsyncQuery((HungerDbContext db, Guid recipientId, Guid donationId) => db.Donations
            .AsNoTracking()
            .Include(donation => donation.Donor)
            .Include(donation => donation.Recipient)
            .Include(donation => donation.Requests)
                .ThenInclude(request => request.Recipient)
            .Where(donation => donation.Id == donationId &&
                ((donation.Status == "Available" && donation.ReviewStatus == "Approved") ||
                 donation.RecipientId == recipientId || donation.Requests.Any(request => request.RecipientId == recipientId)) &&
                donation.Donor.VerificationStatus == "Approved"));

    public static readonly Func<HungerDbContext, Guid, IAsyncEnumerable<DonationEntity>> AdminDonationById =
        EF.CompileAsyncQuery((HungerDbContext db, Guid donationId) => db.Donations
            .AsNoTracking()
            .Include(donation => donation.Donor)
            .Include(donation => donation.Recipient)
            .Include(donation => donation.Requests)
                .ThenInclude(request => request.Recipient)
            .Where(donation => donation.Id == donationId));
}
