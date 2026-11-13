namespace Hunger.Api.Data;

public sealed class RoleEntity
{
    public string RoleKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool RegistrationAllowed { get; set; }
    public bool IsEnabled { get; set; }
    public int SortOrder { get; set; }
}

public sealed class AccountEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string OrganizationName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string VerificationStatus { get; set; } = string.Empty;
    public byte[] PasswordSalt { get; set; } = [];
    public byte[] PasswordHash { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; }
    public RoleEntity RoleDefinition { get; set; } = null!;
    public VerificationDocumentEntity? VerificationDocumentRecord { get; set; }
}

public sealed class VerificationDocumentEntity
{
    public Guid AccountId { get; set; }
    public byte[] Contents { get; set; } = [];
    public string ContentType { get; set; } = string.Empty;
    public AccountEntity Account { get; set; } = null!;
}

public sealed class DonationEntity
{
    public Guid Id { get; set; }
    public Guid CreatedBy { get; set; }
    public Guid? RecipientId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Quantity { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public string PickupAddress { get; set; } = string.Empty;
    public DateTimeOffset PickupFrom { get; set; }
    public DateTimeOffset PickupUntil { get; set; }
    public string? StorageInstructions { get; set; }
    public string? Allergens { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public string ReviewStatus { get; set; } = string.Empty;
    public string? ReviewNotes { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public AccountEntity Donor { get; set; } = null!;
    public AccountEntity? Recipient { get; set; }
    public ICollection<DonationRequestEntity> Requests { get; set; } = new List<DonationRequestEntity>();
}

public sealed class DonationRequestEntity
{
    public Guid Id { get; set; }
    public Guid DonationId { get; set; }
    public Guid RecipientId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset RequestedAt { get; set; }
    public DateTimeOffset? RespondedAt { get; set; }
    public DonationEntity Donation { get; set; } = null!;
    public AccountEntity Recipient { get; set; } = null!;
}

public sealed class DonationStatusHistoryEntity
{
    public Guid Id { get; set; }
    public Guid DonationId { get; set; }
    public string Status { get; set; } = string.Empty;
    public Guid ChangedBy { get; set; }
    public DateTimeOffset ChangedAt { get; set; }
    public DonationEntity Donation { get; set; } = null!;
    public AccountEntity ChangedByAccount { get; set; } = null!;
}

public sealed class CommunityNeedEntity
{
    public Guid Id { get; set; }
    public Guid RecipientId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public DateTimeOffset NeededBy { get; set; }
    public string Status { get; set; } = string.Empty;
    public string ReviewStatus { get; set; } = string.Empty;
    public string? ReviewNotes { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public AccountEntity Recipient { get; set; } = null!;
    public ICollection<NeedContributionEntity> Contributions { get; set; } = new List<NeedContributionEntity>();
}

public sealed class NeedContributionEntity
{
    public Guid Id { get; set; }
    public Guid NeedId { get; set; }
    public Guid DonorId { get; set; }
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public string PickupAddress { get; set; } = string.Empty;
    public DateTimeOffset PickupFrom { get; set; }
    public DateTimeOffset PickupUntil { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public string? StorageInstructions { get; set; }
    public string? Allergens { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ReviewNotes { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedAt { get; set; }
    public DateTimeOffset? ReceivedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public CommunityNeedEntity Need { get; set; } = null!;
    public AccountEntity Donor { get; set; } = null!;
}
