using Mapcars.Application.Common.Interfaces;
using Mapcars.Application.Contact.Dtos;
using Mapcars.Application.Contact.Services;

namespace Mapcars.Application.Tests;

/// <summary>
/// The contact form is anonymous input rendered in an admin's mail client, so
/// the failure modes are injection and becoming a relay, not crashes.
/// </summary>
public class ContactServiceTests
{
    private sealed class CaptureEmail : IEmailService
    {
        public string ProviderName => "test";
        public string DefaultFromAddress => "no-reply@mapcars.uk";
        public string DefaultFromName => "test";
        public List<(string To, string Subject, string Body, EmailSendOptions? Options)> Sent { get; } = [];

        public Task SendAsync(string toEmail, string subject, string body, EmailSendOptions? options = null, CancellationToken ct = default)
        {
            Sent.Add((toEmail, subject, body, options));
            return Task.CompletedTask;
        }
    }

    private static ContactRequest Req(string first = "Ada", string message = "Hello", string? website = null)
        => new(first, "Lovelace", "ada@example.com", "business", message, website);

    [Fact]
    public async Task Goes_only_to_the_support_inbox()
    {
        var mail = new CaptureEmail();
        await new ContactService(mail).SubmitAsync(Req());

        var sent = Assert.Single(mail.Sent);
        Assert.Equal(ContactService.SupportAddress, sent.To);
        Assert.Equal("Contact", sent.Options?.Category);
        Assert.Contains("ada@example.com", sent.Body);
        Assert.Contains("Business enquiry", sent.Subject);
    }

    [Fact]
    public async Task The_honeypot_drops_the_submission_silently()
    {
        var mail = new CaptureEmail();
        await new ContactService(mail).SubmitAsync(Req(website: "http://spam.example"));
        Assert.Empty(mail.Sent);
    }

    [Fact]
    public async Task User_input_is_html_encoded()
    {
        var mail = new CaptureEmail();
        await new ContactService(mail).SubmitAsync(Req(message: "<script>alert(1)</script>"));
        Assert.DoesNotContain("<script>", mail.Sent[0].Body);
        Assert.Contains("&lt;script&gt;", mail.Sent[0].Body);
    }

    [Fact]
    public async Task A_name_cannot_inject_a_header_line()
    {
        var mail = new CaptureEmail();
        await new ContactService(mail).SubmitAsync(Req(first: "Ada\r\nBcc: victim@example.com"));
        Assert.DoesNotContain("\n", mail.Sent[0].Subject);
        Assert.DoesNotContain("\r", mail.Sent[0].Subject);
    }
}
