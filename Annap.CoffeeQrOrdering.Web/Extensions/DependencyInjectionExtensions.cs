using Annap.CoffeeQrOrdering.Application;
using Microsoft.Extensions.Options;
using Annap.CoffeeQrOrdering.Infrastructure;
using Annap.CoffeeQrOrdering.Infrastructure.Persistence;
using Annap.CoffeeQrOrdering.Web.Internal;
using Annap.CoffeeQrOrdering.Web.Security;
using Annap.CoffeeQrOrdering.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.RateLimiting;
using System.Globalization;
using System.Threading.RateLimiting;

namespace Annap.CoffeeQrOrdering.Web.Extensions;

public static class DependencyInjectionExtensions
{
    public static WebApplicationBuilder AddAnnapWebServices(this WebApplicationBuilder builder, int devPort)
    {
        builder.Host.ConfigureHostOptions(options =>
        {
            options.ShutdownTimeout = TimeSpan.FromSeconds(15);
        });

        if (builder.Environment.IsDevelopment())
        {
            builder.WebHost.UseShutdownTimeout(TimeSpan.FromSeconds(10));
        }

        builder.Services.AddHttpContextAccessor();
        builder.Services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
        });

        ConfigureDataProtection(builder);
        builder.Services.Configure<LanDemoOptions>(builder.Configuration.GetSection(LanDemoOptions.SectionName));
        builder.Services.Configure<AppUrlOptions>(builder.Configuration.GetSection(AppUrlOptions.SectionName));
        builder.Services.Configure<CloudinaryOptions>(builder.Configuration.GetSection(CloudinaryOptions.SectionName));
        builder.Services.Configure<DataProtectionStorageOptions>(
            builder.Configuration.GetSection(DataProtectionStorageOptions.SectionName));
        builder.Services.AddSingleton<IMenuImageStorage, MenuImageStorage>();
        builder.Services.AddSingleton<ILanIpDetector, LanIpDetector>();
        builder.Services.AddSingleton<IPostConfigureOptions<AppUrlOptions>, AppUrlDevelopmentPostConfigure>();
        builder.Services.AddScoped<IAppUrlService, AppUrlService>();
        builder.Services.AddScoped<InfrastructureDiagnosticsService>();
        builder.Services.AddScoped<ProductionDataAuditService>();

        builder.Services.Configure<StaffAuthOptions>(builder.Configuration.GetSection(StaffAuthOptions.SectionName));
        builder.Services.Configure<BankTransferOptions>(builder.Configuration.GetSection(BankTransferOptions.SectionName));
        builder.Services.AddSingleton<BankTransferQrBuilder>();
        builder.Services.AddScoped<IOrderPaymentWorkflowService, OrderPaymentWorkflowService>();
        builder.Services.AddScoped<IBankTransferConfirmationService, BankTransferConfirmationService>();
        builder.Services.AddScoped<IStaffAccountService, StaffAccountService>();
        builder.Services.AddSingleton<DevBankTransferWebhookParser>();
        builder.Services.Configure<DiagnosticsOptions>(
            builder.Configuration.GetSection(DiagnosticsOptions.SectionName));
        builder.Services.Configure<GuestOperationalOptions>(
            builder.Configuration.GetSection(GuestOperationalOptions.SectionName));
        builder.Services.Configure<ForwardedHeadersOptions>(TrustedProxyNetworks.Apply);

        builder.Services.Configure<RequestLocalizationOptions>(options =>
        {
            var supported = new[] { new CultureInfo("vi-VN"), new CultureInfo("en-US") };
            options.DefaultRequestCulture = new RequestCulture("vi-VN");
            options.SupportedCultures = supported;
            options.SupportedUICultures = supported;
            options.RequestCultureProviders =
            [
                new CookieRequestCultureProvider(),
                new QueryStringRequestCultureProvider(),
                new AcceptLanguageHeaderRequestCultureProvider()
            ];
        });

        builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
        builder.Services.AddSingleton<I18nBundleService>();

        builder.Services.AddSingleton<HubConnectionRegistry>();
        builder.Services.AddSingleton<IOrderStatusNotifier, OrderStatusNotifier>();
        builder.Services.AddSignalR(o =>
        {
            o.EnableDetailedErrors = false;
            o.KeepAliveInterval = TimeSpan.FromSeconds(12);
            o.ClientTimeoutInterval = TimeSpan.FromSeconds(45);
            o.MaximumReceiveMessageSize = 48 * 1024;
        });
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(o =>
            {
                o.LoginPath = "/staff/login";
                o.AccessDeniedPath = "/staff/login";
                o.SlidingExpiration = true;
                o.ExpireTimeSpan = TimeSpan.FromHours(12);
                o.Cookie.Name = "annap_staff";
                o.Cookie.HttpOnly = true;
                o.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
                    ? CookieSecurePolicy.SameAsRequest
                    : CookieSecurePolicy.Always;
                o.Cookie.SameSite = SameSiteMode.Lax;
                o.Events.OnRedirectToLogin = StaffCookieAuthenticationEvents.OnRedirectToLogin;
                o.Events.OnRedirectToAccessDenied = StaffCookieAuthenticationEvents.OnRedirectToAccessDenied;
            });
        builder.Services.AddAuthorization(o =>
        {
            o.AddPolicy("Staff", p => p.RequireRole(StaffRoleNames.Admin));
            o.AddPolicy("StaffAdmin", p => p.RequireRole(StaffRoleNames.Admin));
            o.AddPolicy("StaffCheckout", p => p.RequireRole(StaffRoleNames.Admin, StaffRoleNames.Checkout));
            o.AddPolicy("StaffBarista", p => p.RequireRole(StaffRoleNames.Admin, StaffRoleNames.Barista));
            o.AddPolicy("StaffBoardAccess", p => p.RequireRole(
                StaffRoleNames.Admin,
                StaffRoleNames.Checkout,
                StaffRoleNames.Barista));
            o.AddPolicy("StaffFloor", p => p.RequireRole(
                StaffRoleNames.Admin,
                StaffRoleNames.Checkout,
                StaffRoleNames.Barista));
            o.AddPolicy("BillManage", p => p.RequireRole(StaffRoleNames.Admin));
            o.AddPolicy("BillView", p => p.RequireRole(
                StaffRoleNames.Admin,
                StaffRoleNames.Checkout,
                StaffRoleNames.Barista));
            // Kết ca — admin and checkout (shared + employee), not barista-only.
            o.AddPolicy("StaffShiftClose", p => p.RequireRole(
                StaffRoleNames.Admin,
                StaffRoleNames.Checkout));
        });

        builder.Services.AddScoped<IShiftCloseService, ShiftCloseService>();
        builder.Services.AddSingleton<IStaffCredentialFlashStore, StaffCredentialFlashStore>();

        builder.Services.AddRazorPages(o =>
        {
            o.Conventions.AuthorizeFolder("/Staff", "StaffFloor");
            o.Conventions.AuthorizePage("/Staff/ShiftClose/Index", "StaffShiftClose");
            o.Conventions.AuthorizeFolder("/Admin", "StaffAdmin");
            o.Conventions.AllowAnonymousToPage("/Diag/Mobile");
            o.Conventions.AllowAnonymousToPage("/Diag/Assets");
            o.Conventions.AllowAnonymousToPage("/Diag/Minimal");
            o.Conventions.AllowAnonymousToPage("/Staff/Login");
            o.Conventions.AllowAnonymousToPage("/Staff/Logout");
        });
        builder.Services.AddHealthChecks()
            .AddDbContextCheck<AppDbContext>("database")
            .AddCheck<PaymentWorkflowSchemaHealthCheck>("payment_workflow_schema")
            .AddCheck<ProductionStorageHealthCheck>("production_storage");
        builder.Services.AddHttpClient();
        builder.Services.AddScoped<GoLiveVerificationService>();

        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (ctx, _) =>
            {
                var http = ctx.HttpContext;
                if (http.Request.Path.StartsWithSegments("/staff/login", StringComparison.OrdinalIgnoreCase)
                    && HttpMethods.IsPost(http.Request.Method))
                {
                    http.Response.Redirect("/staff/login?rateLimited=1");
                    return;
                }

                http.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                http.Response.ContentType = "application/json";
                await http.Response.WriteAsJsonAsync(
                    new { error = "Too many requests. Please wait a moment and try again." },
                    cancellationToken: http.RequestAborted);
            };

            static string Partition(HttpContext http) => RateLimitClientKey.Resolve(http);

            options.AddPolicy("anon-order-post", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    Partition(httpContext),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 45,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 4,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        AutoReplenishment = true
                    }));

            options.AddPolicy("anon-ai-post", httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    Partition(httpContext),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 22,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));

            options.AddPolicy("staff-login", httpContext =>
            {
                if (!HttpMethods.IsPost(httpContext.Request.Method))
                    return RateLimitPartition.GetNoLimiter("staff-login-get");

                return RateLimitPartition.GetFixedWindowLimiter(
                    Partition(httpContext),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 8,
                        Window = TimeSpan.FromMinutes(5),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            });
        });

        builder.Services
            .AddApplication()
            .AddInfrastructure(builder.Configuration);

        builder.Services.AddScoped<MenuMediaMaintenanceService>();

        return builder;
    }

    private static void ConfigureDataProtection(WebApplicationBuilder builder)
    {
        var section = builder.Configuration.GetSection(DataProtectionStorageOptions.SectionName);
        var keysPath = section[nameof(DataProtectionStorageOptions.KeysPath)]?.Trim();
        var applicationName = section[nameof(DataProtectionStorageOptions.ApplicationName)]?.Trim();
        if (string.IsNullOrWhiteSpace(applicationName))
            applicationName = "Annap.CoffeeQrOrdering";

        var dataProtection = builder.Services
            .AddDataProtection()
            .SetApplicationName(applicationName);

        if (string.IsNullOrWhiteSpace(keysPath))
            return;

        var directory = new DirectoryInfo(Path.GetFullPath(keysPath));
        Directory.CreateDirectory(directory.FullName);
        dataProtection.PersistKeysToFileSystem(directory);
    }
}
