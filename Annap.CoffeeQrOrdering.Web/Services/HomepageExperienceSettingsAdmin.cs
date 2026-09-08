using Annap.CoffeeQrOrdering.Application.Abstractions;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Annap.CoffeeQrOrdering.Web.Services;

public static class HomepageExperienceSettingsAdmin
{
    public sealed class FormModel
    {
        public bool IsGroupEnabled { get; set; } = true;

        public bool IsSoloEnabled { get; set; } = true;

        public bool IsSommelierEnabled { get; set; } = true;
    }

    public static int VisibleCount(FormModel form) =>
        (form.IsGroupEnabled ? 1 : 0) + (form.IsSoloEnabled ? 1 : 0) + (form.IsSommelierEnabled ? 1 : 0);

    public static async Task<FormModel> LoadAsync(IApplicationDbContext db, CancellationToken cancellationToken = default)
    {
        var sid = HomepageExperienceSettingsConfiguration.SingletonId;
        var row = await db.HomepageExperienceSettings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == sid, cancellationToken);
        return row is null
            ? new FormModel()
            : new FormModel
            {
                IsGroupEnabled = row.IsGroupEnabled,
                IsSoloEnabled = row.IsSoloEnabled,
                IsSommelierEnabled = row.IsSommelierEnabled
            };
    }

    public static async Task SaveAsync(IApplicationDbContext db, FormModel form, CancellationToken cancellationToken = default)
    {
        var sid = HomepageExperienceSettingsConfiguration.SingletonId;
        var existing = await db.HomepageExperienceSettings.FirstOrDefaultAsync(x => x.Id == sid, cancellationToken);

        if (existing is null)
        {
            db.HomepageExperienceSettings.Add(new HomepageExperienceSettings
            {
                Id = sid,
                IsGroupEnabled = form.IsGroupEnabled,
                IsSoloEnabled = form.IsSoloEnabled,
                IsSommelierEnabled = form.IsSommelierEnabled,
                CreatedAtUtc = DateTimeOffset.UtcNow
            });
        }
        else
        {
            existing.IsGroupEnabled = form.IsGroupEnabled;
            existing.IsSoloEnabled = form.IsSoloEnabled;
            existing.IsSommelierEnabled = form.IsSommelierEnabled;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
