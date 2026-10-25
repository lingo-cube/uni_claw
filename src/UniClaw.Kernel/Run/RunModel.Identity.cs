using System.Collections.Frozen;
using System.Collections.Immutable;

namespace UniClaw.Kernel.Run;

/// <summary>Original Owner identity facts, not a RunState or activation checkpoint.</summary>
public sealed record RunIdentityRecord(string Schema, string RunId, bool RuntimeAssigned,
    string Version, string Objective, string[] Scope, string[] AllowedEffects,
    string[] ForbiddenEffects, string[] ProofCriteria, int MaxConsultations, int MaxTotalSteps,
    BudgetValueOrigin ConsultationsOrigin, BudgetValueOrigin StepsOrigin,
    string TaskInstanceId, string ProductSessionId, string ContractRef,
    string? AcceptedContractViewRef, int? ContractGeneration,
    string ScopeComparer = "ordinal", string AllowedEffectsComparer = "ordinal", string ForbiddenEffectsComparer = "ordinal");

public sealed partial class RunModel
{
    private const string IdentitySchema = "uniclaw.run.accepted-identity.v2";
    private bool _identityRuntimeAssigned;

    internal RunIdentityRecord CaptureIdentity()
    {
        var view = View ?? throw new InvalidOperationException("run-identity-not-accepted");
        return CaptureIdentity(view, RunId, _identityRuntimeAssigned);
    }

    private static RunIdentityRecord CaptureIdentity(ExecutionContractView view, string runId, bool runtimeAssigned) =>
        new(IdentitySchema, runId, runtimeAssigned, view.Version, view.Objective,
            view.Scope.Order(StringComparer.Ordinal).ToArray(), view.AllowedEffects.Order(StringComparer.Ordinal).ToArray(),
            view.ForbiddenEffects.Order(StringComparer.Ordinal).ToArray(), view.ProofCriteria.ToArray(),
            view.MaxConsultations, view.MaxTotalSteps, view.MaxConsultationsOrigin, view.MaxTotalStepsOrigin,
            view.TaskInstanceId!, view.ProductSessionId!, view.ContractRef!, view.AcceptedContractViewRef, view.ContractGeneration,
            ComparerName(view.Scope), ComparerName(view.AllowedEffects), ComparerName(view.ForbiddenEffects));

    // Pure historical validation. Does not construct a RunModel, admit a new
    // contract, restore canonical State or confer activation ownership.
    internal static ExecutionContractView ReadArchivedIdentity(RunIdentityRecord record, string expectedRunId)
    {
        if ((record.Schema != IdentitySchema && record.Schema != "uniclaw.run.accepted-identity.v1") || string.IsNullOrWhiteSpace(expectedRunId) || record.RunId != expectedRunId)
            throw new InvalidDataException("run-identity-scope");
        if (record.ConsultationsOrigin is not (BudgetValueOrigin.Configured or BudgetValueOrigin.Default)
            || record.StepsOrigin is not (BudgetValueOrigin.Configured or BudgetValueOrigin.Default)
            || (record.ConsultationsOrigin == BudgetValueOrigin.Default && record.MaxConsultations != DefaultMaxConsultations)
            || (record.StepsOrigin == BudgetValueOrigin.Default && record.MaxTotalSteps != DefaultMaxTotalSteps))
            throw new InvalidDataException("run-identity-budget-origin");
        var contract = new ExecutionContract(record.Version, record.Objective,
            record.Scope.ToHashSet(ReadComparer(record.ScopeComparer)), record.AllowedEffects.ToHashSet(ReadComparer(record.AllowedEffectsComparer)),
            record.ForbiddenEffects.ToHashSet(ReadComparer(record.ForbiddenEffectsComparer)), record.ProofCriteria,
            MaxConsultations: record.MaxConsultations, MaxTotalSteps: record.MaxTotalSteps,
            TaskInstanceId: record.TaskInstanceId, ProductSessionId: record.ProductSessionId, ContractRef: record.ContractRef,
            AcceptedContractViewRef: record.AcceptedContractViewRef, ContractGeneration: record.ContractGeneration);
        if (!CheckContract(contract).All(check => check.Passed)
            || (!record.RuntimeAssigned && record.RunId != MintRunId(contract, record.MaxConsultations, record.MaxTotalSteps)))
            throw new InvalidDataException("run-identity-owner-facts");
        return new(record.Version, record.Objective, SnapshotSet(contract.Scope!), SnapshotSet(contract.AllowedEffects!),
            SnapshotSet(contract.ForbiddenEffects!), Array.AsReadOnly(record.ProofCriteria.ToArray()),
            record.MaxConsultations, record.MaxTotalSteps,
            record.ConsultationsOrigin == record.StepsOrigin ? record.StepsOrigin : BudgetValueOrigin.Mixed,
            record.TaskInstanceId, record.ProductSessionId, record.ContractRef, record.AcceptedContractViewRef, record.ContractGeneration)
        { MaxConsultationsOrigin = record.ConsultationsOrigin, MaxTotalStepsOrigin = record.StepsOrigin };
    }

    private static string ComparerName(IReadOnlySet<string> set)
    {
        object? comparer = set switch
        {
            HashSet<string> value => value.Comparer,
            FrozenSet<string> value => value.Comparer,
            ImmutableHashSet<string> value => value.KeyComparer,
            SortedSet<string> value => value.Comparer,
            ImmutableSortedSet<string> value => value.KeyComparer,
            _ => null,
        };
        if (Equals(comparer, StringComparer.Ordinal) || Equals(comparer, EqualityComparer<string>.Default)) return "ordinal";
        if (Equals(comparer, StringComparer.OrdinalIgnoreCase)) return "ordinal-ignore-case";
        throw new InvalidOperationException("unsupported-run-identity-comparer");
    }

    private static StringComparer ReadComparer(string name) => name switch
    {
        "ordinal" => StringComparer.Ordinal,
        "ordinal-ignore-case" => StringComparer.OrdinalIgnoreCase,
        _ => throw new InvalidDataException("unsupported-run-identity-comparer"),
    };

    private static List<ContractCheck> CheckContract(ExecutionContract contract) => new List<ContractCheck>
    {
            new("version-present", !string.IsNullOrWhiteSpace(contract.Version)),
            new("objective-present", !string.IsNullOrWhiteSpace(contract.Objective)),
            new("scope-nonempty", contract.Scope is { Count: > 0 }),
            new("allowed-effects-nonempty", contract.AllowedEffects is { Count: > 0 }),
            new("forbidden-effects-declared", contract.ForbiddenEffects is not null),
            new("proof-criteria-declared", contract.ProofCriteria is { Count: > 0 }),
            new("obligations-wellformed", contract.Obligations is null
                || (contract.Obligations.Count > 0
                    && contract.Obligations.All(o => !string.IsNullOrWhiteSpace(o.ObligationId)
                        && !string.IsNullOrWhiteSpace(o.Subject)
                        && !string.IsNullOrWhiteSpace(o.RequiredValue)))),
            // RUNTIME-IDENTITY-001（ADR-0046）：身份三件套必填——
            // Runtime Store 已接线、RuntimeHttpServer 已铸造、HostRunner
            /// 已沿载入契约；从留痕收紧为拒收（fail-closed）。
            new("identity-required",
                !string.IsNullOrWhiteSpace(contract.TaskInstanceId)
                && !string.IsNullOrWhiteSpace(contract.ProductSessionId)
                && !string.IsNullOrWhiteSpace(contract.ContractRef)),
        };

}
