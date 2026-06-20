namespace Mapcars.Domain.Exceptions;

/// <summary>
/// Thrown when a domain invariant/business rule is violated
/// (e.g. an illegal trip status transition). Mapped to HTTP 400.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
}
