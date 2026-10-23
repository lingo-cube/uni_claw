namespace UniClaw.Kernel.World;

/// <summary>Presentation coordinates; deliberately not an executable SpatialLocator.</summary>
public sealed record WorldContentBounds(double X1, double Y1, double X2, double Y2);
public sealed record WorldContentItem(string PresentationRef, string ContainerId, string Role,
    string? SemanticDescriptor, string? CurrentState, string? ObservedState,
    string CurrentPresence, WorldContentBounds Bounds, string CaptureEvidenceId,
    DateTimeOffset CaptureTime, IReadOnlyList<string> ObservationOccurrenceRefs);
public sealed record WorldAccumulatedContent(string SourceRevisionId, string ContainerId,
    string? CurrentCaptureEvidenceId, IReadOnlyList<WorldContentItem> Items,
    IReadOnlyList<string> RegistrationSourceRefs, IReadOnlyList<string> Unknown);
