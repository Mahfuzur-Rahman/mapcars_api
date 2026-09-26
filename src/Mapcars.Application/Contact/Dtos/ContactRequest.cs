namespace Mapcars.Application.Contact.Dtos;

/// <summary>
/// The marketing site's contact form. <c>Subject</c> is one of the form's fixed
/// options (customer, driver, business, press, other).
/// </summary>
/// <param name="Website">
/// Honeypot: a field hidden from people, which naive bots fill in. A submission
/// with it set is accepted and silently dropped, so the bot learns nothing.
/// </param>
public record ContactRequest(
    string FirstName,
    string LastName,
    string Email,
    string Subject,
    string Message,
    string? Website = null);
