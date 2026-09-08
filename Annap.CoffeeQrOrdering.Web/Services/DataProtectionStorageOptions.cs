namespace Annap.CoffeeQrOrdering.Web.Services;

public sealed class DataProtectionStorageOptions
{
    public const string SectionName = "DataProtection";

    public string KeysPath { get; set; } = "";
    public string ApplicationName { get; set; } = "Annap.CoffeeQrOrdering";
}
