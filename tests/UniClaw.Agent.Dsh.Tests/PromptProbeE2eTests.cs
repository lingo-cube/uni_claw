using System.Text.Json;
using UniClaw.Agent.Dsh;
using Xunit;
using Xunit.Abstractions;

namespace UniClaw.Agent.Dsh.Tests;

/// <summary>
/// PRF-004 — live isolation probe (Q12: evidence + standing regression).
/// Asserts the uniagent-prod preset scope assembles ONLY product prompt
/// inputs: the frozen allowlist below is the mechanical definition of
/// "the product session sees no development-harness prompt surface".
/// Gated on <c>UNICLAW_DSH_E2E_BASE</c> like the peer E2E suite; the owner's
/// port 3080 is refused. Absent the environment the suite skips silently.
/// </summary>
public sealed class PromptProbeE2eTests
{
    private const int OwnerServicePort = 3080;
    private static readonly string? BaseUrl = Environment.GetEnvironmentVariable("UNICLAW_DSH_E2E_BASE");

    private readonly ITestOutputHelper _output;

    public PromptProbeE2eTests(ITestOutputHelper output) => _output = output;

    static PromptProbeE2eTests()
    {
        if (string.IsNullOrWhiteSpace(BaseUrl)) return;
        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri))
            throw new InvalidOperationException($"UNICLAW_DSH_E2E_BASE is not an absolute URI: {BaseUrl}");
        if (uri.IsLoopback && uri.Port == OwnerServicePort)
            throw new InvalidOperationException(
                $"Refusing live DSH E2E against the owner's port {OwnerServicePort}.");
    }

    public static bool Available => !string.IsNullOrWhiteSpace(BaseUrl);

    /// <summary>
    /// The frozen allowlist: every section name the product preset scope is
    /// allowed to assemble. Anything else (harness identity, deployment
    /// persona, AGENTS.md/skill instructions, runtime context snapshots,
    /// uniflow/provider-usage rows) is a development-surface leak = RED.
    /// Changing this list is a reviewed decision, never a test fix.
    /// </summary>
    private static readonly IReadOnlySet<string> AllowedSections = new HashSet<string>(
        StringComparer.Ordinal)
    {
        "uniagent-prod:product-prompt",
    };

    [Fact]
    public async Task Product_Preset_Scope_Assembles_Only_Product_Prompt_Sections()
    {
        if (!Available) return; // E2E gate: UNICLAW_DSH_E2E_BASE not set

        using var http = new HttpClient { BaseAddress = new Uri(BaseUrl!, UriKind.Absolute) };
        var credential = DshWebCredential.Mint(
            new DshServiceEndpoint(new Uri(BaseUrl!, UriKind.Absolute)));
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/uniclaw-agent/prompt-probe");
        request.Headers.TryAddWithoutValidation("Cookie", credential.CookieHeader);
        using var response = await http.SendAsync(request);
        var raw = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode,
            $"prompt-probe route failed: {response.StatusCode}: {raw}");
        _output.WriteLine($"prompt-probe raw: {raw}");
        using var document = JsonDocument.Parse(raw);
        var root = document.RootElement;

        Assert.True(root.TryGetProperty("recorded", out var recorded) && recorded.GetBoolean(),
            "probe not recorded — the session-scoped preset row did not assemble (is the preset row mounted?)");
        if (root.TryGetProperty("error", out var probeError)
            && probeError.ValueKind == JsonValueKind.String)
            Assert.Fail($"probe assembly failed: {probeError.GetString()}");

        var sections = root.GetProperty("sections").EnumerateArray()
            .Select(static section => section.GetString() ?? "").ToArray();
        var contexts = root.TryGetProperty("contexts", out var contextsNode)
            ? contextsNode.EnumerateArray().Select(static c => c.GetString() ?? "").ToArray() : [];
        _output.WriteLine($"prompt-probe sections: [{string.Join(", ", sections)}]");
        _output.WriteLine($"prompt-probe contexts: [{string.Join(", ", contexts)}]");

        var leaks = sections.Where(name => !AllowedSections.Contains(name)).Order(StringComparer.Ordinal).ToArray();
        Assert.True(leaks.Length == 0,
            "development-harness prompt sections leaked into the uniagent-prod preset scope: "
            + $"[{string.Join(", ", leaks)}]; allowed = [{string.Join(", ", AllowedSections)}]");
    }
}
