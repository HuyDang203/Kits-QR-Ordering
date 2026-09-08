using Annap.CoffeeQrOrdering.Application.Abstractions;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Annap.CoffeeQrOrdering.Infrastructure.Services;

public sealed class MenuInventoryGate(IApplicationDbContext db) : IMenuInventoryGate
{
    public async Task<IReadOnlySet<Guid>> GetStockBlockedMenuItemIdsAsync(CancellationToken cancellationToken = default)
    {
        var ingredients = await db.Ingredients.AsNoTracking().ToListAsync(cancellationToken);
        if (ingredients.Count == 0)
            return new HashSet<Guid>();

        var ingById = ingredients.ToDictionary(i => i.Id);
        var links = await db.MenuItemIngredients.AsNoTracking().ToListAsync(cancellationToken);
        if (links.Count == 0)
            return new HashSet<Guid>();

        var blocked = new HashSet<Guid>();
        foreach (var g in links.GroupBy(l => l.MenuItemId))
        {
            foreach (var line in g)
            {
                if (!ingById.TryGetValue(line.IngredientId, out var ing))
                {
                    blocked.Add(g.Key);
                    break;
                }

                if (!ing.IsActive || ing.CurrentStock < line.QuantityRequired)
                {
                    blocked.Add(g.Key);
                    break;
                }
            }
        }

        return blocked;
    }
}
