using Annap.CoffeeQrOrdering.Domain.Common;

namespace Annap.CoffeeQrOrdering.Domain.Entities;

public sealed class MenuCategory : AuditableEntity
{
    public string Name { get; set; } = null!;
    public int SortOrder { get; set; }

    public List<MenuItem> Items { get; set; } = [];
}

