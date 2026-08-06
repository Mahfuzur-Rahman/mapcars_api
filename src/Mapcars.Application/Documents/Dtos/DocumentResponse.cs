namespace Mapcars.Application.Documents.Dtos;

/// <summary>Outbound document representation. Never expose entities directly.</summary>
public record DocumentResponse(
    Guid Id,
    string Type,
    string OriginalFileName,
    string ReviewStatus,
    DateTime CreatedAtUtc,
    DateTime? ReviewedAtUtc,
    DateOnly? ExpiresOn);
