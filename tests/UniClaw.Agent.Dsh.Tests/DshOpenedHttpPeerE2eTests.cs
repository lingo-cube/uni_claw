using System.Text.Json;
using UniClaw.Agent.Dsh;
using UniClaw.Kernel.Runtime;

namespace UniClaw.Agent.Dsh.Tests;

/// <summary>
/// Live E2E against a real DSH web instance carrying the
/// <c>@uniclaw/dsh-decision-channel</c> profile plugin (AGT-002 E2E slice).
/// Gated on <c>UNICLAW_DSH_E2E_BASE</c> (e.g. <c>http://127.0.0.1:3081/</c>);
/// absent the environment the suite skips silently. Credentials come from the
/// harness home credential store via <see cref="DshWebCredential"/> — nothing
/// is committed. The default profile deployment model is used unless
/// <c>UNICLAW_DSH_E2E_MODEL</c> selects provider/model
/// (free|deepseekFlash|deepseekV41|deepseekV4Vision|glm53Flash).
/// </summary>
public sealed class DshOpenedHttpPeerE2eTests
{
    private const int OwnerServicePort = 3080;
    private static readonly string? BaseUrl = Environment.GetEnvironmentVariable("UNICLAW_DSH_E2E_BASE");

    static DshOpenedHttpPeerE2eTests()
    {
        if (string.IsNullOrWhiteSpace(BaseUrl)) return;
        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri))
            throw new InvalidOperationException($"UNICLAW_DSH_E2E_BASE is not an absolute URI: {BaseUrl}");
        if (uri.IsLoopback && uri.Port == OwnerServicePort)
        {
            throw new InvalidOperationException(
                $"Refusing to run live DSH E2E against the owner's port {OwnerServicePort}. " +
                "Start a dedicated test instance (for example on 3081) and set UNICLAW_DSH_E2E_BASE to it.");
        }
    }

    public static bool Available => !string.IsNullOrWhiteSpace(BaseUrl);

    private static DshServiceEndpoint Endpoint() =>
        new(new Uri(BaseUrl!, UriKind.Absolute));

    private static DshOpenedHttpPeer Peer() => new(
        Endpoint(),
        model: SelectModel());

    private static ModelConfiguration? SelectModel()
    {
        var selection = Environment.GetEnvironmentVariable("UNICLAW_DSH_E2E_MODEL");
        return selection switch
        {
            "free" => new ModelConfiguration("opencode-go", "space-bunny-free"),
            "deepseekFlash" => new ModelConfiguration("opencode-go", "deepseek-flash"),
            "deepseekV41" => new ModelConfiguration("opencode-go", "deepseek-v4.1-flash"),
            "glm53Flash" => new ModelConfiguration("zai-coding-cn", "glm-5.3-flash"),
            "deepseekV4Vision" => new ModelConfiguration("opencode-go", "deepseek-v4-flash-vision-exp"),
            _ => null, // deployment default (uniagent-prod.yaml default binding)
        };
    }

    private static AgentDecisionContext Context(
        string decisionId,
        string objective = "make-wifi-switch-on",
        AgentDecisionPhase phase = AgentDecisionPhase.InitialPlanning,
        string? failureReason = null,
        IReadOnlyList<(string Subject, string Value)>? claims = null) => new(
        decisionId, "e2e-run-1", "s1-v1", objective,
        new HashSet<string> { "tap" },
        (claims ?? Array.Empty<(string, string)>()).ToDictionary(
            c => c.Subject, c => new ClaimSummary(c.Value, "established", false)),
        Array.Empty<AgentObligationView>(),
        phase,
        FailureReason: failureReason,
        Screen: new ScreenSummary("screen-1", "sig-1"),
        Elements: new[]
        {
            new ElementSummary("switch", "WiFi", "0.1,0.1,0.2,0.2", true, true, true, ElementEpistemic.Observed, "checked"),
        },
        Progress: new ConsultationProgress(1, 0, 0),
        BudgetRemaining: new ConsultationBudget(3, 8));

    [Fact]
    public async Task E2E_Handshake_AcceptedOnRealDsh_WithFrozenStamp()
    {
        if (!Available) return; // E2E gate: UNICLAW_DSH_E2E_BASE not set
        await using var peer = Peer();
        var expected = ProductHandshake.CreateRequest("e2e-product-session-1", "e2e-run-1");

        var response = await peer.HandshakeAsync(expected, CancellationToken.None);
        var validation = ProductHandshake.Validate(expected, response);

        Assert.True(validation.Accepted, validation.FailureReason ?? "handshake rejected");
        Assert.False(string.IsNullOrWhiteSpace(response.DshSessionId));
        Assert.Equal("uniagent-prod", response.Protocol.ProfileId);
        Assert.Contains(ProductCapabilities.SubmitDecision,
            response.ReportedCapabilities.NormalizedCapabilities);
    }

    [Fact]
    public async Task E2E_Handshake_RejectsProfileDrift_OnRealDsh()
    {
        if (!Available) return; // E2E gate: UNICLAW_DSH_E2E_BASE not set
        await using var peer = Peer();
        var expected = ProductHandshake.CreateRequest("e2e-product-session-drift", "e2e-run-1");
        var drifted = expected with
        {
            Protocol = expected.Protocol with { ProfileId = "some-developer-profile" },
        };
        // The channel validates the CALLER's expected stamp against its own
        // frozen identity: a drifted expectation must not attach.
        var response = await peer.HandshakeAsync(drifted, CancellationToken.None);
        Assert.False(response.Accepted);
        Assert.Contains("profile-id-mismatch", response.FailureReason ?? "", StringComparison.Ordinal);
    }

    [Fact]
    public async Task E2E_Consult_ChannelRoundTrips_FailsClosedWithoutInventedDecisions()
    {
        if (!Available) return; // E2E gate: UNICLAW_DSH_E2E_BASE not set
        await using var channel = new DshOpenedDecisionChannel(Peer(), attachTimeout: TimeSpan.FromSeconds(20));
        var adapter = new DshAgentAdapter(channel, "e2e-product-session-1", "e2e-run-1",
            turnTimeout: TimeSpan.FromSeconds(110));

        var decision = await adapter.ConsultAsync(Context("decision-e2e-1"));

        // Whatever the real service answers — a captured real-model decision
        // or a fail-closed provider/credential/no-submit error — the adapter
        // must never invent a decision, and the diagnostics must say which.
        if (decision is null)
        {
            Assert.NotEmpty(adapter.Diagnostics);
            if (Environment.GetEnvironmentVariable("UNICLAW_DSH_E2E_MODEL") == "deepseekV41")
                Assert.Fail(string.Join("; ", adapter.Diagnostics.Select(d => $"{d.Code}:{d.Message}")));
        }
        else
        {
            var decisionId = AgentDecisionCorrelation.TryGetDecisionId(decision);
            Assert.Equal("decision-e2e-1", decisionId);
        }
        Assert.NotEqual(string.Empty, adapter.ProductSessionId);
    }

    [Fact]
    public async Task E2E_Abort_Mechanical_AbortWithoutAttachmentIsHarmless()
    {
        if (!Available) return; // E2E gate: UNICLAW_DSH_E2E_BASE not set
        await using var channel = new DshOpenedDecisionChannel(Peer(), attachTimeout: TimeSpan.FromSeconds(20));
        // Abort before attach must not throw and carries no lifecycle meaning.
        await channel.AbortCurrentTurnAsync(CancellationToken.None);
        Assert.False(channel.IsAttached);
    }

    [Fact]
    public async Task E2E_VisionModel_AcceptsSameCapturePngThroughDsh()
    {
        if (!Available || Environment.GetEnvironmentVariable("UNICLAW_DSH_E2E_MODEL") != "deepseekV4Vision") return;
        await using var channel = new DshOpenedDecisionChannel(Peer(), attachTimeout: TimeSpan.FromSeconds(20));
        // Reuse the single attached Product mapping left by the live profile;
        // the channel deliberately permits only one mapping at a time.
        var request = ProductHandshake.CreateRequest("e2e-product-session-1", "e2e-run-1");
        var attached = await channel.AttachAsync(request, CancellationToken.None);
        Assert.True(attached.Accepted, attached.FailureReason);

        var response = await channel.PushAsync(new DecisionRequest(
            "dsh-vision-1", 1, "e2e-product-session-1", "e2e-run-1",
            Context("decision-e2e-vision"),
            // 1x1 transparent PNG: proves the DSH image-content path without
            // creating a second capture or reading a filesystem artifact.
            Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=")),
            CancellationToken.None);

        // The exact vision model and image transport are reached, but the
        // existing Product decision channel intentionally fails closed because
        // this model did not submit the Product tool call. This is the current
        // structured-Slow gate, not a fabricated success.
        Assert.Null(response.Decision);
        Assert.Equal("no-submit-decision", response.Error);
    }

    [Fact]
    public async Task E2E_VisionModel_SlowRouteReturnsStructuredJson()
    {
        if (!Available || Environment.GetEnvironmentVariable("UNICLAW_DSH_E2E_MODEL") != "deepseekV4Vision") return;
        await using var peer = Peer();
        var request = ProductHandshake.CreateRequest("e2e-product-session-1", "e2e-run-1");
        var handshake = await peer.HandshakeAsync(request, CancellationToken.None);
        Assert.True(handshake.Accepted, "DSH handshake rejected: " + handshake.FailureReason);
        const string prompt = "Return exactly one JSON object and no markdown: {\"status\":\"Succeeded\",\"semanticDisposition\":\"Supported\",\"proposals\":[]}. The image is the same capture for this request.";
        var response = await peer.ExecuteSlowAsync(
            "slow-vision-e2e-1", prompt,
            new ModelConfiguration("opencode-go", "deepseek-v4-flash-vision-exp"),
            Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII="),
            CancellationToken.None);
        Assert.True(response.Error is null, $"{response.Error}:{response.Diagnostic}");
        Assert.NotNull(response.Text);
        using var json = JsonDocument.Parse(response.Text!);
        Assert.Equal("Succeeded", json.RootElement.GetProperty("status").GetString());
        await peer.DetachAsync(CancellationToken.None);
    }

    [Fact]
    public async Task E2E_TextModel_SlowRouteReturnsStructuredJson()
    {
        if (!Available || Environment.GetEnvironmentVariable("UNICLAW_DSH_E2E_MODEL") != "deepseekV41") return;
        await using var peer = Peer();
        var request = ProductHandshake.CreateRequest("e2e-product-session-1", "e2e-run-1");
        var handshake = await peer.HandshakeAsync(request, CancellationToken.None);
        Assert.True(handshake.Accepted, handshake.FailureReason);
        var response = await peer.ExecuteSlowAsync("slow-text-e2e-1",
            "Return exactly one JSON object and no markdown: {\"status\":\"Succeeded\",\"semanticDisposition\":\"Supported\",\"proposals\":[]}.",
            new ModelConfiguration("opencode-go", "deepseek-v4.1-flash"), cancellationToken: CancellationToken.None);
        Assert.Null(response.Error);
        Assert.NotNull(response.Text);
        using var json = JsonDocument.Parse(response.Text!);
        Assert.Equal("Succeeded", json.RootElement.GetProperty("status").GetString());
        await peer.DetachAsync(CancellationToken.None);
    }
}
