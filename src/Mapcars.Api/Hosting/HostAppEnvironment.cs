using Mapcars.Application.Common.Interfaces;

namespace Mapcars.Api.Hosting;

/// <summary>
/// Bridges the Application layer's <see cref="IAppEnvironment"/> onto ASP.NET's
/// <see cref="IHostEnvironment"/>. Registered in Program.cs with the app's real
/// environment, so business code never has to reference hosting directly.
/// </summary>
public sealed class HostAppEnvironment(IHostEnvironment env) : IAppEnvironment
{
    public bool IsDevelopment => env.IsDevelopment();
}
