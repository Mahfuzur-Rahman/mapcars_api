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
});
builder.Services.AddOpenApi();

// Behind Nginx (GCE) / any reverse proxy that terminates TLS: trust its
// X-Forwarded-Proto so the app knows the original request was HTTPS, not the
// plain-HTTP hop from the proxy to Kestrel. Known networks/proxies are cleared
// because the proxy sits on the same host/container network, not a fixed IP.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
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
// by the real client IP (X-Forwarded-For first, since we sit behind Nginx). Two
// named policies applied per-endpoint (see the *AuthController classes):
//   • "otp"  — endpoints that send a billable SMS/email (send-otp, signup, resend)
//   • "auth" — credential/code submission (verify-*, login, google, setup)
// Authenticated, high-frequency endpoints (profile, driver location, polling) are
// deliberately NOT limited here.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

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

// Partition key for rate limiting: the originating client IP. Behind Nginx the
// connection IP is the proxy, so prefer the left-most X-Forwarded-For entry.
static string ClientKey(HttpContext context)
{
    var forwarded = context.Request.Headers["X-Forwarded-For"].ToString();
    if (!string.IsNullOrWhiteSpace(forwarded))
        return forwarded.Split(',')[0].Trim();
    return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
