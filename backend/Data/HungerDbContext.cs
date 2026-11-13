using Microsoft.EntityFrameworkCore;

namespace Hunger.Api.Data;

public sealed class HungerDbContext(DbContextOptions<HungerDbContext> options) : DbContext(options)
{
    public DbSet<RoleEntity> Roles => Set<RoleEntity>();
    public DbSet<AccountEntity> Accounts => Set<AccountEntity>();
    public DbSet<VerificationDocumentEntity> VerificationDocuments => Set<VerificationDocumentEntity>();
    public DbSet<DonationEntity> Donations => Set<DonationEntity>();
    public DbSet<DonationRequestEntity> DonationRequests => Set<DonationRequestEntity>();
    public DbSet<DonationStatusHistoryEntity> DonationStatusHistory => Set<DonationStatusHistoryEntity>();
    public DbSet<CommunityNeedEntity> CommunityNeeds => Set<CommunityNeedEntity>();
    public DbSet<NeedContributionEntity> NeedContributions => Set<NeedContributionEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RoleEntity>(entity =>
        {
            entity.ToTable("Roles");
            entity.HasKey(role => role.RoleKey);
            entity.Property(role => role.RoleKey).HasMaxLength(20);
            entity.Property(role => role.DisplayName).HasMaxLength(80).IsRequired();
            entity.Property(role => role.Description).HasMaxLength(200).IsRequired();
        });

        modelBuilder.Entity<AccountEntity>(entity =>
        {
            entity.ToTable("Accounts");
            entity.HasKey(account => account.Id);
            entity.HasIndex(account => account.Email).IsUnique();
            entity.Property(account => account.Name).HasMaxLength(120).IsRequired();
            entity.Property(account => account.Email).HasMaxLength(254).IsRequired();
            entity.Property(account => account.OrganizationName).HasMaxLength(160).IsRequired();
            entity.Property(account => account.Role).HasMaxLength(20).IsRequired();
            entity.Property(account => account.VerificationStatus).HasMaxLength(20).IsRequired();
            entity.Property(account => account.PasswordSalt).HasColumnType("varbinary(16)");
            entity.Property(account => account.PasswordHash).HasColumnType("varbinary(32)");
            entity.HasOne(account => account.RoleDefinition)
                .WithMany()
                .HasForeignKey(account => account.Role)
                .HasPrincipalKey(role => role.RoleKey)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(account => account.VerificationDocumentRecord)
                .WithOne(document => document.Account)
                .HasForeignKey<VerificationDocumentEntity>(document => document.AccountId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<VerificationDocumentEntity>(entity =>
        {
            entity.ToTable("VerificationDocuments");
            entity.HasKey(document => document.AccountId);
            entity.Property(document => document.Contents).HasColumnType("varbinary(max)").IsRequired();
            entity.Property(document => document.ContentType).HasMaxLength(100).IsRequired();
        });

        modelBuilder.Entity<DonationEntity>(entity =>
        {
            entity.ToTable("Donations");
            entity.HasKey(donation => donation.Id);
            entity.Property(donation => donation.Title).HasMaxLength(160).IsRequired();
            entity.Property(donation => donation.Description).HasMaxLength(1200).IsRequired();
            entity.Property(donation => donation.Type).HasMaxLength(20).IsRequired();
            entity.Property(donation => donation.Quantity).HasMaxLength(80).IsRequired();
            entity.Property(donation => donation.Unit).HasMaxLength(40);
            entity.Property(donation => donation.PickupAddress).HasMaxLength(300).IsRequired();
            entity.Property(donation => donation.StorageInstructions).HasMaxLength(500);
            entity.Property(donation => donation.Allergens).HasMaxLength(500);
            entity.Property(donation => donation.ReviewStatus).HasMaxLength(20).IsRequired();
            entity.Property(donation => donation.ReviewNotes).HasMaxLength(500);
            entity.Property(donation => donation.Status).HasMaxLength(20).IsRequired();
            entity.HasOne(donation => donation.Donor).WithMany().HasForeignKey(donation => donation.CreatedBy).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(donation => donation.Recipient).WithMany().HasForeignKey(donation => donation.RecipientId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DonationRequestEntity>(entity =>
        {
            entity.ToTable("DonationRequests");
            entity.HasKey(request => request.Id);
            entity.Property(request => request.Status).HasMaxLength(20).IsRequired();
            entity.HasOne(request => request.Donation).WithMany(donation => donation.Requests).HasForeignKey(request => request.DonationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(request => request.Recipient).WithMany().HasForeignKey(request => request.RecipientId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DonationStatusHistoryEntity>(entity =>
        {
            entity.ToTable("DonationStatusHistory");
            entity.HasKey(history => history.Id);
            entity.Property(history => history.Status).HasMaxLength(20).IsRequired();
            entity.HasOne(history => history.Donation).WithMany().HasForeignKey(history => history.DonationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(history => history.ChangedByAccount).WithMany().HasForeignKey(history => history.ChangedBy).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CommunityNeedEntity>(entity =>
        {
            entity.ToTable("CommunityNeeds");
            entity.HasKey(need => need.Id);
            entity.Property(need => need.Title).HasMaxLength(160).IsRequired();
            entity.Property(need => need.Description).HasMaxLength(1200).IsRequired();
            entity.Property(need => need.Type).HasMaxLength(20).IsRequired();
            entity.Property(need => need.Quantity).HasPrecision(18, 2);
            entity.Property(need => need.Unit).HasMaxLength(40).IsRequired();
            entity.Property(need => need.Status).HasMaxLength(30).IsRequired();
            entity.Property(need => need.ReviewStatus).HasMaxLength(20).IsRequired();
            entity.Property(need => need.ReviewNotes).HasMaxLength(500);
            entity.HasOne(need => need.Recipient).WithMany().HasForeignKey(need => need.RecipientId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<NeedContributionEntity>(entity =>
        {
            entity.ToTable("NeedContributions");
            entity.HasKey(contribution => contribution.Id);
            entity.Property(contribution => contribution.Quantity).HasPrecision(18, 2);
            entity.Property(contribution => contribution.Unit).HasMaxLength(40).IsRequired();
            entity.Property(contribution => contribution.PickupAddress).HasMaxLength(300).IsRequired();
            entity.Property(contribution => contribution.StorageInstructions).HasMaxLength(500);
            entity.Property(contribution => contribution.Allergens).HasMaxLength(500);
            entity.Property(contribution => contribution.Status).HasMaxLength(20).IsRequired();
            entity.Property(contribution => contribution.ReviewNotes).HasMaxLength(500);
            entity.HasOne(contribution => contribution.Need).WithMany(need => need.Contributions).HasForeignKey(contribution => contribution.NeedId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(contribution => contribution.Donor).WithMany().HasForeignKey(contribution => contribution.DonorId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
