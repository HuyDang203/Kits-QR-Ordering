using Annap.CoffeeQrOrdering.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Annap.CoffeeQrOrdering.Infrastructure.Persistence.Configurations;

public sealed class SommelierSuggestionFeedbackConfiguration : IEntityTypeConfiguration<SommelierSuggestionFeedback>
{
    public void Configure(EntityTypeBuilder<SommelierSuggestionFeedback> builder)
    {
        builder.ToTable("sommelier_suggestion_feedback");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Outcome).HasMaxLength(32).IsRequired();
        builder.Property(x => x.MoodKey).HasMaxLength(64);
        builder.Property(x => x.RefinementKey).HasMaxLength(64);
        builder.HasIndex(x => x.SessionId);
        builder.HasIndex(x => x.MenuItemId);
    }
}
