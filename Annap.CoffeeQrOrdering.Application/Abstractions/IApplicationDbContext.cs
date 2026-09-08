using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Domain.Entities.KiotViet;
using Microsoft.EntityFrameworkCore;

namespace Annap.CoffeeQrOrdering.Application.Abstractions;

public interface IApplicationDbContext
{
    DbSet<MenuCategory> MenuCategories { get; }
    DbSet<MenuItem> MenuItems { get; }
    DbSet<VenueTable> VenueTables { get; }
    DbSet<Order> Orders { get; }
    DbSet<OrderItem> OrderItems { get; }
    DbSet<ChatSession> ChatSessions { get; }
    DbSet<ChatMessage> ChatMessages { get; }
    DbSet<Ingredient> Ingredients { get; }
    DbSet<MenuItemIngredient> MenuItemIngredients { get; }
    DbSet<SommelierSuggestionFeedback> SommelierSuggestionFeedbacks { get; }
    DbSet<OperationalAuditEntry> OperationalAuditEntries { get; }
    DbSet<ExperienceSignatureSlot> ExperienceSignatureSlots { get; }
    DbSet<ExperienceDiscoverySettings> ExperienceDiscoverySettings { get; }
    DbSet<ExperienceGuidedQuestion> ExperienceGuidedQuestions { get; }
    DbSet<ExperienceGuidedOption> ExperienceGuidedOptions { get; }
    DbSet<ExperienceGuidedAffinity> ExperienceGuidedAffinities { get; }
    DbSet<ExperienceSnapshot> ExperienceSnapshots { get; }
    DbSet<ExperiencePublishRecord> ExperiencePublishRecords { get; }
    DbSet<ExperienceGroupSettings> ExperienceGroupSettings { get; }
    DbSet<HomepageExperienceSettings> HomepageExperienceSettings { get; }
    DbSet<AppNetworkSettings> AppNetworkSettings { get; }
    DbSet<KiotVietOutboxMessage> KiotVietOutboxMessages { get; }
    DbSet<KiotVietSyncLog> KiotVietSyncLogs { get; }
    DbSet<KiotVietProductMapping> KiotVietProductMappings { get; }
    DbSet<PaymentConfirmation> PaymentConfirmations { get; }
    DbSet<StaffAccount> StaffAccounts { get; }
    DbSet<ShiftClose> ShiftCloses { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

