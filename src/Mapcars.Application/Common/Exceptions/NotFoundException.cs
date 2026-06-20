namespace Mapcars.Application.Common.Exceptions;

/// <summary>Requested resource does not exist. Mapped to HTTP 404.</summary>
public class NotFoundException : Exception
{
    public NotFoundException(string name, object key)
        : base($"\"{name}\" ({key}) was not found.") { }
}
