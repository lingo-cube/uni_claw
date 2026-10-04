namespace UniClaw.Kernel.Capability;

public enum CapabilityEventKind { PointEvent, OperationStarted, OperationTerminal }
public enum CapabilityStatus { Completed, Failed, Cancelled, TimedOut, Unknown, Unavailable, Late, Duplicate }
public enum AssociationDisposition { Associated, Missing, Late, Duplicate }
public enum FixtureLifecycleState { Setup, Ready, Failed, Teardown, Closed, Unknown, Unavailable }
public enum FindingDisposition { Pass, Violation, Unknown, NotApplicable }
public enum InspectorCoverage { Full, Partial, Missing, Unavailable, Mismatch }
public enum InspectorInputState { Accepted, MissingInput, Unavailable, Late }
public enum OperationMeasurementStage { Dispatch, Receipt, PostActionVerification }

public sealed record CapabilityCorrelation
{
    public string RunId { get; }
    public string? ParentEventId { get; }
    public string? DomainCorrelation { get; }
    public string? OperationId { get; }
    public string? ReceiptId { get; }
    public string? CaptureId { get; }
    public string? ObservationCycleId { get; }

    public CapabilityCorrelation(string runId, string? parentEventId = null, string? domainCorrelation = null,
        string? operationId = null, string? receiptId = null, string? captureId = null, string? observationCycleId = null)
    {
        RunId = Required(runId, nameof(runId));
        ParentEventId = Optional(parentEventId);
        DomainCorrelation = Optional(domainCorrelation);
        OperationId = Optional(operationId);
        ReceiptId = Optional(receiptId);
        CaptureId = Optional(captureId);
        ObservationCycleId = Optional(observationCycleId);
    }

    public bool HasExplicitAssociation => OperationId is not null || ReceiptId is not null || CaptureId is not null || ObservationCycleId is not null;
    private static string Required(string value, string parameter) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("A value is required.", parameter) : value.Trim();
    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record CapabilityEnvelope
{
    public string EventId { get; }
    public CapabilityCorrelation Correlation { get; }
    public long EventSeq { get; }
    public string SourceOwner { get; }
    public string SchemaVersion { get; }
    public CapabilityEventKind EventKind { get; }
    public CapabilityStatus Status { get; }
    public TimeSpan? RuntimeElapsed { get; }
    public long? HostMonotonicTime { get; }
    public long? ExternalDeviceTime { get; }
    public long? SensorTime { get; }

    public CapabilityEnvelope(string eventId, CapabilityCorrelation correlation, long eventSeq, string sourceOwner,
        string schemaVersion, CapabilityEventKind eventKind, CapabilityStatus status,
        TimeSpan? runtimeElapsed = null, long? hostMonotonicTime = null, long? externalDeviceTime = null, long? sensorTime = null)
    {
        EventId = Required(eventId, nameof(eventId));
        Correlation = correlation ?? throw new ArgumentNullException(nameof(correlation));
        if (eventSeq < 1) throw new ArgumentOutOfRangeException(nameof(eventSeq));
        if (runtimeElapsed < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(runtimeElapsed));
        EventSeq = eventSeq;
        SourceOwner = Required(sourceOwner, nameof(sourceOwner));
        SchemaVersion = Required(schemaVersion, nameof(schemaVersion));
        EventKind = eventKind;
        Status = status;
        RuntimeElapsed = runtimeElapsed;
        HostMonotonicTime = hostMonotonicTime;
        ExternalDeviceTime = externalDeviceTime;
        SensorTime = sensorTime;
    }

    private static string Required(string value, string parameter) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("A value is required.", parameter) : value.Trim();
}

public sealed record Finding
{
    public string FindingId { get; }
    public CapabilityCorrelation Correlation { get; }
    public string RuleVersion { get; }
    public string SourceOwner { get; }
    public FindingDisposition Disposition { get; }
    public CapabilityStatus Status { get; }
    public string Diagnostic { get; }
    public Finding(string findingId, CapabilityCorrelation correlation, string ruleVersion, string sourceOwner,
        FindingDisposition disposition, CapabilityStatus status, string diagnostic)
    { FindingId = Required(findingId, nameof(findingId)); Correlation = correlation ?? throw new ArgumentNullException(nameof(correlation)); RuleVersion = Required(ruleVersion, nameof(ruleVersion)); SourceOwner = Required(sourceOwner, nameof(sourceOwner)); Disposition = disposition; Status = status; Diagnostic = Required(diagnostic, nameof(diagnostic)); }
    private static string Required(string value, string parameter) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("A value is required.", parameter) : value.Trim();
}

public sealed record MeasurementSample
{
    public string SampleId { get; }
    public CapabilityCorrelation Correlation { get; }
    public string SourceOwner { get; }
    public string MetricName { get; }
    public string Unit { get; }
    public string MeasurementVersion { get; }
    public double? Value { get; }
    public TimeSpan? RuntimeElapsed { get; }
    public long? HostMonotonicTime { get; }
    public long? ExternalDeviceTime { get; }
    public long? SensorTime { get; }
    public CapabilityStatus Status { get; }
    public AssociationDisposition Association { get; }
    public string? Diagnostic { get; }

    public MeasurementSample(string sampleId, CapabilityCorrelation correlation, string sourceOwner, string metricName, string unit, string measurementVersion,
        double? value, CapabilityStatus status, AssociationDisposition association = AssociationDisposition.Associated,
        TimeSpan? runtimeElapsed = null, long? hostMonotonicTime = null, long? externalDeviceTime = null, long? sensorTime = null, string? diagnostic = null)
    {
        SampleId = Required(sampleId, nameof(sampleId));
        Correlation = correlation ?? throw new ArgumentNullException(nameof(correlation));
        if (!correlation.HasExplicitAssociation) throw new ArgumentException("A measurement requires explicit association.", nameof(correlation));
        SourceOwner = Required(sourceOwner, nameof(sourceOwner));
        MetricName = Required(metricName, nameof(metricName));
        Unit = Required(unit, nameof(unit));
        MeasurementVersion = Required(measurementVersion, nameof(measurementVersion));
        if (runtimeElapsed < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(runtimeElapsed));
        Value = value;
        Status = status;
        Association = association;
        RuntimeElapsed = runtimeElapsed;
        HostMonotonicTime = hostMonotonicTime;
        ExternalDeviceTime = externalDeviceTime;
        SensorTime = sensorTime;
        Diagnostic = string.IsNullOrWhiteSpace(diagnostic) ? null : diagnostic.Trim();
    }
    private static string Required(string value, string parameter) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("A value is required.", parameter) : value.Trim();
}

public sealed record OperationMeasurementStart
{
    public CapabilityEnvelope Envelope { get; }
    public OperationMeasurementStage Stage { get; }
    public OperationMeasurementStart(CapabilityEnvelope envelope, OperationMeasurementStage stage)
    { Envelope = Validate(envelope, CapabilityEventKind.OperationStarted); Stage = stage; }
    private static CapabilityEnvelope Validate(CapabilityEnvelope envelope, CapabilityEventKind kind)
        => envelope is null ? throw new ArgumentNullException(nameof(envelope)) : envelope.EventKind != kind ? throw new ArgumentException($"Envelope must be {kind}.", nameof(envelope)) : envelope;
}

public sealed record OperationMeasurementTerminal
{
    public CapabilityEnvelope Envelope { get; }
    public OperationMeasurementStage Stage { get; }
    public OperationMeasurementTerminal(CapabilityEnvelope envelope, OperationMeasurementStage stage)
    { Envelope = Validate(envelope); Stage = stage; }
    private static CapabilityEnvelope Validate(CapabilityEnvelope envelope)
    {
        if (envelope is null) throw new ArgumentNullException(nameof(envelope));
        if (envelope.EventKind != CapabilityEventKind.OperationTerminal) throw new ArgumentException("Envelope must be OperationTerminal.", nameof(envelope));
        if (envelope.Status is CapabilityStatus.Late or CapabilityStatus.Duplicate) throw new ArgumentException("Late or duplicate is an association disposition, not a terminal state.", nameof(envelope));
        return envelope;
    }
}

public sealed record OperationMeasurementPair
{
    public OperationMeasurementStart Start { get; }
    public OperationMeasurementTerminal Terminal { get; }
    private OperationMeasurementPair(OperationMeasurementStart start, OperationMeasurementTerminal terminal) { Start = start; Terminal = terminal; }
    public static OperationMeasurementPair Create(OperationMeasurementStart start, OperationMeasurementTerminal terminal)
    {
        ArgumentNullException.ThrowIfNull(start);
        ArgumentNullException.ThrowIfNull(terminal);
        if (start.Stage != terminal.Stage) throw new ArgumentException("Start and terminal stages must match.", nameof(terminal));
        if (start.Envelope.Correlation != terminal.Envelope.Correlation) throw new ArgumentException("Start and terminal correlation must match.", nameof(terminal));
        if (!start.Envelope.Correlation.HasExplicitAssociation) throw new ArgumentException("Operation measurement requires explicit association.", nameof(start));
        if (terminal.Envelope.EventSeq <= start.Envelope.EventSeq) throw new ArgumentException("Terminal event sequence must be after start.", nameof(terminal));
        return new(start, terminal);
    }
}

public sealed record ArtifactReference
{
    public string ArtifactId { get; }
    public CapabilityCorrelation Correlation { get; }
    public string SourceOwner { get; }
    public string ArtifactVersion { get; }
    public string Locator { get; }
    public string? MediaType { get; }
    public ArtifactReference(string artifactId, CapabilityCorrelation correlation, string sourceOwner, string artifactVersion, string locator, string? mediaType = null)
    { ArtifactId = Required(artifactId, nameof(artifactId)); Correlation = correlation ?? throw new ArgumentNullException(nameof(correlation)); SourceOwner = Required(sourceOwner, nameof(sourceOwner)); ArtifactVersion = Required(artifactVersion, nameof(artifactVersion)); Locator = Required(locator, nameof(locator)); MediaType = string.IsNullOrWhiteSpace(mediaType) ? null : mediaType.Trim(); }
    private static string Required(string value, string parameter) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("A value is required.", parameter) : value.Trim();
}

public sealed record ArtifactMeasurementBinding
{
    public ArtifactReference Artifact { get; }
    public MeasurementSample Measurement { get; }
    private ArtifactMeasurementBinding(ArtifactReference artifact, MeasurementSample measurement)
    { Artifact = artifact; Measurement = measurement; }

    public static ArtifactMeasurementBinding Create(ArtifactReference artifact, MeasurementSample measurement)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentNullException.ThrowIfNull(measurement);
        if (artifact.Correlation != measurement.Correlation)
            throw new ArgumentException("Artifact and measurement correlation must match.", nameof(measurement));
        return new(artifact, measurement);
    }
}

public sealed record FixtureLifecycleFact
{
    public string FixtureId { get; }
    public string SourceOwner { get; }
    public FixtureLifecycleState State { get; }
    public string FixtureVersion { get; }
    public string? Diagnostic { get; }
    public FixtureLifecycleFact(string fixtureId, string sourceOwner, FixtureLifecycleState state, string fixtureVersion, string? diagnostic = null)
    { FixtureId = Required(fixtureId, nameof(fixtureId)); SourceOwner = Required(sourceOwner, nameof(sourceOwner)); State = state; FixtureVersion = Required(fixtureVersion, nameof(fixtureVersion)); Diagnostic = string.IsNullOrWhiteSpace(diagnostic) ? null : diagnostic.Trim(); }
    private static string Required(string value, string parameter) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("A value is required.", parameter) : value.Trim();
}

public sealed record ObservationTextItem
{
    public string OccurrenceId { get; }
    public string SourceReference { get; }
    public string? DeclaredText { get; }
    public string? RenderedText { get; }

    public ObservationTextItem(string occurrenceId, string sourceReference, string? declaredText, string? renderedText)
    {
        OccurrenceId = Required(occurrenceId, nameof(occurrenceId));
        SourceReference = Required(sourceReference, nameof(sourceReference));
        if (declaredText is null && renderedText is null) throw new ArgumentException("At least one text projection is required.");
        DeclaredText = declaredText;
        RenderedText = renderedText;
    }
    private static string Required(string value, string parameter) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("A value is required.", parameter) : value.Trim();
}

public sealed record LanguageInspectorRequest
{
    public CapabilityCorrelation Correlation { get; }
    public string RuleVersion { get; }
    public string ExpectedLanguage { get; }
    public InspectorCoverage Coverage { get; }
    public InspectorInputState InputState { get; }
    public IReadOnlyList<ObservationTextItem> Items { get; }

    public LanguageInspectorRequest(CapabilityCorrelation correlation, string ruleVersion, string expectedLanguage,
        InspectorCoverage coverage, InspectorInputState inputState, IEnumerable<ObservationTextItem>? items)
    {
        Correlation = correlation ?? throw new ArgumentNullException(nameof(correlation));
        RuleVersion = Required(ruleVersion, nameof(ruleVersion));
        ExpectedLanguage = Required(expectedLanguage, nameof(expectedLanguage));
        Coverage = coverage;
        InputState = inputState;
        Items = (items ?? Array.Empty<ObservationTextItem>()).ToArray();
    }

    public Finding CreateFinding(string findingId, string sourceOwner, FindingDisposition disposition, CapabilityStatus status, string diagnostic)
        => new(findingId, Correlation, RuleVersion, sourceOwner, disposition, status, diagnostic);

    private static string Required(string value, string parameter) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("A value is required.", parameter) : value.Trim();
}
