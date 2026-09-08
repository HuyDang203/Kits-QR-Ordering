using Annap.CoffeeQrOrdering.Domain.Entities;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Annap.CoffeeQrOrdering.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Annap.CoffeeQrOrdering.Web.Pages.Admin;

/// <summary>Admin home metrics — EF queries run sequentially on the scoped DbContext (never parallel on one context).</summary>
[Authorize(Policy = "StaffAdmin")]
public sealed class IndexModel(
    AppDbContext db,
    IWebHostEnvironment env,
    IConfiguration configuration,
    IAppUrlService appUrlService,
    IHttpContextAccessor httpContextAccessor) : PageModel
{
    [BindProperty]
    public HomepageExperienceSettingsAdmin.FormModel HomepageForm { get; set; } = new();

    public string? HomepageCompositionStatus { get; set; }

    public bool HomepageCompositionShowDedicatedLink => true;

    public string HomepageCompositionFormId => "exp-homepage-form-admin";

    public string HomepageCompositionPostHandler => "SaveHomepageComposition";

    public int HomepageModesVisible => HomepageExperienceSettingsAdmin.VisibleCount(HomepageForm);
    private static readonly OrderStatus[] OpenTicketStatuses =
    [
        OrderStatus.Submitted,
        OrderStatus.InProgress,
        OrderStatus.Ready,
        OrderStatus.FinishingTouches
    ];

    /// <summary>Local time string for hero (server clock).</summary>
    public string ServerLocalTime { get; private set; } = "";

    public string EnvironmentDisplayName { get; private set; } = "";

    public bool DatabaseConnected { get; private set; }

    public bool ApplyMigrationsOnStartup { get; private set; }

    /// <summary>Configured LAN / QR base URL (Development display).</summary>
    public string? LanPublicBaseUrl { get; private set; }

    public int ActiveVenueTables { get; private set; }

    public int OpenOrdersCount { get; private set; }

    public int CupsInFlight { get; private set; }

    public decimal OpenTicketsValue { get; private set; }

    public int SignatureSlotsFilled { get; private set; }

    public int SignatureMenuItems { get; private set; }

    public int SeasonalHighlightDrinks { get; private set; }

    public int ActiveMenuCategories { get; private set; }

    public int AvailableMenuDrinks { get; private set; }

    public int GuidedQuestionsEnabled { get; private set; }

    public int GuidedQuestionsTotal { get; private set; }

    public bool GroupExperienceAvailable { get; private set; }

    public bool GuidedSommelierCmsLive { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        ServerLocalTime = DateTime.Now.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture);
        EnvironmentDisplayName = env.EnvironmentName;
        ApplyMigrationsOnStartup = configuration.GetValue("Database:ApplyMigrationsOnStartup", false);
        LanPublicBaseUrl = appUrlService.GetBaseUrl(httpContextAccessor.HttpContext);
        if (string.IsNullOrWhiteSpace(LanPublicBaseUrl))
            LanPublicBaseUrl = null;
        else
            LanPublicBaseUrl = LanPublicBaseUrl.Trim().TrimEnd('/');

        DatabaseConnected = false;
        try
        {
            DatabaseConnected = await db.Database.CanConnectAsync(cancellationToken);
        }
        catch
        {
            DatabaseConnected = false;
        }

        if (!DatabaseConnected)
            return;

        ActiveVenueTables = await db.VenueTables.AsNoTracking().CountAsync(v => v.IsActive, cancellationToken);
        OpenOrdersCount = await db.Orders.AsNoTracking()
            .CountAsync(o => OpenTicketStatuses.Contains(o.Status), cancellationToken);
        CupsInFlight = await (
            from oi in db.OrderItems.AsNoTracking()
            join o in db.Orders.AsNoTracking() on oi.OrderId equals o.Id
            where OpenTicketStatuses.Contains(o.Status)
            select oi.Quantity).SumAsync(cancellationToken);
        OpenTicketsValue = await db.Orders.AsNoTracking()
            .Where(o => OpenTicketStatuses.Contains(o.Status))
            .SumAsync(o => o.TotalAmount, cancellationToken);
        ActiveMenuCategories = await db.MenuCategories.AsNoTracking().CountAsync(cancellationToken);
        AvailableMenuDrinks = await db.MenuItems.AsNoTracking()
            .CountAsync(m => m.IsAvailable && !m.IsArchived, cancellationToken);
        SignatureMenuItems = await db.MenuItems.AsNoTracking()
            .CountAsync(m => m.IsAvailable && !m.IsArchived && m.IsSignature, cancellationToken);
        SeasonalHighlightDrinks = await db.MenuItems.AsNoTracking()
            .CountAsync(m => m.IsAvailable && !m.IsArchived && m.IsSignature, cancellationToken);

        try
        {
            SignatureSlotsFilled = await db.ExperienceSignatureSlots.AsNoTracking().CountAsync(cancellationToken);
            GuidedQuestionsEnabled = await db.ExperienceGuidedQuestions.AsNoTracking()
                .CountAsync(q => q.IsEnabled, cancellationToken);
            GuidedQuestionsTotal = await db.ExperienceGuidedQuestions.AsNoTracking()
                .CountAsync(cancellationToken);
        }
        catch
        {
            SignatureSlotsFilled = 0;
            GuidedQuestionsEnabled = 0;
            GuidedQuestionsTotal = 0;
        }

        GroupExperienceAvailable = SignatureSlotsFilled > 0 || SignatureMenuItems > 0;
        GuidedSommelierCmsLive = GuidedQuestionsEnabled > 0;

        try
        {
            HomepageForm = await HomepageExperienceSettingsAdmin.LoadAsync(db, cancellationToken);
        }
        catch
        {
            HomepageForm = new HomepageExperienceSettingsAdmin.FormModel();
        }
    }

    public async Task<IActionResult> OnPostSaveHomepageCompositionAsync(CancellationToken cancellationToken)
    {
        try
        {
            await HomepageExperienceSettingsAdmin.SaveAsync(db, HomepageForm, cancellationToken);
            HomepageCompositionStatus = "Lobby composition saved — guest phones will reflect this on refresh.";
        }
        catch (Exception ex)
        {
            HomepageCompositionStatus = null;
            ModelState.AddModelError(string.Empty, ex.Message);
        }

        await OnGetAsync(cancellationToken);
        return Page();
    }
}
