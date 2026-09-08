using Annap.CoffeeQrOrdering.Application.Abstractions;
using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Annap.CoffeeQrOrdering.Web.Pages.Admin.Menu;

[Authorize(Policy = "Staff")]
public sealed class IndexModel(IApplicationDbContext db) : PageModel
{
    public IReadOnlyList<MenuRowVm> Items { get; private set; } = Array.Empty<MenuRowVm>();

    public IReadOnlyList<CategoryOptionVm> Categories { get; private set; } = Array.Empty<CategoryOptionVm>();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var categories = await db.MenuCategories.AsNoTracking()
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .Select(c => new CategoryOptionVm(c.Id, c.Name, c.SortOrder))
            .ToListAsync(cancellationToken);

        var rows = await db.MenuItems.AsNoTracking()
            .Include(i => i.Category)
            .OrderBy(i => i.IsArchived)
            .ThenBy(i => i.DisplaySortOrder)
            .ThenBy(i => i.Name)
            .ToListAsync(cancellationToken);

        Items = rows.Select(BuildRow).ToList();
        Categories = categories;
    }

    private static MenuRowVm BuildRow(MenuItem item)
    {
        var catName = item.Category?.Name ?? "—";
        var preview = MenuMediaResolver.ResolveCardImageUrl(item, catName);
        return new MenuRowVm(
            item.Id,
            item.Name,
            item.Subtitle,
            item.CategoryId,
            catName,
            item.Price,
            item.IsAvailable,
            item.IsFeatured,
            item.IsSignature,
            item.IsArchived,
            preview);
    }

    public sealed record CategoryOptionVm(Guid Id, string Name, int SortOrder);

    public sealed record MenuRowVm(
        Guid Id,
        string Name,
        string? Subtitle,
        Guid CategoryId,
        string CategoryName,
        decimal Price,
        bool IsAvailable,
        bool IsFeatured,
        bool IsSignature,
        bool IsArchived,
        string PreviewImageUrl);
}