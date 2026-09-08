namespace Annap.CoffeeQrOrdering.Web.Extensions;

public static class MiddlewareExtensions
{
    public static WebApplication UseAnnapMiddleware(this WebApplication app)
    {
        if (!app.Environment.IsDevelopment())
            app.UseForwardedHeaders();

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            app.UseStatusCodePagesWithReExecute("/Error", "?statusCode={0}");
            app.UseHsts();
        }

        // Development is often HTTP-only (LAN demo on :8080). HTTPS redirection breaks phones hitting http://<LAN>:8080.
        if (!app.Environment.IsDevelopment())
            app.UseHttpsRedirection();
        app.UseResponseCompression();
        app.UseRequestLocalization();
        if (!app.Environment.IsDevelopment())
        {
            app.UseStaticFiles(new StaticFileOptions
            {
                OnPrepareResponse = ctx =>
                {
                    if (ctx.Context.Request.Path.StartsWithSegments("/media/menu-items"))
                    {
                        ctx.Context.Response.Headers.CacheControl = "public,max-age=0,no-cache";
                        return;
                    }

                    var path = ctx.File.Name;
                    if (path.EndsWith(".woff2", StringComparison.OrdinalIgnoreCase) ||
                        path.EndsWith(".css", StringComparison.OrdinalIgnoreCase) ||
                        path.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
                        ctx.Context.Response.Headers.CacheControl = "public,max-age=86400,immutable";
                }
            });
        }
        else
        {
            app.UseStaticFiles();
        }

        /* Static files must run before routing so /css, /js, /lib are served without hitting endpoint middleware first. */
        app.Use(async (ctx, next) =>
        {
            var path = ctx.Request.Path;
            if (path.StartsWithSegments("/table", out var remainder) && remainder.HasValue)
            {
                var seg = remainder.Value.TrimStart('/').Trim();
                if (seg.Length is > 0 and < 48)
                    ctx.Items["QrTableDisplayCode"] = Uri.UnescapeDataString(seg);
                ctx.Request.Path = "/";
            }
            else if (path.StartsWithSegments("/t", out var remainder2) && remainder2.HasValue)
            {
                var slug = remainder2.Value.TrimStart('/').Trim().ToLowerInvariant();
                if (slug.Length is > 0 and < 96)
                    ctx.Items["QrPublicSlug"] = slug;
                ctx.Request.Path = "/";
            }

            await next();
        });

        /* Rewrite legacy staff login URL to canonical /staff/login route. */
        app.Use(async (ctx, next) =>
        {
            if (ctx.Request.Path.StartsWithSegments("/Staff/Login", StringComparison.OrdinalIgnoreCase, out var remainder))
                ctx.Request.Path = new PathString("/staff/login").Add(remainder);
            await next();
        });

        app.UseRouting();
        app.UseRateLimiter();
        if (app.Environment.IsDevelopment())
        {
            app.UseCors("DevelopmentLan");
            app.UseMiddleware<DevRequestLoggingMiddleware>();
        }
        app.UseAuthentication();
        app.UseAuthorization();

        return app;
    }
}
