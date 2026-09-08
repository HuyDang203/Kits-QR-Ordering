using Annap.CoffeeQrOrdering.Domain.Common;

namespace Annap.CoffeeQrOrdering.Domain.Entities;

/// <summary>Singleton CMS row for the seated “group” hospitality path (guest count, copy, limits).</summary>
public sealed class ExperienceGroupSettings : AuditableEntity
{
    /// <summary>Short line above the guest-count step.</summary>
    public string ArrivalKicker { get; set; } = "";

    public string GuestCountPrompt { get; set; } = "How many guests are joining?";

    public string? GuestCountLead { get; set; }

    public int MinGuests { get; set; } = 1;

    public int MaxGuests { get; set; } = 8;

    public string? GuestTabsIntro { get; set; }

    public string? GuestDoneHint { get; set; }

    public string SummaryHeadline { get; set; } = "Your table";

    public string? SummaryLead { get; set; }

    public string? HospitalityClosing { get; set; }
}
