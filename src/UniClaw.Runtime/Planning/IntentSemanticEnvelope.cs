using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.Planning;

/// <summary>
/// Immutable upstream semantic projection of already-authoritative caller input.
/// It does not parse intent, select an execution method, or invoke the Runtime.
/// </summary>
public abstract record IntentSemanticEnvelope
{
    private IntentSemanticEnvelope()
    {
    }

    /// <summary>Projects complete caller input into a resolved semantic envelope.</summary>
    public static Resolved Project(string intent, Goal goal, IntentExecutionRepresentation representation)
        => new(intent, goal, representation);

    /// <summary>Projects an explicit caller insufficiency receipt without inventing an executable input.</summary>
    public static Insufficient Project(string intent, string insufficientReason)
        => new(intent, insufficientReason);

    /// <summary>Complete caller input with one truthful execution-representation variant.</summary>
    public sealed record Resolved : IntentSemanticEnvelope
    {
        /// <summary>Creates a validated resolved envelope.</summary>
        public Resolved(string intent, Goal goal, IntentExecutionRepresentation representation)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(intent);
            ArgumentNullException.ThrowIfNull(goal);
            ArgumentNullException.ThrowIfNull(representation);
            Intent = intent;
            Goal = goal;
            Representation = representation;
        }

        /// <summary>Caller-supplied intent text.</summary>
        public string Intent { get; }
        /// <summary>Caller-supplied goal; final completion authority remains with Agent.</summary>
        public Goal Goal { get; }
        /// <summary>Exactly one caller-supplied execution representation.</summary>
        public IntentExecutionRepresentation Representation { get; }
    }

    /// <summary>Explicit caller insufficiency receipt; it deliberately has no goal or execution representation.</summary>
    public sealed record Insufficient : IntentSemanticEnvelope
    {
        /// <summary>Creates a validated insufficiency receipt.</summary>
        public Insufficient(string intent, string reason)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(intent);
            ArgumentException.ThrowIfNullOrWhiteSpace(reason);
            Intent = intent;
            Reason = reason;
        }

        /// <summary>Caller-supplied intent text.</summary>
        public string Intent { get; }
        /// <summary>Caller-supplied reason that a resolved projection is unavailable.</summary>
        public string Reason { get; }
    }
}

/// <summary>Immutable union of truthful closed-world and open-world execution representations.</summary>
public abstract record IntentExecutionRepresentation
{
    private IntentExecutionRepresentation()
    {
    }

    /// <summary>Closed-world representation containing exactly the existing concrete Plan hypothesis.</summary>
    public sealed record ClosedWorldConcrete : IntentExecutionRepresentation
    {
        /// <summary>Creates a validated closed-world representation.</summary>
        public ClosedWorldConcrete(Plan plan)
        {
            ArgumentNullException.ThrowIfNull(plan);
            Plan = plan;
        }

        /// <summary>Exact caller-supplied concrete plan hypothesis.</summary>
        public Plan Plan { get; }
    }

    /// <summary>Open-world representation containing exactly the validated type-level specification.</summary>
    public sealed record OpenWorldTypeLevel : IntentExecutionRepresentation
    {
        /// <summary>Creates a validated open-world representation.</summary>
        public OpenWorldTypeLevel(TypeLevelTraversalSpecification specification)
        {
            ArgumentNullException.ThrowIfNull(specification);
            Specification = specification;
        }

        /// <summary>Exact caller-supplied open-world type-level specification.</summary>
        public TypeLevelTraversalSpecification Specification { get; }
    }
}
