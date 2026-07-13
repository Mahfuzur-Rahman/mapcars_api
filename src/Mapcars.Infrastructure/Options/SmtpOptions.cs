namespace Mapcars.Infrastructure.Options;

public class SmtpOptions
{
    public const string Section = "Email:Smtp";

    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 587;
    public string User { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string FromAddress { get; init; } = string.Empty;
    public string FromName { get; init; } = "MAP CARS";
    public string Bcc { get; init; } = string.Empty;

    // false = STARTTLS (port 587), true = SSL/TLS (port 465)
    public bool UseSsl { get; init; } = false;
}
