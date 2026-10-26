namespace Mapcars.Application.Common.Exceptions;

/// <summary>
/// "Not now; try again in <see cref="RetryAfterSeconds"/>." Mapped to HTTP 429
/// with a Retry-After header and <c>retryAfterSeconds</c> in the problem body,
/// which the clients read to run their resend countdown.
/// </summary>
public class TooManyRequestsException(int retryAfterSeconds, string message) : Exception(message)
{
    public int RetryAfterSeconds { get; } = retryAfterSeconds;
}
