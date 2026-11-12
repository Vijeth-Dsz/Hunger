namespace Hunger.Api.Models;

public enum UserRole { Donor, Recipient, Admin }
public enum VerificationStatus { Pending, Approved }
public enum DonationType { PreparedFood, Groceries }
public enum DonationStatus { Available, Requested, Accepted, PickedUp, Delivered, Cancelled }

public sealed record Account(Guid Id, string Name, string Email, string OrganizationName, UserRole Role, VerificationStatus VerificationStatus);
public sealed record RegisterRequest(string? Name, string? Email, string? OrganizationName, string? Role, string? Password);
public sealed record LoginRequest(string? Email, string? Password);
public sealed record CreateDonationRequest(string? Title, string? Description, string? Type, string? Quantity, string? Unit, string? PickupAddress, DateTimeOffset PickupFrom, DateTimeOffset PickupUntil, string? StorageInstructions, string? Allergens);
public sealed record UpdateDonationStatusRequest(string? Status);
public sealed record DonationView(Guid Id, Guid CreatedBy, Guid? RecipientId, string Title, string Description, DonationType Type, string Quantity, string? Unit, string PickupAddress, DateTimeOffset PickupFrom, DateTimeOffset PickupUntil, string? StorageInstructions, string? Allergens, DonationStatus Status, DateTimeOffset CreatedAt, string DonorOrganization, string RecipientOrganization);
public sealed record DashboardSummary(int Total, int Active, int Delivered);
public sealed record PendingRecipient(Guid Id, string Name, string Email, string OrganizationName, DateTimeOffset CreatedAt);
public sealed record StatusHistoryItem(string Status, DateTimeOffset ChangedAt, string ChangedBy);
