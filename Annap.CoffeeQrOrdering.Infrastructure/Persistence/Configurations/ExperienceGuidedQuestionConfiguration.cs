using Annap.CoffeeQrOrdering.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Annap.CoffeeQrOrdering.Infrastructure.Persistence.Configurations;

public sealed class ExperienceGuidedQuestionConfiguration : IEntityTypeConfiguration<ExperienceGuidedQuestion>
{
    public void Configure(EntityTypeBuilder<ExperienceGuidedQuestion> builder)
    {
        builder.ToTable("experience_guided_questions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ExternalKey).HasMaxLength(64).IsRequired();
        builder.Property(x => x.SetKey).HasMaxLength(100).IsRequired().HasDefaultValue("");
        builder.Property(x => x.Prompt).HasMaxLength(600).IsRequired();
        builder.Property(x => x.PromptEn).HasMaxLength(600);
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.DescriptionEn).HasMaxLength(2000);

        builder.HasIndex(x => new { x.SetKey, x.ExternalKey }).IsUnique();
        builder.HasIndex(x => x.SortOrder);

        builder.HasMany(x => x.Options)
            .WithOne(x => x.Question)
            .HasForeignKey(x => x.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
