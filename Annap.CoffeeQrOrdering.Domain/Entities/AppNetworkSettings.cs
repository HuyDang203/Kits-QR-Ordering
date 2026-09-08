using Annap.CoffeeQrOrdering.Domain.Common;

namespace Annap.CoffeeQrOrdering.Domain.Entities;

/// <summary>Singleton row: optional public base URL override for QR and absolute links (admin /admin/system/network).</summary>
public sealed class AppNetworkSettings : AuditableEntity
{
    public static readonly Guid SingletonId = Guid.Parse("cccccccc-cccc-cccc-cccc-ccccccccccc1");

    /// <summary>When set, wins over appsettings AppUrl:PublicBaseUrl for the app URL resolver.</summary>
    public string? PublicBaseUrlOverride { get; set; }
}
