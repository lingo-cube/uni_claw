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
/// <c>UNICLAW_DSH_E2E_MODEL</c> selects provider/model (free|deepseekFlash).
/// </summary>
public sealed class DshOpenedHttpPeerE2eTests
{
    private static readonly string? BaseUrl = Environment.GetEnvironmentVariable("UNICLAW_DSH_E2E_BASE");

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
            new ElementSummary("switch", "WiFi", "0.1,0.1,0.2,0.2", true, true, true, ElementEpistemic.Observed),
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
}
