using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;

namespace FieldOps.Api.Common;

/// <summary>Limits sign-in attempts per client address, to slow down password guessing.</summary>
public sealed class AuthRateLimitOptions
{
    public const string Section = "RateLimiting:Auth";
    public const string Policy = "auth";

    public int PermitLimit { get; set; } = 10;
    public int WindowSeconds { get; set; } = 60;
}

public static class Security
{
    // The Angular app needs inline styles (Angular Material), Google Fonts, OpenStreetMap tiles, and blob/data images
    // (photo previews, signatures); the API and SignalR hub are same-origin.
    private const string ContentSecurityPolicy =
        "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
        "font-src 'self' https://fonts.gstatic.com; img-src 'self' data: blob: https://tile.openstreetmap.org; " +
        "connect-src 'self'; manifest-src 'self'; worker-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'; " +
        "object-src 'none'";

    public static IServiceCollection AddFieldOpsSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        var limits = configuration.GetSection(AuthRateLimitOptions.Section).Get<AuthRateLimitOptions>() ?? new AuthRateLimitOptions();
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.AddPolicy(AuthRateLimitOptions.Policy, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = limits.PermitLimit,
                    Window = TimeSpan.FromSeconds(limits.WindowSeconds),
                    QueueLimit = 0,
                }));
            o.OnRejected = async (context, ct) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                await Results.Problem(
                        title: "Too many attempts. Wait a minute and try again.",
                        statusCode: StatusCodes.Status429TooManyRequests,
                        type: "https://fieldops/errors/rate-limited")
                    .ExecuteAsync(context.HttpContext);
            };
        });

        // Behind a reverse proxy (docs/02-architecture.md), trust X-Forwarded-For/Proto only from the proxies listed in
        // configuration ("ForwardedHeaders:KnownProxies"); the default trusts loopback only.
        services.Configure<ForwardedHeadersOptions>(o =>
        {
            o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            foreach (var proxy in configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [])
                o.KnownProxies.Add(System.Net.IPAddress.Parse(proxy));
        });
        services.AddHsts(o => o.MaxAge = TimeSpan.FromDays(365));
        return services;
    }

    /// <summary>Standard hardening headers on every response. Swagger (Development only) keeps its own inline scripts.</summary>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        var headers = context.Response.Headers;
        headers.XContentTypeOptions = "nosniff";
        headers.XFrameOptions = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Permissions-Policy"] = "geolocation=(self), camera=(self), microphone=(), payment=()";
        headers["Cross-Origin-Opener-Policy"] = "same-origin";
        if (!context.Request.Path.StartsWithSegments("/swagger")) headers.ContentSecurityPolicy = ContentSecurityPolicy;
        await next();
    });
}
