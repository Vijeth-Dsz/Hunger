using Hunger.Api.Data;

namespace Hunger.Api.Models;

public static class ContractMappings
{
    public static Account ToAccount(AccountEntity account) => new(
        account.Id,
        account.Name,
        account.Email,
        account.OrganizationName,
        Enum.Parse<UserRole>(account.Role),
        Enum.Parse<VerificationStatus>(account.VerificationStatus));

    public static DonationView ToDonation(DonationEntity donation, bool recipientView = false)
    {
        var recipientOrganization = donation.Recipient?.OrganizationName ?? donation.Requests
            .Where(request => request.Status == "Pending")
            .OrderBy(request => request.RequestedAt)
            .Select(request => request.Recipient.OrganizationName)
            .FirstOrDefault() ?? string.Empty;
        var showPickupAddress = !recipientView || donation.Status is "Accepted" or "PickedUp" or "Delivered";

        return new DonationView(
            donation.Id,
            donation.CreatedBy,
            donation.RecipientId,
            donation.Title,
            donation.Description,
            Enum.Parse<DonationType>(donation.Type),
            donation.Quantity,
            donation.Unit,
            showPickupAddress ? donation.PickupAddress : "Available after request acceptance",
            donation.PickupFrom,
            donation.PickupUntil,
            donation.StorageInstructions,
            donation.Allergens,
            Enum.Parse<DonationStatus>(donation.Status),
            donation.CreatedAt,
            donation.Donor.OrganizationName,
            recipientOrganization,
            donation.ExpiresAt,
            Enum.Parse<ReviewStatus>(donation.ReviewStatus));
    }

    public static NeedView ToNeed(CommunityNeedEntity need) => new(
        need.Id,
        need.RecipientId,
        need.Title,
        need.Description,
        Enum.Parse<DonationType>(need.Type),
        need.Quantity,
        need.Unit,
        need.NeededBy,
        Enum.Parse<NeedStatus>(need.Status),
        Enum.Parse<ReviewStatus>(need.ReviewStatus),
        need.Contributions.Where(contribution => contribution.Status == "Delivered").Sum(contribution => contribution.Quantity),
        need.Recipient.OrganizationName,
        need.CreatedAt);

    public static NeedContributionView ToContribution(NeedContributionEntity contribution) => new(
        contribution.Id,
        contribution.NeedId,
        contribution.DonorId,
        contribution.Quantity,
        contribution.Unit,
        contribution.PickupAddress,
        contribution.PickupFrom,
        contribution.PickupUntil,
        contribution.ExpiresAt,
        contribution.StorageInstructions,
        contribution.Allergens,
        Enum.Parse<ContributionStatus>(contribution.Status),
        contribution.Donor.OrganizationName,
        contribution.Need.Recipient.OrganizationName,
        contribution.Need.Title,
        contribution.CreatedAt);
}
