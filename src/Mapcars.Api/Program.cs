using System.Text;
using Mapcars.Api.Filters;
using Mapcars.Api.Middleware;
using Mapcars.Application;
using Mapcars.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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

// CORS — the browser web app calls the API cross-origin in dev.
const string CorsPolicy = "MapcarsCors";
builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy => policy
        .WithOrigins(builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [])
        .AllowAnyHeader()
        .AllowAnyMethod());
});

// JWT authentication
var jwtSecret = builder.Configuration["Jwt:Secret"];
if (string.IsNullOrWhiteSpace(jwtSecret))
{
    // No secret configured (e.g. a credential-free test deploy). Fall back to an
    // insecure placeholder so the app can still boot and serve the API explorer.
    // Tokens issued/validated with this key are NOT secure — set a real
    // Jwt:Secret (user-secrets locally, appsettings.Production.json on the host)
    // for anything beyond testing.
    jwtSecret = "mapcars-insecure-testing-only-secret-key-change-me";
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
    });

builder.Services.AddAuthorization();

// Layered registration — each layer owns its own DI wiring.
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// --- HTTP pipeline ----------------------------------------------------------

// Translates domain/application exceptions into proper HTTP responses.
app.UseMiddleware<ExceptionHandlingMiddleware>();

// API explorer is exposed in every environment so the deployed API can be
// browsed at /scalar/v1 (handy for testing on hosts like Somee).
app.MapOpenApi();
app.MapScalarApiReference();

// Base URL → the interactive explorer, so hitting "/" isn't a bare 404.
app.MapGet("/", () => Results.Redirect("/scalar/v1"));

// Only enforce HTTPS locally. On managed hosts (Somee, etc.) TLS is terminated
// at the proxy, so redirecting here would cause a redirect loop.
if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors(CorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
