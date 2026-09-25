using System.Security.Cryptography;
using System.Text;

namespace UniClaw.Agent.Dsh;

/// <summary>
/// Transport credential for the local DSH web service (127.0.0.1:3080).
///
/// The service authenticates browser-session requests with an authority-bound
/// HMAC-SHA256 signed cookie whose signing secret lives in the harness home's
/// secure credential store (<c>~/.dsh/.credentials.yaml</c>, record
/// <c>client-connection/browser-session</c>). A co-resident local Product
/// process presents that cookie after minting it from the same secure runtime
/// source — the same programmatic pattern DSH's own host tests use. The
/// launch-token browser flow is unavailable to a non-browser peer because the
/// process launch token never leaves the serving process.
///
/// Credential sources (fail closed, in order):
///  1. <c>UNICLAW_DSH_WEB_SECRET</c> environment variable (base64url secret);
///  2. the harness-home credentials record (path from
///     <c>UNICLAW_DSH_CREDENTIALS</c> or <c>~/.dsh/.credentials.yaml</c>).
///
/// Nothing here is a Product decision-channel authorization: transport
/// authentication only. The Product handshake (protocol stamp, capability
/// manifest, session identity) remains the channel's own fail-closed gate.
/// </summary>
public sealed record DshWebCredential(
    string CookieName,
    string CookieValue,
    DateTimeOffset ExpiresAt)
{
    public const string SecretEnvironmentVariable = "UNICLAW_DSH_WEB_SECRET";
    public const string CredentialsPathEnvironmentVariable = "UNICLAW_DSH_CREDENTIALS";
    public const string CredentialsRelativePath = ".dsh/.credentials.yaml";
    public const string BrowserSessionRecordKey = "client-connection/browser-session";
    public const int SecretByteLength = 32;

    /// <summary>Cookie header form for HTTP requests.</summary>
    public string CookieHeader => CookieName + "=" + CookieValue;

    /// <summary>
    /// Resolve the signing secret from environment or the harness-home
    /// credential store. Throws <see cref="InvalidOperationException"/> (fail
    /// closed) when no usable secret is present.
    /// </summary>
    public static byte[] ResolveSecret()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable(SecretEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
            return DecodeSecret(fromEnvironment, "environment:" + SecretEnvironmentVariable);
        return ReadSecretFromCredentials(CredentialsPath());
    }

    /// <summary>Mint the authority-bound signed cookie for an endpoint.</summary>
    public static DshWebCredential Mint(DshServiceEndpoint endpoint, DateTimeOffset? now = null)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        endpoint.Validate();
        var authority = endpoint.BaseUri.Authority;
        if (string.IsNullOrWhiteSpace(authority))
            throw new InvalidOperationException("dsh-web-credential-invalid:service-authority");

        var secret = ResolveSecret();
        var issuedAt = now ?? DateTimeOffset.UtcNow;
        var expiresAt = issuedAt.AddDays(1);
        var payload = "{\"version\":1,\"authority\":\"" + authority
            + "\",\"issuedAt\":" + issuedAt.ToUnixTimeMilliseconds()
            + ",\"expiresAt\":" + expiresAt.ToUnixTimeMilliseconds() + "}";
        var body = Base64UrlEncode(Encoding.UTF8.GetBytes(payload));
        var signature = Base64UrlEncode(HmacSha256(secret, Encoding.UTF8.GetBytes(body)));
        return new DshWebCredential(
            "dsh-auth-" + Base64UrlEncode(SHA256.HashData(Encoding.UTF8.GetBytes(authority))),
            "v1." + body + "." + signature,
            expiresAt);
    }

    /// <summary>
    /// Read the browser-session signing secret from a DSH credentials.yaml
    /// document: <c>records.{client-connection/browser-session}.payload.secret</c>
    /// (base64url, 32 bytes). Fails closed on any other shape.
    /// </summary>
    public static byte[] ReadSecretFromCredentials(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new InvalidOperationException($"dsh-web-credential-missing:{path}");
        var secret = DshCredentialsYaml.FindBrowserSessionSecret(File.ReadAllLines(path));
        if (secret is null)
            throw new InvalidOperationException(
                $"dsh-web-credential-invalid:{path}:no {BrowserSessionRecordKey} secret");
        return DecodeSecret(secret, path);
    }

    public static string CredentialsPath()
    {
        var configured = Environment.GetEnvironmentVariable(CredentialsPathEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configured))
            return configured;
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return string.IsNullOrEmpty(home)
            ? CredentialsRelativePath
            : Path.Combine(home, CredentialsRelativePath);
    }

    private static byte[] DecodeSecret(string value, string origin)
    {
        byte[] decoded;
        try
        {
            decoded = Base64UrlDecode(value);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException($"dsh-web-credential-invalid:{origin}:secret-not-base64url");
        }
        if (decoded.Length != SecretByteLength)
            throw new InvalidOperationException(
                $"dsh-web-credential-invalid:{origin}:secret-length-{decoded.Length}");
        return decoded;
    }

    private static byte[] HmacSha256(byte[] key, byte[] body)
    {
        using var hmac = new HMACSHA256(key);
        return hmac.ComputeHash(body);
    }

    public static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    internal static byte[] Base64UrlDecode(string value)
    {
        var normalized = value.Replace('-', '+').Replace('/', '_');
        normalized += new string('=', (4 - normalized.Length % 4) % 4);
        return Convert.FromBase64String(normalized);
    }

    /// <summary>
    /// Minimal reader for the browser-session secret in a DSH credentials.yaml
    /// document (block-mapping subset: <c>records:</c> mapping of record keys,
    /// nested <c>payload:</c> mapping, scalar values). Fails closed on any
    /// other shape by returning null.
    /// </summary>
    internal static class DshCredentialsYaml
    {
        internal static string? FindBrowserSessionSecret(string[] lines)
        {
            var inRecords = false;
            var inRecordBlock = false;
            foreach (var rawLine in lines)
            {
                var line = rawLine.TrimEnd();
                if (line.Length is 0 || line.TrimStart().StartsWith('#'))
                    continue;
                var indent = line.Length - line.TrimStart().Length;
                var content = line.TrimStart();

                if (indent == 0)
                {
                    inRecords = StartsWithKey(content, "records");
                    inRecordBlock = false;
                    continue;
                }
                if (!inRecords || indent != 2)
                    continue;
                inRecordBlock = StartsWithKey(content, BrowserSessionRecordKey);
                if (inRecordBlock)
                {
                    var nested = FindSecretInRecordBlock(lines, rawLine);
                    if (nested is not null)
                        return nested;
                }
            }
            return null;
        }

        private static string? FindSecretInRecordBlock(string[] lines, string recordKeyLine)
        {
            var startIndex = Array.IndexOf(lines, recordKeyLine);
            var recordIndent = recordKeyLine.Length - recordKeyLine.TrimStart().Length;
            var inPayload = false;
            for (var index = startIndex + 1; index < lines.Length; index++)
            {
                var line = lines[index].TrimEnd();
                if (line.Length is 0 || line.TrimStart().StartsWith('#'))
                    continue;
                var indent = line.Length - line.TrimStart().Length;
                var content = line.TrimStart();
                if (indent <= recordIndent)
                    return null; // record block ended without a secret
                if (indent == recordIndent + 2)
                {
                    inPayload = StartsWithKey(content, "payload");
                    continue;
                }
                if (inPayload && indent == recordIndent + 4
                    && StartsWithKey(content, "secret"))
                {
                    var value = content["secret:".Length..].Trim().Trim('"').Trim('\'');
                    return value.Length is 0 ? null : value;
                }
            }
            return null;
        }

        private static bool StartsWithKey(string content, string key) =>
            content.StartsWith(key + ":", StringComparison.Ordinal);
    }
}
