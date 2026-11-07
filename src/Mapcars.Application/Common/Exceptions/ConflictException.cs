namespace Mapcars.Application.Common.Exceptions;

/// <summary>The request is valid but the resource's state forbids it (HTTP 409).</summary>
public class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}
