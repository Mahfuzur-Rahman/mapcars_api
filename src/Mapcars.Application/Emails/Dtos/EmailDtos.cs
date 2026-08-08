namespace Mapcars.Application.Emails.Dtos;

/// <summary>What the admin portal posts from the Compose form.</summary>
public record ComposeEmailRequest(
    string To,
    string Subject,
    string BodyHtml,
    string FromAddress,
    string? FromName = null);

/// <summary>A row in the admin email list.</summary>
public record EmailLogListItem(
    Guid Id,
    string ToEmail,
    string FromAddress,
    string Subject,
    string Provider,
    string Category,
    string Status,
    DateTime CreatedAtUtc);

/// <summary>The full entry, including the body.</summary>
public record EmailLogDetail(
    Guid Id,
    string ToEmail,
    string FromAddress,
    string? FromName,
    string Subject,
    string BodyHtml,
    string Provider,
    string Category,
    string Status,
    string? ErrorMessage,
    Guid? SentByAdminId,
    DateTime CreatedAtUtc);

/// <summary>One page of the log plus the total, so the UI can page.</summary>
public record EmailLogPage(IReadOnlyList<EmailLogListItem> Items, int Total, int Page, int PageSize);
