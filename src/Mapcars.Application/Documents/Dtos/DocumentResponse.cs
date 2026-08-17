namespace Mapcars.Application.Documents.Dtos;

/// <summary>Outbound document representation. Never expose entities directly.</summary>
public record DocumentResponse(
    Guid Id,
    string Type,
    string OriginalFileName,
    string ReviewStatus,
    DateTime CreatedAtUtc,
    DateTime? ReviewedAtUtc,
    DateOnly? ExpiresOn,
    bool IsDeletionRequested = false,
    string? DeletionReason = null,
    DateTime? DeletionRequestedAtUtc = null);

/// <summary>Driver requests an uploaded document to be deleted by admin.</summary>
public record RequestDocumentDeletionRequest(string? Reason = null);

/// <summary>Admin decision on a document deletion request. Status: "Approved" | "Rejected".</summary>
public record ReviewDocumentDeletionRequest(string Status, string? AdminNotes = null);
