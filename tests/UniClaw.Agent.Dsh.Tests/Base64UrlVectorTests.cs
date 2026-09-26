using System.Text;
using UniClaw.Agent.Dsh;

namespace UniClaw.Agent.Dsh.Tests;

/// <summary>
/// M1 regression: base64url decoding must map '-'→'+' and '_'→'/' (the
/// review found the second mapping reversed, so any signing secret containing
/// '_' decoded incorrectly). Known-answer vectors cover secrets containing
/// both alphabet extensions, through both the raw decode and the full
/// cookie-mint/signature path.
/// </summary>
[Collection("dsh-web-credential-environment")]
public sealed class Base64UrlVectorTests
{
    // 32-byte secrets whose base64url form contains '-' and '_' (generated
    // once, fixed forever): raw = FB EF BE 01..0D FB EF BE FF FE..F3.
    private const string SecretWithDashAndUnderscore1 =
        "----AQIDBAUGBwgJCgsMDfvvvv_-_fz7-vn49_b19PM";
    private const string SecretWithDashAndUnderscore2 =
        "_--_7wARIjNEVWZ3iJmqu8zd7v_-_fz7v9_v9_v9_v8";

    private static readonly DshServiceEndpoint LocalWeb =
        new(new Uri("http://127.0.0.1:3080/", UriKind.Absolute));

    private static readonly DateTimeOffset FixedNow =
        DateTimeOffset.FromUnixTimeMilliseconds(1790000000000);

    private static string? SetSecret(string? value)
    {
        var previous = Environment.GetEnvironmentVariable(DshWebCredential.SecretEnvironmentVariable);
        Environment.SetEnvironmentVariable(DshWebCredential.SecretEnvironmentVariable, value);
        return previous;
    }

    [Theory]
    [InlineData(SecretWithDashAndUnderscore1)]
    [InlineData(SecretWithDashAndUnderscore2)]
    public void Base64UrlDecode_RoundTripsSecretsContainingDashAndUnderscore(string secret)
    {
        var decoded = DshWebCredential.Base64UrlDecode(secret);

        Assert.Equal(DshWebCredential.SecretByteLength, decoded.Length);
        // Re-encoding must reproduce the exact base64url input.
        Assert.Equal(secret, DshWebCredential.Base64UrlEncode(decoded));
    }

    [Fact]
    public void Base64UrlDecode_ReversedMappingWouldFail_TheseVectors()
    {
        // The buggy implementation mapped '_'→'_' (no-op '/'→'_'); these
        // vectors must decode to DIFFERENT bytes than the 0x3F-saturated
        // pattern the bug produced. Guard by round-trip (above) plus a spot
        // check that a '_' byte decodes as 0b111111 only in the correct
        // alphabet position: decode of "_" padded is 63 (0b00111111) — the
        // buggy path made it invalid base64 (character '/' never produced).
        var decoded = DshWebCredential.Base64UrlDecode(SecretWithDashAndUnderscore2);
        Assert.Equal(0xFF, decoded[0]);
        Assert.Equal(0xEF, decoded[1]);
    }

    [Theory]
    [InlineData(SecretWithDashAndUnderscore1,
        "v1.eyJ2ZXJzaW9uIjoxLCJhdXRob3JpdHkiOiIxMjcuMC4wLjE6MzA4MCIsImlzc3VlZEF0IjoxNzkwMDAwMDAwMDAwLCJleHBpcmVzQXQiOjE3OTAwODY0MDAwMDB9.KxP0e0xXZPDMQ1ajkHNmDrw6Hbutv0ShZMXicEF0hnM")]
    [InlineData(SecretWithDashAndUnderscore2,
        "v1.eyJ2ZXJzaW9uIjoxLCJhdXRob3JpdHkiOiIxMjcuMC4wLjE6MzA4MCIsImlzc3VlZEF0IjoxNzkwMDAwMDAwMDAwLCJleHBpcmVzQXQiOjE3OTAwODY0MDAwMDB9.4eRd3biq4SoXpyCnFGTeNMJLbgSnJhSH_FVuuR-3aA0")]
    public void Mint_SignaturePath_SurvivesDashAndUnderscoreSecrets(string secret, string expectedValue)
    {
        var previous = SetSecret(secret);
        try
        {
            var credential = DshWebCredential.Mint(LocalWeb, FixedNow);

            Assert.Equal("dsh-auth-VPhEEcLKeqRDBoBalzN2Nm7CnfxKhLE00pKIDWxt1sw",
                credential.CookieName);
            Assert.Equal(expectedValue, credential.CookieValue);
        }
        finally
        {
            SetSecret(previous);
        }
    }
}
