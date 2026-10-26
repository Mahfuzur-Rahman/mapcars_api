using System.Text;
using System.Threading.RateLimiting;
using Mapcars.Api.Filters;
using Mapcars.Api.Hosting;
using Mapcars.Api.Hubs;
using Mapcars.Api.Middleware;
using Mapcars.Api.Realtime;
using Mapcars.Application;
using Mapcars.Application.Common.Interfaces;
using Mapcars.Application.Realtime.Interfaces;
using Mapcars.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// --- Services ---------------------------------------------------------------

// Global input validation: any request with a registered IValidator<T> is
// validated before the action runs (see ValidationActionFilter).
builder.Services.AddControllers(options =>
{
    options.Filters.Add<ValidationActionFilter>();

    // Admin menu permissions: an endpoint marked [RequiresAdminMenu] refuses an
    // Admin who doesn't hold one of its menus (403). The portal only hides those
    // menus; this is what actually enforces them.
    options.Filters.Add<AdminMenuAuthorizationFilter>();
});
builder.Services.AddOpenApi();

// Behind Nginx (GCE) / any reverse proxy that terminates TLS: trust its
// X-Forwarded-For / -Proto so the app sees the real client and knows the request
// was HTTPS. Trusted ONLY from private and loopback hops — Nginx on the host
// reaches the container via the Docker gateway, and the web BFF calls it over the
// Docker network — and only ONE hop is unwound, i.e. the right-most entry, the one
// our own proxy appended. Nginx uses $proxy_add_x_forwarded_for, which APPENDS, so
// the left-most entry is whatever the client typed: until 2026-09 the API keyed its
// rate limits on that, and any caller could dodge every limit by sending a fresh
// fake IP per request. After this, Connection.RemoteIpAddress IS the client IP.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
    foreach (var cidr in new[]
             {
                 "127.0.0.0/8", "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16",
                 // The same ranges as IPv4-mapped IPv6, which is how a dual-stack
                 // socket can report them.
                 "::ffff:127.0.0.0/104", "::ffff:10.0.0.0/104", "::ffff:172.16.0.0/108", "::ffff:192.168.0.0/112",
                 "::1/128",
             })
    {
        var parts = cidr.Split('/');
        options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(
            System.Net.IPAddress.Parse(parts[0]), int.Parse(parts[1])));
    }
});

// CORS — the browser web app calls the API cross-origin in dev.
const string CorsPolicy = "MapcarsCors";
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy => policy
        .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()); // required for a browser SignalR (WebSocket) client
});

// JWT authentication.
// The signing key must be real and strong OUTSIDE Development — a weak or missing
// key lets anyone forge tokens (incl. SuperAdmin). We therefore fail fast at
// startup rather than silently booting with an insecure default. The insecure
// placeholder is permitted ONLY in Development so the app boots without config.
var jwtSecret = builder.Configuration["Jwt:Secret"];
if (string.IsNullOrWhiteSpace(jwtSecret))
{
    if (!builder.Environment.IsDevelopment())
        throw new InvalidOperationException(
            "Jwt:Secret is not configured. Set the Jwt__Secret environment variable (Production) " +
            "or user-secrets (local). The API will not start without a real signing key outside Development.");

    jwtSecret = "mapcars-insecure-testing-only-secret-key-change-me";
}
else if (!builder.Environment.IsDevelopment() && Encoding.UTF8.GetByteCount(jwtSecret) < 32)
{
    // HS256 needs a key of at least 256 bits (32 bytes); a shorter one is trivially brute-forced.
    throw new InvalidOperationException(
        "Jwt:Secret is too short for HS256 — use at least 32 bytes (256 bits) of high-entropy secret.");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        };

        // WebSocket clients can't set an Authorization header on the handshake —
        // SignalR sends the JWT as the `access_token` query param instead. Accept
        // it for /hubs paths so hub connections authenticate.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) &&
                    context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            },
        };
    });

builder.Services.AddAuthorization();

// Rate limiting — brute-force / abuse protection on the auth surface. Partitioned
// by the real client IP (resolved from trusted proxy hops only — see ClientKey). Two
// named policies applied per-endpoint (see the *AuthController classes):
//   • "otp"  — endpoints that send a billable SMS/email (send-otp, signup, resend)
//   • "auth" — credential/code submission (verify-*, login, google, setup)
// Authenticated, high-frequency endpoints (profile, driver location, polling) are
// deliberately NOT limited here.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Say how long to wait, so the apps' resend countdown is right even when it
    // was the per-IP limiter (not the per-number guard) that said no.
    options.OnRejected = (ctx, _) =>
    {
        if (ctx.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            ctx.HttpContext.Response.Headers.RetryAfter =
                ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture);
        return ValueTask.CompletedTask;
    };

    options.AddPolicy("otp", context =>
        RateLimitPartition.GetFixedWindowLimiter(ClientKey(context), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(15),
            QueueLimit = 0,
        }));

    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(ClientKey(context), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromMinutes(5),
            QueueLimit = 0,
        }));

    // Card-add is the endpoint a card tester wants: it asks the issuer "is this
    // card live?" and answers instantly, for free. Left unlimited it is a free
    // validation oracle for a stolen-card list — and providers monitor for
    // exactly that, so the damage lands on OUR account even when every card
    // tried belonged to someone else.
    //
    // Keyed on IP, so it stops the casual and accidental case. It does NOT stop
    // someone rotating IPs — the per-ACCOUNT limits are what cover that, and
    // they live in the payments service because they have to follow the person
    // rather than the connection.
    options.AddPolicy("payments", context =>
        RateLimitPartition.GetFixedWindowLimiter(ClientKey(context), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(15),
            QueueLimit = 0,
        }));

    // Client crash reporting (POST /api/v1/error-logs) is anonymous, so it needs
    // a ceiling — generous enough that a genuinely broken app can report a burst
    // of failures, tight enough that it can't be used to fill the table.
    options.AddPolicy("errors", context =>
        RateLimitPartition.GetFixedWindowLimiter(ClientKey(context), _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 60,
            Window = TimeSpan.FromMinutes(5),
            QueueLimit = 0,
        }));
});

// Realtime — SignalR with a Redis backplane (reuses the Valkey connection) so
// pushes fan out across API instances; falls back to in-memory when Redis isn't
// configured. The SignalR notifier overrides the Application's no-op default.
var signalR = builder.Services.AddSignalR();
var signalRRedis = builder.Configuration["Redis:Configuration"];
if (!string.IsNullOrWhiteSpace(signalRRedis))
{
    signalR.AddStackExchangeRedis(signalRRedis);
}
builder.Services.AddSingleton<ITripNotifier, SignalRTripNotifier>();

// Host-environment signal for the Application layer (e.g. dev-only OTP reveal).
builder.Services.AddSingleton<IAppEnvironment>(new HostAppEnvironment(builder.Environment));

// Layered registration — each layer owns its own DI wiring.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// --- HTTP pipeline ----------------------------------------------------------

// Must run before anything that reads Request.Scheme (OpenAPI doc generation,
// HTTPS redirection, etc.) so a proxied HTTPS request isn't seen as HTTP.
app.UseForwardedHeaders();

// Translates domain/application exceptions into proper HTTP responses.
app.UseMiddleware<ExceptionHandlingMiddleware>();

// API explorer is exposed in every environment so the deployed API can be
// browsed at /scalar/v1 (handy for testing on hosts like Somee).
app.MapOpenApi();
app.MapScalarApiReference();

// Base URL → the interactive explorer, so hitting "/" isn't a bare 404.
app.MapGet("/", () => Results.Redirect("/scalar/v1"));

// No HTTPS redirect. In deployment TLS is terminated at the proxy (Nginx on GCE),
// so redirecting here would cause a redirect loop. Locally it broke every plain-HTTP
// client: http://localhost:5200 answered 307 → https://localhost:7200, and the
// Kestrel dev certificate is self-signed, so the web app's BFF (Node `fetch`) failed
// the hop with DEPTH_ZERO_SELF_SIGNED_CERT and every login surfaced as a 502
// "Unable to reach the server". Loopback HTTP needs no TLS. https://localhost:7200
// still works for anything that wants it — it just isn't forced.

app.UseCors(CorsPolicy);

// After UseForwardedHeaders so the limiter sees the real client IP, before the
// endpoints so throttled requests are rejected early.
app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<TripHub>("/hubs/trip");

app.Run();

// Partition key for rate limiting: the originating client IP, as resolved by
// UseForwardedHeaders from trusted hops only. Never read X-Forwarded-For here
// directly — its left-most entry is client-controlled (see the options above).
static string ClientKey(HttpContext context)
    => context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
