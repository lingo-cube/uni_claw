using UniClaw.Agent.Dsh;

namespace UniClaw.Agent.Dsh.Tests;

/// <summary>
/// Transport credential coverage (F-bridge prerequisite): deterministic
/// known-answer minting (mirror of the DSH browser-auth cookie contract:
/// dsh-auth-&lt;sha256(authority)&gt; = v1.&lt;payload&gt;.&lt;hmac-sha256&gt;),
/// credential-store parsing, and fail-closed behavior on missing/malformed
/// secrets. No real credentials are read; the environment override points at
/// fixture files.
/// </summary>
public sealed class DshWebCredentialTests
{
    private static readonly DshServiceEndpoint LocalWeb =
        new(new Uri("http://127.0.0.1:3080/", UriKind.Absolute));

    // Known-answer vector (fixed secret 0x01×32, fixed clock).
    private const string Secret0102 = "AQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQE";
    private static readonly DateTimeOffset FixedNow =
        DateTimeOffset.FromUnixTimeMilliseconds(1790000000000);

    private static string? ReplaceEnvironment(string? value)
    {
        var previous = Environment.GetEnvironmentVariable(DshWebCredential.SecretEnvironmentVariable);
        Environment.SetEnvironmentVariable(DshWebCredential.SecretEnvironmentVariable, value);
        return previous;
    }

    [Fact]
    public void Mint_ProducesAuthorityBoundSignedCookie_KnownAnswer()
    {
        var previous = ReplaceEnvironment(Secret0102);
        try
        {
            var credential = DshWebCredential.Mint(LocalWeb, FixedNow);

            Assert.Equal("dsh-auth-VPhEEcLKeqRDBoBalzN2Nm7CnfxKhLE00pKIDWxt1sw",
                credential.CookieName);
            Assert.Equal(
                "v1.eyJ2ZXJzaW9uIjoxLCJhdXRob3JpdHkiOiIxMjcuMC4wLjE6MzA4MCIsImlzc3VlZEF0IjoxNzkwMDAwMDAwMDAwLCJleHBpcmVzQXQiOjE3OTAwODY0MDAwMDB9.iHcsBGSkWO0ETskiorewrdSde6BhqhN3EyW6Xb2IZhc",
                credential.CookieValue);
            Assert.Equal(
                "dsh-auth-VPhEEcLKeqRDBoBalzN2Nm7CnfxKhLE00pKIDWxt1sw=v1.eyJ2ZXJzaW9uIjoxLCJhdXRob3JpdHkiOiIxMjcuMC4wLjE6MzA4MCIsImlzc3VlZEF0IjoxNzkwMDAwMDAwMDAwLCJleHBpcmVzQXQiOjE3OTAwODY0MDAwMDB9.iHcsBGSkWO0ETskiorewrdSde6BhqhN3EyW6Xb2IZhc",
                credential.CookieHeader);
            Assert.Equal(FixedNow.AddDays(1), credential.ExpiresAt);
        }
        finally
        {
            ReplaceEnvironment(previous);
        }
    }

    [Fact]
    public void ResolveSecret_FailsClosed_WhenNoSecretAvailable()
    {
        var previousSecret = ReplaceEnvironment(null);
        var previousPath = Environment.GetEnvironmentVariable(
            DshWebCredential.CredentialsPathEnvironmentVariable);
        var missing = Path.Combine(Path.GetTempPath(), "uniclaw-missing-credentials.yaml");
        if (File.Exists(missing)) File.Delete(missing);
        Environment.SetEnvironmentVariable(
            DshWebCredential.CredentialsPathEnvironmentVariable, missing);
        try
        {
            var failure = Assert.Throws<InvalidOperationException>(
                () => DshWebCredential.ResolveSecret());
            Assert.StartsWith("dsh-web-credential-missing:", failure.Message);
        }
        finally
        {
            ReplaceEnvironment(previousSecret);
            Environment.SetEnvironmentVariable(
                DshWebCredential.CredentialsPathEnvironmentVariable, previousPath);
        }
    }

    [Fact]
    public void ResolveSecret_FailsClosed_OnMalformedSecret()
    {
        var previous = ReplaceEnvironment("not-valid-base64url!!");
        try
        {
            var failure = Assert.Throws<InvalidOperationException>(
                () => DshWebCredential.ResolveSecret());
            Assert.StartsWith("dsh-web-credential-invalid:", failure.Message);
        }
        finally
        {
            ReplaceEnvironment(previous);
        }
    }

    [Fact]
    public void ResolveSecret_FailsClosed_OnWrongSecretLength()
    {
        var shortSecret = DshWebCredential.Base64UrlEncode(new byte[8]);
        var previous = ReplaceEnvironment(shortSecret);
        try
        {
            var failure = Assert.Throws<InvalidOperationException>(
                () => DshWebCredential.ResolveSecret());
            Assert.Contains("secret-length-8", failure.Message, StringComparison.Ordinal);
        }
        finally
        {
            ReplaceEnvironment(previous);
        }
    }

    [Fact]
    public void ReadSecretFromCredentials_ParsesBrowserSessionRecord()
    {
        var path = Path.Combine(Path.GetTempPath(),
            "uniclaw-credentials-" + Guid.NewGuid().ToString("N") + ".yaml");
        var otherRecord = "client-connection/other-session";
        File.WriteAllLines(path, new[]
        {
            "version: 1",
            "refs: {}",
            "records:",
            $"  {otherRecord}:",
            "    kind: grant",
            "    payload:",
            "      version: 1",
            "      secret: " + DshWebCredential.Base64UrlEncode(new byte[32]),
            $"  {DshWebCredential.BrowserSessionRecordKey}:",
            "    kind: grant",
            "    payload:",
            "      version: 1",
            "      secret: " + Secret0102,
        });
        try
        {
            var secret = DshWebCredential.ReadSecretFromCredentials(path);
            Assert.Equal(32, secret.Length);
            Assert.All(secret, value => Assert.Equal(0x01, value));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ReadSecretFromCredentials_FailsClosed_WithoutBrowserSessionRecord()
    {
        var path = Path.Combine(Path.GetTempPath(),
            "uniclaw-credentials-" + Guid.NewGuid().ToString("N") + ".yaml");
        File.WriteAllLines(path, new[]
        {
            "version: 1",
            "records:",
            "  some/other-record:",
            "    kind: grant",
            "    payload:",
            "      version: 1",
            "      secret: " + Secret0102,
        });
        try
        {
            var failure = Assert.Throws<InvalidOperationException>(
                () => DshWebCredential.ReadSecretFromCredentials(path));
            Assert.StartsWith("dsh-web-credential-invalid:", failure.Message);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
