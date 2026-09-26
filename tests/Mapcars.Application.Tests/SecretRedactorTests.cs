using Mapcars.Application.ErrorLogs;

namespace Mapcars.Application.Tests;

/// <summary>
/// The error log is admin-readable and long-lived. A key written there has to be
/// treated as leaked, so the redactor's misses are the expensive failure.
/// Test values are assembled at runtime so no secret-shaped literal sits in the
/// source for a scanner to flag.
/// </summary>
public class SecretRedactorTests
{
    private static readonly string Tail = new('x', 24);

    [Theory]
    [InlineData("sk_live_")]
    [InlineData("sk_test_")]
    [InlineData("rk_live_")]
    [InlineData("whsec_")]
    public void Stripe_secrets_are_masked(string prefix)
    {
        var secret = prefix + Tail;
        var output = SecretRedactor.Redact($"Invalid API Key provided: {secret}")!;
        Assert.DoesNotContain(secret, output);
        Assert.Contains(SecretRedactor.Mask, output);
    }

    [Fact]
    public void A_publishable_key_is_left_alone()
    {
        var key = "pk_test_" + Tail;
        Assert.Equal($"using {key}", SecretRedactor.Redact($"using {key}"));
    }

    [Fact]
    public void Intent_client_secrets_are_masked()
    {
        var secret = "pi_3Abc123_secret_" + Tail;
        Assert.DoesNotContain(secret, SecretRedactor.Redact($"confirm failed for {secret}")!);
    }

    [Fact]
    public void Connection_string_passwords_are_masked()
    {
        var output = SecretRedactor.Redact("Host=db;Username=app;Password=hunter2secret;SSL Mode=Require")!;
        Assert.DoesNotContain("hunter2secret", output);
        Assert.Contains("Host=db", output);
        Assert.Contains("SSL Mode=Require", output);
    }

    [Fact]
    public void Url_credentials_are_masked_but_the_host_survives()
    {
        var output = SecretRedactor.Redact("postgres://avnadmin:s3cretpass@pg.example.com:5432/db")!;
        Assert.DoesNotContain("s3cretpass", output);
        Assert.Contains("pg.example.com", output);
    }

    [Fact]
    public void A_port_is_not_mistaken_for_a_password()
        => Assert.Equal("https://api.example.com:443/v1", SecretRedactor.Redact("https://api.example.com:443/v1"));

    [Fact]
    public void Query_string_keys_are_masked()
    {
        var key = "AIza" + new string('B', 35);
        var output = SecretRedactor.Redact($"GET https://maps.googleapis.com/maps/api/directions/json?origin=1&key={key}")!;
        Assert.DoesNotContain(key, output);
        Assert.Contains("origin=1", output);
    }

    [Fact]
    public void Bearer_tokens_and_jwts_are_masked()
    {
        var jwt = "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjMifQ.c2lnbmF0dXJl";
        var output = SecretRedactor.Redact($"Authorization: Bearer {jwt}")!;
        Assert.DoesNotContain(jwt, output);
        Assert.DoesNotContain("eyJzdWIi", SecretRedactor.Redact($"token {jwt} rejected")!);
    }

    [Fact]
    public void Private_keys_are_masked()
    {
        var pem = "-----BEGIN PRIVATE KEY-----\nMIIEvQIBADANBgkqhkiG9w0BAQEFAASC\n-----END PRIVATE KEY-----";
        var output = SecretRedactor.Redact($"bad credential: {pem} end")!;
        Assert.DoesNotContain("MIIEvQ", output);
        Assert.EndsWith(" end", output);
    }

    [Fact]
    public void Ordinary_messages_pass_through_unchanged()
    {
        const string msg = "Trip 5c1e... must be in 'InProgress' status for this action.";
        Assert.Equal(msg, SecretRedactor.Redact(msg));
    }

    [Fact]
    public void Null_and_empty_pass_through()
    {
        Assert.Null(SecretRedactor.Redact(null));
        Assert.Equal("", SecretRedactor.Redact(""));
    }
}
