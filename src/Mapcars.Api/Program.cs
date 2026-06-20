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
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Jwt:Secret not configured. Run: dotnet user-secrets set \"Jwt:Secret\" \"<your-32-char-key>\"");

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

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    // Interactive API explorer at /scalar/v1 — reads the OpenAPI doc above.
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseCors(CorsPolicy);

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
