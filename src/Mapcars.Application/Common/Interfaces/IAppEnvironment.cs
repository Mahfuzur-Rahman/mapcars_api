namespace Mapcars.Application.Common.Interfaces;

/// <summary>
/// Minimal host-environment signal for the Application layer, so business code
/// can make dev-only vs. production decisions without taking a dependency on
/// ASP.NET hosting. Implemented in the API layer over <c>IHostEnvironment</c>.
/// </summary>
public interface IAppEnvironment
{
    /// <summary>True only when running under the Development environment.</summary>
    bool IsDevelopment { get; }
}
