using Annap.CoffeeQrOrdering.Application.Abstractions;
using Annap.CoffeeQrOrdering.Domain.Common;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Domain.Entities.KiotViet;
using Microsoft.EntityFrameworkCore;

namespace Annap.CoffeeQrOrdering.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<MenuCategory> MenuCategories => Set<MenuCategory>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<VenueTable> VenueTables => Set<VenueTable>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<MenuItemIngredient> MenuItemIngredients => Set<MenuItemIngredient>();
    public DbSet<SommelierSuggestionFeedback> SommelierSuggestionFeedbacks => Set<SommelierSuggestionFeedback>();
    public DbSet<OperationalAuditEntry> OperationalAuditEntries => Set<OperationalAuditEntry>();
    public DbSet<ExperienceSignatureSlot> ExperienceSignatureSlots => Set<ExperienceSignatureSlot>();
    public DbSet<ExperienceDiscoverySettings> ExperienceDiscoverySettings => Set<ExperienceDiscoverySettings>();
    public DbSet<ExperienceGuidedQuestion> ExperienceGuidedQuestions => Set<ExperienceGuidedQuestion>();
    public DbSet<ExperienceGuidedOption> ExperienceGuidedOptions => Set<ExperienceGuidedOption>();
    public DbSet<ExperienceGuidedAffinity> ExperienceGuidedAffinities => Set<ExperienceGuidedAffinity>();
    public DbSet<ExperienceSnapshot> ExperienceSnapshots => Set<ExperienceSnapshot>();
    public DbSet<ExperiencePublishRecord> ExperiencePublishRecords => Set<ExperiencePublishRecord>();
    public DbSet<ExperienceGroupSettings> ExperienceGroupSettings => Set<ExperienceGroupSettings>();
    public DbSet<HomepageExperienceSettings> HomepageExperienceSettings => Set<HomepageExperienceSettings>();
    public DbSet<AppNetworkSettings> AppNetworkSettings => Set<AppNetworkSettings>();
    public DbSet<KiotVietOutboxMessage> KiotVietOutboxMessages => Set<KiotVietOutboxMessage>();
    public DbSet<KiotVietSyncLog> KiotVietSyncLogs => Set<KiotVietSyncLog>();
    public DbSet<KiotVietProductMapping> KiotVietProductMappings => Set<KiotVietProductMapping>();
    public DbSet<PaymentConfirmation> PaymentConfirmations => Set<PaymentConfirmation>();
    public DbSet<StaffAccount> StaffAccounts => Set<StaffAccount>();
    public DbSet<ShiftClose> ShiftCloses => Set<ShiftClose>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAtUtc = now;
                entry.Entity.UpdatedAtUtc = null;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAtUtc = now;
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}

