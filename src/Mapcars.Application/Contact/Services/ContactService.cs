using System.Net;
using Mapcars.Application.Common.Interfaces;
using Mapcars.Application.Contact.Dtos;
using Mapcars.Application.Contact.Interfaces;

namespace Mapcars.Application.Contact.Services;

/// <summary>
/// Sends contact-form enquiries to <see cref="SupportAddress"/>, where they appear
/// on the admin Email page like every other send.
///
/// <para>
/// <b>No acknowledgement goes to the sender.</b> The address on the form is
/// unverified, so auto-replying would let anyone make mapcars.uk mail any address
/// they like — a spam relay that burns our sending reputation. Support replies
/// by hand to the address in the body.
/// </para>
/// </summary>
public class ContactService(IEmailService email) : IContactService
{
    public const string SupportAddress = "support@mapcars.uk";

    /// <summary>The form's fixed subject options, and how they read in the inbox.</summary>
    public static readonly IReadOnlyDictionary<string, string> Subjects = new Dictionary<string, string>
    {
        ["customer"] = "Customer enquiry",
        ["driver"] = "Driver enquiry",
        ["business"] = "Business enquiry",
        ["press"] = "Press & media",
        ["other"] = "General enquiry",
    };

    public async Task SubmitAsync(ContactRequest request, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(request.Website)) return; // honeypot tripped

        var name = $"{request.FirstName.Trim()} {request.LastName.Trim()}";
        var topic = Subjects[request.Subject];

        // Everything user-supplied is HTML-encoded: this body is rendered in an
        // admin's mail client and on the admin Email page.
        var body =
            $"<p><strong>{Enc(topic)}</strong> from the mapcars.uk contact form.</p>" +
            $"<p><strong>Name:</strong> {Enc(name)}<br>" +
            $"<strong>Email:</strong> {Enc(request.Email.Trim())}</p>" +
            $"<p style=\"white-space:pre-wrap\">{Enc(request.Message.Trim())}</p>";

        // Delivery failure surfaces as an error on purpose: unlike an OTP, the
        // person is waiting to hear the message went, and must be told to retry.
        await email.SendAsync(
            SupportAddress,
            $"[Contact] {topic} — {OneLine(name)}",
            body,
            new EmailSendOptions(Category: "Contact"),
            ct);
    }

    private static string Enc(string s) => WebUtility.HtmlEncode(s);

    /// <summary>A subject line is a header: strip line breaks so a name can't inject one.</summary>
    private static string OneLine(string s) => s.Replace("\r", " ").Replace("\n", " ");
}
