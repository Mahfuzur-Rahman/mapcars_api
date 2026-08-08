namespace Mapcars.Domain.Enums;

/// <summary>Which surface produced an error log entry.</summary>
public enum ErrorSource
{
    /// <summary>The .NET API itself (written by ExceptionHandlingMiddleware).</summary>
    Api = 0,

    /// <summary>The Next.js web app (browser or server component).</summary>
    Web = 1,

    /// <summary>The Flutter rider app.</summary>
    CustomerApp = 2,

    /// <summary>The Flutter driver app.</summary>
    DriverApp = 3,
}
