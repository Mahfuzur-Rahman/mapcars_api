namespace Mapcars.Application.Messages;

/// <summary>
/// Chat settings, bound from <c>Chat:SendWindowHours</c> (env: <c>Chat__SendWindowHours</c>)
/// in Infrastructure's DI. A plain singleton — Application has no Options dependency.
/// </summary>
public sealed class ChatOptions
{
    /// <summary>How long after completion either party may still write (lost items).</summary>
    public int SendWindowHours { get; init; } = 24;

    public TimeSpan SendWindow => TimeSpan.FromHours(SendWindowHours);
}
