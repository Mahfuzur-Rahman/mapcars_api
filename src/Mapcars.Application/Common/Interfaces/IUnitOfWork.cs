namespace Mapcars.Application.Common.Interfaces;

/// <summary>
/// Commits a set of changes as one transaction. Implemented by the DbContext.
/// Services call this once per use-case after mutating repositories.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
