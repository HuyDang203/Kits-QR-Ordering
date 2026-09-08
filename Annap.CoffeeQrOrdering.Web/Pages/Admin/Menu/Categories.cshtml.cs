using Annap.CoffeeQrOrdering.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Annap.CoffeeQrOrdering.Web.Pages.Admin.Menu;

[Authorize(Policy = "Staff")]
public sealed class CategoriesModel(IApplicationDbContext db) : PageModel
{
  public sealed record CategoryRow(Guid Id, string Name, int SortOrder);

  public IReadOnlyList<CategoryRow> Categories { get; private set; } = Array.Empty<CategoryRow>();

  [BindProperty]
  public List<CategorySortInput> SortInputs { get; set; } = [];

  public sealed class CategorySortInput
  {
    public Guid Id { get; set; }
    public int SortOrder { get; set; }
  }

  public async Task OnGetAsync(CancellationToken cancellationToken)
  {
    Categories = await LoadRowsAsync(cancellationToken);
    SortInputs = Categories.Select(c => new CategorySortInput { Id = c.Id, SortOrder = c.SortOrder }).ToList();
  }

  public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
  {
    if (SortInputs.Count == 0)
      return RedirectToPage();

    var ids = SortInputs.Select(s => s.Id).ToHashSet();
    var rows = await db.MenuCategories.Where(c => ids.Contains(c.Id)).ToListAsync(cancellationToken);

    foreach (var row in rows)
    {
      var input = SortInputs.FirstOrDefault(s => s.Id == row.Id);
      if (input is null)
        continue;
      row.SortOrder = Math.Clamp(input.SortOrder, 0, 9999);
    }

    await db.SaveChangesAsync(cancellationToken);
    TempData["MenuCurationToast"] = "Category order saved.";
    return RedirectToPage();
  }

  private async Task<IReadOnlyList<CategoryRow>> LoadRowsAsync(CancellationToken cancellationToken) =>
    await db.MenuCategories.AsNoTracking()
      .OrderBy(c => c.SortOrder)
      .ThenBy(c => c.Name)
      .Select(c => new CategoryRow(c.Id, c.Name, c.SortOrder))
      .ToListAsync(cancellationToken);
}
