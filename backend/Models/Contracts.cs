namespace Hunger.Api.Models;

public enum UserRole { Donor, Recipient, Admin }
public enum VerificationStatus { Pending, Approved, Rejected }
public enum DonationType { PreparedFood, Groceries, Stationery }
public enum DonationStatus { Available, Requested, Accepted, PickedUp, Delivered, Cancelled }
public enum ReviewStatus { Pending, Approved, Rejected }
public enum NeedStatus { Open, PartiallyFulfilled, Fulfilled, Closed }
public enum ContributionStatus { PendingReview, Approved, Delivered, Rejected, Cancelled }

public sealed record Account(Guid Id, string Name, string Email, string OrganizationName, UserRole Role, VerificationStatus VerificationStatus);
public sealed record RegisterRequest(string? Name, string? Email, string? OrganizationName, string? Role, string? Password, IFormFile? VerificationDocument);
public sealed record LoginRequest(string? Email, string? Password);
public sealed record RoleOption(string RoleKey, string DisplayName, string Description, bool RegistrationAllowed);
public sealed record CreateDonationRequest(string? Title, string? Description, string? Type, string? Quantity, string? Unit, string? PickupAddress, DateTimeOffset PickupFrom, DateTimeOffset PickupUntil, string? StorageInstructions, string? Allergens, DateTimeOffset? ExpiresAt);
public sealed record UpdateDonationStatusRequest(string? Status);
public sealed record DonationView(Guid Id, Guid CreatedBy, Guid? RecipientId, string Title, string Description, DonationType Type, string Quantity, string? Unit, string PickupAddress, DateTimeOffset PickupFrom, DateTimeOffset PickupUntil, string? StorageInstructions, string? Allergens, DonationStatus Status, DateTimeOffset CreatedAt, string DonorOrganization, string RecipientOrganization, DateTimeOffset? ExpiresAt, ReviewStatus ReviewStatus);
public sealed record CreateNeedRequest(string? Title, string? Description, string? Type, decimal Quantity, string? Unit, DateTimeOffset NeededBy);
public sealed record NeedView(Guid Id, Guid RecipientId, string Title, string Description, DonationType Type, decimal Quantity, string Unit, DateTimeOffset NeededBy, NeedStatus Status, ReviewStatus ReviewStatus, decimal FulfilledQuantity, string RecipientOrganization, DateTimeOffset CreatedAt);
public sealed record CreateContributionRequest(decimal Quantity, string? Unit, string? PickupAddress, DateTimeOffset PickupFrom, DateTimeOffset PickupUntil, DateTimeOffset? ExpiresAt, string? StorageInstructions, string? Allergens);
public sealed record NeedContributionView(Guid Id, Guid NeedId, Guid DonorId, decimal Quantity, string Unit, string PickupAddress, DateTimeOffset PickupFrom, DateTimeOffset PickupUntil, DateTimeOffset? ExpiresAt, string? StorageInstructions, string? Allergens, ContributionStatus Status, string DonorOrganization, string RecipientOrganization, string NeedTitle, DateTimeOffset CreatedAt);
public sealed record AdminAccountView(Guid Id, string Name, string Email, string OrganizationName, UserRole Role, VerificationStatus VerificationStatus, DateTimeOffset CreatedAt, bool HasVerificationDocument);
public sealed record ReviewableAccountRecord(Guid Id, string Name, string Email, string OrganizationName, string Role, string VerificationStatus, DateTimeOffset CreatedAt, bool HasVerificationDocument);
public sealed record VerificationDocumentRecord(byte[]? Contents, string? ContentType);
public sealed record ModerateRequest(string? Status, string? Notes);
public sealed record DashboardSummary(int Total, int Active, int Accepted, int Delivered);
public sealed record StatusHistoryItem(string Status, DateTimeOffset ChangedAt, string ChangedBy);
