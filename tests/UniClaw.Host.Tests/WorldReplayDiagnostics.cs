using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UniClaw.Host;
using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.Perception;
using UniClaw.Kernel.Perception.UiHierarchy;
using UniClaw.Kernel.World;
using UniClaw.Kernel.World.UiRealization;

namespace UniClaw.Host.Tests;

// Diagnostic projection only. Source revisions remain separate, immutable owner records.
internal sealed record ReplayPage(string CaptureId, string PageKey, int Width, int Height, Slice Slice);
internal sealed record DiagnosticRect(double X1, double Y1, double X2, double Y2)
{
    public bool Contains(SpatialLocator b) => b.X1 >= X1 && b.Y1 >= Y1 && b.X2 <= X2 && b.Y2 <= Y2;
    public static DiagnosticRect Of(SpatialLocator b) => new(b.X1, b.Y1, b.X2, b.Y2);
}
internal sealed record ScrollSample(ReplayPage Page, string RegionKey, DiagnosticRect Region,
    IReadOnlySet<string> FixedOccurrenceIds);
internal sealed record ScrollAlignment(string Status, double? OffsetX, double? OffsetY,
    IReadOnlyList<string> Anchors, string FromCapture, string ToCapture,
    string FromRevision, string ToRevision);

internal static class WorldReplayDiagnostics
{
    public static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "UniClaw.Kernel.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Repository root missing");
    }

    public static string Corpus => Path.Combine(RepoRoot(), "platforms/perception/evaluation/validation/fastscreen-v1");
    public static readonly DateTimeOffset T0 = new(2026, 9, 12, 10, 12, 44, TimeSpan.Zero);

    public static (WorldModel World, EvidenceLedger Ledger) NewWorld() =>
        (new WorldModel(new HashSet<string> { "ui.screen", "ui.node.*" },
            new ProductAssociationStrategy(), new UiHierarchyOccurrenceStrategy()), new EvidenceLedger());

    public static ReplayPage Replay(WorldModel world, EvidenceLedger ledger, string xml,
        string capture, string pageKey, int width = 1080, int height = 2400, int step = 0)
    {
        var time = T0.AddSeconds(step);
        void Accept(ObservationProposal p)
        {
            var (admission, record) = ledger.Admit(p);
            if (admission.Decision != AdmissionDecision.Accepted || record is null)
                throw new InvalidOperationException("Replay admission failed: " + admission.Decision);
            var relevance = world.JudgeRelevance(record);
            if (!relevance.IsRelevant) throw new InvalidOperationException(relevance.Reason);
            world.Reconcile(record, relevance);
        }
        Accept(new ObservationProposal(new ObservationClaim("ui.screen", pageKey),
            IngressKind.Observation, ObservationContext.External,
            new Provenance("replay.page.context", time, "capture:" + capture, new[] { "recorded-xml:" + capture })));
        var parsed = UiAutomatorDump.ParseHierarchyObservation(xml,
            new UiAutomatorDump.UiHierarchyParseContext(capture, time, "recorded-api35", "world-replay", 35,
                ObservationCycleId: "cycle:" + capture,
                Capabilities: UiAutomatorDump.LegacyXmlDefaultCapabilities,
                Space: CoordinateSpace.DeviceViewport(width, height)));
        if (parsed.Observation is null) throw new InvalidDataException("Hierarchy parsing failed");
        foreach (var proposal in TypedHierarchyProposalProjector.Project(parsed.Observation, ObservationContext.External))
            Accept(proposal);
        var root = world.Current!.WorldState[WorldModel.CurrentContainerSubject].Value;
        return new ReplayPage(capture, pageKey, width, height, world.DeriveSlice(root));
    }

    public static ReplayPage Recorded(string capture)
    {
        var (world, ledger) = NewWorld();
        return Replay(world, ledger, File.ReadAllText(Path.Combine(Corpus, "uia", capture + ".xml")), capture, capture);
    }

    public static double[] PixelBounds(XElement node) => Regex.Matches(node.Attribute("bounds")!.Value, @"-?\d+")
        .Select(match => double.Parse(match.Value, CultureInfo.InvariantCulture)).ToArray();

    public static string HashFile(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    // The sample explicitly declares the scroll region and fixed elements.
    // Geometry cannot recover DOM ancestry or decide which controls are sticky.
    public static IReadOnlyList<OccurrenceFact> Content(ScrollSample sample) => sample.Page.Slice.Occurrences
        .Where(o => o.Role != "scrollable" && o.Locator is { } b && sample.Region.Contains(b)
            && !sample.FixedOccurrenceIds.Contains(o.OccurrenceId)).ToArray();

    private static string Key(OccurrenceFact o) => JsonSerializer.Serialize(new[] { o.Role, o.SemanticDescriptor, o.State });

    public static ScrollAlignment Align(ScrollSample from, ScrollSample to)
    {
        ScrollAlignment Result(string status, double? x = null, double? y = null, string[]? anchors = null) =>
            new(status, x, y, anchors ?? [], from.Page.CaptureId, to.Page.CaptureId,
                from.Page.Slice.SourceRevisionId, to.Page.Slice.SourceRevisionId);
        if (from.Page.PageKey != to.Page.PageKey || from.Page.Slice.RootContainerId != to.Page.Slice.RootContainerId)
            return Result("DifferentPage");
        if (from.RegionKey != to.RegionKey) return Result("DifferentRegion");
        if (from.Page.Width != to.Page.Width || from.Page.Height != to.Page.Height) return Result("DifferentDimensions");
        var a = Content(from); var b = Content(to);
        var frames = a.Concat(b).Select(o => o.Locator!.SpatialFrameId).Distinct().ToArray();
        if (frames.Length != 1) return Result("FrameMismatch");
        var ag = a.Where(o => !string.IsNullOrWhiteSpace(o.SemanticDescriptor)).GroupBy(Key).ToDictionary(g => g.Key, g => g.ToArray());
        var bg = b.Where(o => !string.IsNullOrWhiteSpace(o.SemanticDescriptor)).GroupBy(Key).ToDictionary(g => g.Key, g => g.ToArray());
        var common = ag.Keys.Intersect(bg.Keys).Order(StringComparer.Ordinal).ToArray();
        if (common.Any(k => ag[k].Length != 1 || bg[k].Length != 1)) return Result("AmbiguousAnchors");
        if (common.Length < 2) return Result("InsufficientOverlap");
        var deltas = common.Select(k =>
        {
            var p = ag[k][0].Locator!; var q = bg[k][0].Locator!;
            return new[] { p.X1 - q.X1, p.Y1 - q.Y1, p.X2 - q.X2, p.Y2 - q.Y2 };
        }).ToArray();
        var dx = deltas[0][0]; var dy = deltas[0][1];
        // One pixel accommodates recorded clipping/rounding, never inferred gesture distance.
        if (deltas.Any(d => Math.Abs(d[0] - dx) > 1.0 / from.Page.Width
            || Math.Abs(d[2] - dx) > 1.0 / from.Page.Width
            || Math.Abs(d[1] - dy) > 1.0 / from.Page.Height
            || Math.Abs(d[3] - dy) > 1.0 / from.Page.Height)) return Result("NotPureTranslation");
        return Result("AlignedDiagnosticOnly", dx, dy, common);
    }

    // Unbounded content coordinates deliberately do not use executable SpatialLocator.
    public static DiagnosticRect ToContent(SpatialLocator b, double x, double y) =>
        new(b.X1 + x, b.Y1 + y, b.X2 + x, b.Y2 + y);
}
