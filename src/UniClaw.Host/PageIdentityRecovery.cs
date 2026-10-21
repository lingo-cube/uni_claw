using System.Collections.Frozen;
using System.Text.Json;
using System.Xml.Linq;
using UniClaw.Kernel.Capability;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Perception.UiHierarchy;
using UniClaw.Kernel.World;

namespace UniClaw.Host;

/// <summary>Observation adapter only: preserves source layout and projects a
/// supplied title selection. World decides identity; Assurance decides arrival.</summary>
internal static class PageIdentityRecovery
{
    internal static PageCaptureObservation Capture(string captureId, string cycle, string session,
        DateTimeOffset time, int width, int height, string? xml, FastTextBasis? fast)
    {
        var items = new List<SemanticLayoutItem>();
        if (xml is not null)
        {
            try
            {
                foreach (var n in XDocument.Parse(xml).Descendants("node"))
                {
                    var raw = (string?)n.Attribute("bounds");
                    var v = raw?.Replace("][", ",").Trim('[', ']').Split(',');
                    if (v?.Length != 4 || !v.All(x => int.TryParse(x, out _))) continue;
                    var b = v.Select(int.Parse).ToArray();
                    if (b[0] < 0 || b[1] < 0 || b[2] <= b[0] || b[3] <= b[1] || b[2] > width || b[3] > height) continue;
                    var text = (string?)n.Attribute("text");
                    if (string.IsNullOrWhiteSpace(text)) text = (string?)n.Attribute("content-desc");
                    var role = (string?)n.Attribute("clickable") == "true" ? "ui.element" : "text";
                    if (string.IsNullOrWhiteSpace(text) && role == "text") continue;
                    items.Add(new($"xml-{items.Count}", role, text?.Trim(), new(b[0], b[1], b[2], b[3])));
                }
            }
            catch (System.Xml.XmlException) { /* Invalid XML supplies no items. */ }
        }
        // Prefer one source for the current layout. Never join different captures.
        if (items.Count == 0 && fast is { ProviderAvailable: true })
        {
            foreach (var raw in fast.OcrTokens.Concat(fast.YoloDetections))
            {
                try
                {
                    using var json = JsonDocument.Parse(raw);
                    var n = json.RootElement;
                    var b = n.GetProperty("boundsPx").EnumerateArray().Select(v => v.GetInt32()).ToArray();
                    if (b.Length != 4 || b[0] < 0 || b[1] < 0 || b[2] <= b[0] || b[3] <= b[1] || b[2] > width || b[3] > height) continue;
                    var isText = n.TryGetProperty("text", out var t);
                    var text = isText ? t.GetString() : null;
                    var role = isText ? "text" : n.GetProperty("label").GetString()!;
                    items.Add(new($"vision-{items.Count}", role, text, new(b[0], b[1], b[2], b[3])));
                }
                catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or FormatException) { }
            }
        }
        return new(captureId, cycle, session, time,
            CoordinateSpace.DeviceViewport(width, height, captureId: captureId), items.AsReadOnly());
    }

    internal static ObservationProposal? Identity(PageObservationState source, string? title,
        string? signature, ObservationContext context, string producer = "host.page-identity")
    {
        var c = source.Capture;
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(signature)
            || c.Items.Count < 2 || !c.Items.Any(i => i.Text?.Trim() == title.Trim())) return null;
        var payload = new PageIdentityObservation(c.CaptureId, c.ObservationCycleId,
            c.SessionCorrelation, c.CaptureTime, signature, title);
        return new(new(PageObservationCodec.IdentitySubject, PageObservationCodec.Encode(payload)),
            IngressKind.Observation, context, new(producer, c.CaptureTime,
                $"identity:{c.CaptureId}", ["capture:" + c.CaptureId, "source-layout-title"]))
        {
            DerivedFromEvidenceIds = new[] { source.EvidenceId },
            TransitiveEvidenceBasis = new[] { source.EvidenceId }.ToFrozenSet(StringComparer.Ordinal),
            FusionRule = "page.identity.observed-title-layout", FusionVersion = "1",
        };
    }

    internal static async Task<(string? Title, IReadOnlyList<ObservationProposal> Meaning)> RecognizeAsync(
        ISemanticPerception? perception, IReadOnlyList<SemanticPageCandidate> candidates,
        ObservationNeed need, TimeSpan budget, ObservationContext context, CancellationToken token)
    {
        if (perception is null || candidates.Count == 0 || budget <= TimeSpan.Zero) return (null, []);
        var c = need.Capture;
        var request = new SemanticPerceptionRequest($"page-identity:{c.CaptureId}:{Guid.NewGuid():N}",
            new CapabilityCorrelation(c.SessionCorrelation, captureId: c.CaptureId, observationCycleId: c.ObservationCycleId),
            new(c.CaptureId, c.ObservationCycleId, c.SessionCorrelation, c.CaptureTime, c.Space,
                c.Items, [need.CaptureEvidenceId]), candidates, Budget: budget);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(budget);
        try
        {
            var result = await perception.ObserveSemanticsAsync(request, timeout.Token).AsTask()
                .WaitAsync(budget, token).ConfigureAwait(false);
            var meaning = SemanticObservationProjector.CreateProposals(request, result, context: context);
            return (meaning.Count > 0 ? result.Observation?.ObservedTitle : null, meaning);
        }
        catch (Exception e) when (e is TimeoutException or OperationCanceledException) { return (null, []); }
    }
}
