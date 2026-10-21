using System.Text.Json;
using System.Xml.Linq;
using UniClaw.Kernel.World;
using Xunit;

namespace UniClaw.Host.Tests;

public sealed class WorldReplayReconstructionTests
{
    [Fact]
    public void RecordedCorpus_ReplaysProductWorldAndProjectsCoordinatesBackToXml()
    {
        var pages = new List<ReplayPage>();
        var reports = new List<object>();
        var captures = Directory.GetFiles(Path.Combine(WorldReplayDiagnostics.Corpus, "uia"), "*.xml").Order().ToArray();
        Assert.Equal(39, captures.Length);
        foreach (var path in captures)
        {
            var capture = Path.GetFileNameWithoutExtension(path);
            var page = WorldReplayDiagnostics.Recorded(capture);
            var replay = WorldReplayDiagnostics.Recorded(capture);
            Assert.Equal(JsonSerializer.Serialize(page), JsonSerializer.Serialize(replay));
            Assert.NotEmpty(page.Slice.Occurrences);
            var nodes = XDocument.Load(path).Descendants("node").ToArray();
            var bounds = nodes.Select(WorldReplayDiagnostics.PixelBounds).ToArray();
            var rawControls = nodes.Where(n => n.Attribute("enabled")?.Value == "true"
                && new[] { "clickable", "checkable", "scrollable" }.Any(field => n.Attribute(field)?.Value == "true")).ToArray();
            var validControls = rawControls.Where(n =>
            {
                var px = WorldReplayDiagnostics.PixelBounds(n);
                return px.Length == 4 && px[0] <= px[2] && px[1] <= px[3];
            }).ToArray();
            // Independent raw-XML control count catches missing/extra projection
            // entries. Recorded inverted/clipped bounds are explicitly invalid,
            // not missing controls that the replay should invent coordinates for.
            Assert.Equal(validControls.Length, page.Slice.Occurrences.Count);
            var unmatchedControlBounds = validControls.Select(WorldReplayDiagnostics.PixelBounds).ToList();
            foreach (var o in page.Slice.Occurrences)
            {
                Assert.Equal(page.Slice.RootContainerId, o.OwningContainerId);
                var b = Assert.IsType<SpatialLocator>(o.Locator);
                var px = new[] { b.X1 * page.Width, b.Y1 * page.Height, b.X2 * page.Width, b.Y2 * page.Height };
                Assert.Contains(bounds, raw => raw.Zip(px).All(pair => Math.Abs(pair.First - pair.Second) < .000001));
                var matched = unmatchedControlBounds.FindIndex(raw => raw.Zip(px).All(pair => Math.Abs(pair.First - pair.Second) < .000001));
                Assert.True(matched >= 0, $"{capture}: unexpected or duplicate World control bounds");
                unmatchedControlBounds.RemoveAt(matched);
            }
            Assert.Empty(unmatchedControlBounds);
            var texts = nodes.Select(n => n.Attribute("text")?.Value).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToArray();
            var labels = page.Slice.Occurrences.Select(o => o.SemanticDescriptor).ToHashSet();
            var missingTexts = texts.Where(t => !labels.Contains(t)).ToArray();
            pages.Add(page);
            reports.Add(new
            {
                capture, xmlSha256 = WorldReplayDiagnostics.HashFile(path),
                screenshotSha256 = WorldReplayDiagnostics.HashFile(Path.Combine(WorldReplayDiagnostics.Corpus, "frames", capture + ".png")),
                nodeCount = nodes.Length, occurrenceCount = page.Slice.Occurrences.Count,
                invalidRawControlBounds = rawControls.Length - validControls.Length,
                distinctXmlTexts = texts.Length, exactTextsInWorldOccurrences = texts.Length - missingTexts.Length,
                missingTexts, page.Slice.SourceRevisionId,
                limitation = "Bounds round-trip verifies coordinate transport, not full-page semantic fidelity; World exposes actionable controls only",
            });
        }
        Assert.Contains(pages, page => page.Slice.Occurrences.Any(o => o.Role == "switch"));
        if (Output() is { } output)
        {
            File.WriteAllText(Path.Combine(output, "recorded-pages.json"), JsonSerializer.Serialize(reports, JsonOptions));
            WorldReplayGallery.Write(output, pages);
        }
    }

    [Fact]
    public void RelativeCoordinates_TranslateBothAxesBeyondViewportWithoutCreatingExecutableLocators()
    {
        var a = Synthetic("a", ("A", .4, .6), ("B", .5, .8));
        var b = Synthetic("b", ("A", .2, .3), ("B", .3, .5));
        ScrollSample Sample(ReplayPage p) => new(p, "body", new(0, 0, 1, 1), new HashSet<string>());
        var alignment = WorldReplayDiagnostics.Align(Sample(a), Sample(b));
        Assert.Equal("AlignedDiagnosticOnly", alignment.Status);
        Assert.Equal(.2, alignment.OffsetX!.Value, 9);
        Assert.Equal(.3, alignment.OffsetY!.Value, 9);
        var content = WorldReplayDiagnostics.ToContent(new SpatialLocator(.1, .9, .3, .95, "device-viewport"), .2, .3);
        Assert.Equal(1.2, content.Y1, 9);
        Assert.Equal(1.25, content.Y2, 9);
        Assert.Equal(.3, content.X1, 9);
    }

    [Fact]
    public void SuccessiveCaptures_KeepCurrentViewportSeparateFromHistoryAndNavigation()
    {
        var (world, ledger) = WorldReplayDiagnostics.NewWorld();
        var first = WorldReplayDiagnostics.Replay(world, ledger, Xml(("Alpha", 100), ("Beta", 300)), "first", "list", height: 1000);
        var frozen = JsonSerializer.Serialize(first);
        var second = WorldReplayDiagnostics.Replay(world, ledger, Xml(("Beta", 100), ("Gamma", 300)), "second", "list", height: 1000, step: 1);
        Assert.Equal(first.Slice.RootContainerId, second.Slice.RootContainerId);
        Assert.Equal(new[] { "Beta", "Gamma" }, second.Slice.Occurrences.Select(o => o.SemanticDescriptor));
        Assert.DoesNotContain(second.Slice.Occurrences, o => first.Slice.Occurrences.Any(p => p.OccurrenceId == o.OccurrenceId));
        var third = WorldReplayDiagnostics.Replay(world, ledger, Xml(("Delta", 100)), "third", "other", height: 1000, step: 2);
        Assert.NotEqual(second.Slice.RootContainerId, third.Slice.RootContainerId);
        Assert.Equal("Delta", Assert.Single(third.Slice.Occurrences).SemanticDescriptor);
        Assert.Equal(frozen, JsonSerializer.Serialize(first));
    }

    [Fact]
    public void RelativeCoordinates_RoundTripAcrossForwardReverseAndIndependentRegions()
    {
        var (world, ledger) = WorldReplayDiagnostics.NewWorld();
        var a = WorldReplayDiagnostics.Replay(world, ledger, Xml(("Header", 10), ("Alpha", 400), ("Beta", 600)), "a", "list", height: 1000);
        var b = WorldReplayDiagnostics.Replay(world, ledger, Xml(("Header", 10), ("Alpha", 200), ("Beta", 400), ("Gamma", 600)), "b", "list", height: 1000, step: 1);
        var c = WorldReplayDiagnostics.Replay(world, ledger, Xml(("Header", 10), ("Alpha", 300), ("Beta", 500), ("Gamma", 700)), "c", "list", height: 1000, step: 2);
        ScrollSample Sample(ReplayPage p) => new(p, "list-content", new(0, 0, 1, 1),
            p.Slice.Occurrences.Where(o => o.SemanticDescriptor == "Header").Select(o => o.OccurrenceId).ToHashSet());
        var forward = WorldReplayDiagnostics.Align(Sample(a), Sample(b));
        Assert.Equal("AlignedDiagnosticOnly", forward.Status);
        Assert.Equal(.2, forward.OffsetY!.Value, 9);
        var reverse = WorldReplayDiagnostics.Align(Sample(b), Sample(c));
        Assert.Equal("AlignedDiagnosticOnly", reverse.Status);
        Assert.Equal(-.1, reverse.OffsetY!.Value, 9);
        var gamma = c.Slice.Occurrences.Single(o => o.SemanticDescriptor == "Gamma").Locator!;
        var content = WorldReplayDiagnostics.ToContent(gamma, 0, forward.OffsetY.Value + reverse.OffsetY.Value);
        Assert.Equal(.8, content.Y1, 9); // worked content position, independent of implementation
        Assert.Equal(gamma.Y1, content.Y1 - .1, 9);
        Assert.DoesNotContain(forward.Anchors, key => key.Contains("Header", StringComparison.Ordinal));

        // Two regions in the same viewport have different translations. The
        // full-viewport alignment must fail rather than mix their coordinates.
        var bothA = Synthetic("both-a", ("L1", .1, .4), ("L2", .1, .6), ("R1", .6, .3), ("R2", .6, .5));
        var bothB = Synthetic("both-b", ("L1", .1, .2), ("L2", .1, .4), ("R1", .6, .4), ("R2", .6, .6));
        var leftA = Sample(bothA) with { RegionKey = "left", Region = new(0, .1, .5, 1) };
        var leftB = Sample(bothB) with { RegionKey = "left", Region = new(0, .1, .5, 1) };
        Assert.Equal(.2, WorldReplayDiagnostics.Align(leftA, leftB).OffsetY!.Value, 9);
        Assert.Equal(-.1, WorldReplayDiagnostics.Align(Sample(bothA) with { RegionKey = "right", Region = new(.5, .1, 1, 1) },
            Sample(bothB) with { RegionKey = "right", Region = new(.5, .1, 1, 1) }).OffsetY!.Value, 9);
        Assert.Equal("NotPureTranslation", WorldReplayDiagnostics.Align(Sample(bothA), Sample(bothB)).Status);
        Assert.Equal("DifferentRegion", WorldReplayDiagnostics.Align(leftA, leftB with { RegionKey = "right" }).Status);
        if (Output() is { } output)
        {
            File.WriteAllText(Path.Combine(output, "worked-scroll.json"), JsonSerializer.Serialize(new { forward, reverse, content }, JsonOptions));
            WorldReplayGallery.WriteScroll(output, new[] { a, b, c }, new[] { 0.0, forward.OffsetY.Value, forward.OffsetY.Value + reverse.OffsetY.Value });
        }
    }

    [Theory]
    [InlineData("no-overlap", "InsufficientOverlap")]
    [InlineData("one-anchor", "InsufficientOverlap")]
    [InlineData("duplicate", "AmbiguousAnchors")]
    [InlineData("reflow", "NotPureTranslation")]
    [InlineData("clipped", "NotPureTranslation")]
    [InlineData("state-change", "InsufficientOverlap")]
    [InlineData("frame", "FrameMismatch")]
    [InlineData("page", "DifferentPage")]
    [InlineData("dimensions", "DifferentDimensions")]
    public void UnsupportedScrollAlignment_ReportsReasonWithoutInventingOffset(string mutation, string expected)
    {
        var a = Synthetic("a", ("A", .1, .4), ("B", .1, .6));
        var b = Synthetic("b", ("A", .1, .2), ("B", .1, .4));
        var os = b.Slice.Occurrences.ToArray();
        if (mutation == "no-overlap") os = os.Select(o => o with { SemanticDescriptor = "Other " + o.SemanticDescriptor }).ToArray();
        if (mutation == "one-anchor") os = os.Take(1).ToArray();
        if (mutation == "duplicate") os = os.Append(os[0] with { OccurrenceId = "duplicate" }).ToArray();
        if (mutation == "state-change") os[0] = os[0] with { State = "checked" };
        if (mutation is "reflow" or "clipped" or "frame")
        {
            var p = os[0].Locator!;
            os[0] = os[0] with { Locator = new SpatialLocator(p.X1, p.Y1, p.X2,
                mutation == "frame" ? p.Y2 : p.Y2 + .02, mutation == "frame" ? "other-frame" : p.SpatialFrameId) };
        }
        b = b with { Slice = b.Slice with { Occurrences = os } };
        if (mutation == "page") b = b with { PageKey = "other" };
        if (mutation == "dimensions") b = b with { Height = 900 };
        ScrollSample Sample(ReplayPage p) => new(p, "body", new(0, 0, 1, 1), new HashSet<string>());
        var result = WorldReplayDiagnostics.Align(Sample(a), Sample(b));
        Assert.Equal(expected, result.Status);
        Assert.Null(result.OffsetX); Assert.Null(result.OffsetY);
    }

    [Theory]
    [InlineData("seq-scroll-apps")]
    [InlineData("seq-scroll-devopts")]
    public void RecordedScrollSequences_ExposeOverlapLimitations(string sequenceId)
    {
        using var sequence = JsonDocument.Parse(File.ReadAllText(Path.Combine(WorldReplayDiagnostics.Corpus, "sequences", sequenceId + ".json")));
        var ids = sequence.RootElement.GetProperty("frames").EnumerateArray().Select(x => x.GetString()!).ToArray();
        var (world, ledger) = WorldReplayDiagnostics.NewWorld();
        var pages = ids.Select((id, step) => WorldReplayDiagnostics.Replay(world, ledger,
            File.ReadAllText(Path.Combine(WorldReplayDiagnostics.Corpus, "uia", id + ".xml")), id, sequenceId, step: step)).ToArray();
        ScrollSample Sample(ReplayPage p)
        {
            // Explicit innermost list region. XML ancestry is not injected into the World output.
            var scroll = p.Slice.Occurrences.Where(o => o.Role == "scrollable")
                .OrderBy(o => o.Locator!.Y2 - o.Locator.Y1).First();
            return new(p, sequenceId + ":list", DiagnosticRect.Of(scroll.Locator!), new HashSet<string>());
        }
        var result = WorldReplayDiagnostics.Align(Sample(pages[0]), Sample(pages[1]));
        // Apps also repeats labels on row subcontrols; devopts has too little overlap.
        Assert.Equal(sequenceId == "seq-scroll-apps" ? "AmbiguousAnchors" : "InsufficientOverlap", result.Status);
        Assert.Null(result.OffsetY);
        if (Output() is { } output)
            File.WriteAllText(Path.Combine(output, sequenceId + ".json"), JsonSerializer.Serialize(new { result, pages }, JsonOptions));
    }

    private static ReplayPage Synthetic(string capture, params (string Label, double X, double Y)[] items) => new(capture, "page", 1000, 1000,
        new Slice("revision-" + capture, "container", new(WorldReplayDiagnostics.T0), new[] { "container" },
            items.Select((item, i) => new OccurrenceFact(capture + i, "container", "ui.element", item.Label,
                Locator: new SpatialLocator(item.X, item.Y, item.X + .2, item.Y + .05, "device-viewport"))).ToArray(), new Dictionary<string, string>()));

    private static string Xml(params (string Label, int Y)[] rows) => "<hierarchy>" + string.Concat(rows.Select(r =>
        $"<node text=\"{r.Label}\" class=\"android.widget.Button\" clickable=\"true\" enabled=\"true\" checkable=\"false\" scrollable=\"false\" bounds=\"[100,{r.Y}][400,{r.Y + 60}]\"/>")) + "</hierarchy>";

    private static string? Output()
    {
        var output = Environment.GetEnvironmentVariable("UNICLAW_WORLD_REPLAY_OUTPUT");
        if (string.IsNullOrWhiteSpace(output)) return null;
        Directory.CreateDirectory(output); return output;
    }
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
}
