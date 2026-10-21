using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using UniClaw.Agent.Dsh;
using UniClaw.Agent.Profile;

namespace UniClaw.Agent.Dsh.Tests;

public sealed class ArtifactBindingTests
{
    private sealed class ResponseHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) => respond(request, cancellationToken);
    }

    [Fact]
    public async Task HandshakeSendsProfilePinsAndAcceptsMatchingReportedFingerprints()
    {
        var profile = UniAgentProfileYaml.LoadDefault();
        string? requestBody = null;
        var handler = new ResponseHandler(async (request, _) =>
        {
            requestBody = await request.Content!.ReadAsStringAsync();
            return Response(profile, ProductHandshake.CreateRequest("session", "run"));
        });
        await using var peer = Peer(handler, profile);

        var response = await peer.HandshakeAsync(
            ProductHandshake.CreateRequest("session", "run"), CancellationToken.None);

        Assert.True(response.Accepted);
        Assert.Equal(profile.Assembly!.PromptManifest!.Hash, response.ReportedArtifacts!.PromptHash);
        using var body = JsonDocument.Parse(requestBody!);
        var pins = body.RootElement.GetProperty("expectedArtifacts");
        Assert.Equal(profile.Assembly.PromptManifest.Hash, pins.GetProperty("promptHash").GetString());
        var skill = Assert.Single(profile.AllowedSkillRefs!);
        Assert.Equal(skill.Name, pins.GetProperty("skill").GetProperty("name").GetString());
        Assert.Equal(skill.Revision, pins.GetProperty("skill").GetProperty("revision").GetInt32());
        Assert.Equal(skill.Sha256, pins.GetProperty("skill").GetProperty("sha256").GetString());
    }

    [Fact]
    public async Task HandshakeRejectsMissingOrDriftedReportedArtifactFingerprint()
    {
        var profile = UniAgentProfileYaml.LoadDefault();
        var handler = new ResponseHandler((_, _) => Task.FromResult(Response(profile,
            ProductHandshake.CreateRequest("session", "run"), promptHash: new string('0', 64))));
        await using var peer = Peer(handler, profile);

        var response = await peer.HandshakeAsync(
            ProductHandshake.CreateRequest("session", "run"), CancellationToken.None);

        Assert.False(response.Accepted);
        Assert.Null(response.DshSessionId);
        Assert.Equal("artifact-fingerprint-mismatch", response.FailureReason);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("null")]
    [InlineData("revision-string")]
    public async Task HandshakeRejectsMissingOrMalformedReportedFingerprints(string variant)
    {
        var profile = UniAgentProfileYaml.LoadDefault();
        using var handler = new ResponseHandler(async (_, _) =>
        {
            using var valid = Response(profile, ProductHandshake.CreateRequest("session", "run"));
            var json = JsonNode.Parse(await valid.Content.ReadAsStringAsync())!.AsObject();
            if (variant == "missing") json.Remove("artifacts");
            else if (variant == "null") json["artifacts"] = null;
            else json["artifacts"]!["skill"]!["revision"] = "4";
            return new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(json.ToJsonString(), System.Text.Encoding.UTF8, "application/json") };
        });
        await using var peer = Peer(handler, profile);
        var result = await peer.HandshakeAsync(ProductHandshake.CreateRequest("session", "run"), CancellationToken.None);
        Assert.False(result.Accepted);
        Assert.Null(result.DshSessionId);
        Assert.Equal("artifact-fingerprint-mismatch", result.FailureReason);
    }

    private static DshOpenedHttpPeer Peer(HttpMessageHandler handler, UniAgentProfile profile) => new(
        new DshServiceEndpoint(new Uri("http://127.0.0.1:3080/", UriKind.Absolute)),
        credential: new DshWebCredential("dsh-auth-test", "v1.test.test", DateTimeOffset.UtcNow.AddDays(1)),
        handler: handler,
        expectedProfile: profile);

    private static HttpResponseMessage Response(
        UniAgentProfile profile, HandshakeRequest request, string? promptHash = null)
    {
        var skill = Assert.Single(profile.AllowedSkillRefs!);
        var payload = new
        {
            accepted = true,
            protocol = new
            {
                protocolVersion = request.Protocol.ProtocolVersion,
                schemaVersion = request.Protocol.SchemaVersion,
                schemaHash = request.Protocol.SchemaHash,
                profileId = request.Protocol.ProfileId,
                profileVersion = request.Protocol.ProfileVersion,
                capabilityManifestHash = request.Protocol.CapabilityManifestHash,
            },
            reportedCapabilities = new { capabilities = new[] { "submit_decision" } },
            runtimeCapabilities = new[] { "submit_decision" },
            artifacts = new
            {
                promptHash = promptHash ?? profile.Assembly!.PromptManifest!.Hash,
                skill = new { name = skill.Name, revision = skill.Revision, sha256 = skill.Sha256 },
            },
            dshSessionId = "dsh-session",
        };
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json"),
        };
    }
}
