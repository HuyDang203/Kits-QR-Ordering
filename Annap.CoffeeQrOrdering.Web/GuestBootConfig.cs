using System.Text.Json.Serialization;

namespace Annap.CoffeeQrOrdering.Web;

/// <summary>
/// Effective guest boot flags after merging Development diagnostics and optional <c>?safe=</c> ladder.
/// Serialized to <c>window.AnnapGuestBoot</c> for client boot isolation.
/// </summary>
public sealed class GuestBootConfig
{
    [JsonPropertyName("disableSommelierBoot")]
    public bool DisableSommelierBoot { get; set; }

    [JsonPropertyName("disableSignalR")]
    public bool DisableSignalR { get; set; }

    [JsonPropertyName("disableGuestQueue")]
    public bool DisableGuestQueue { get; set; }

    [JsonPropertyName("disableI18n")]
    public bool DisableI18n { get; set; }

    [JsonPropertyName("disableMoodCatalog")]
    public bool DisableMoodCatalog { get; set; }

    [JsonPropertyName("disableCartHydration")]
    public bool DisableCartHydration { get; set; }

    [JsonPropertyName("disableGuestAnimations")]
    public bool DisableGuestAnimations { get; set; }

    [JsonPropertyName("disableGuestObservers")]
    public bool DisableGuestObservers { get; set; }

    /// <summary>Show the floating boot checklist when <see cref="GuestBootResolver"/> enables it (Development + opt-in).</summary>
    [JsonPropertyName("showBootChecklist")]
    public bool ShowBootChecklist { get; set; }

    [JsonPropertyName("safeQuery")]
    public string SafeQuery { get; set; } = "";
}
