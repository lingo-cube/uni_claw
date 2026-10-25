using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UniClaw.Kernel.Run;

namespace UniClaw.Kernel.Runtime;

/// <summary>Trusted original-fact storage seam. Not an untrusted JSON admission API.</summary>
public interface IRunRecoveryArchive
{
    void Initialize(RunRecoveryRecord record);
    RunRecoveryRecord Read();
}

/// <summary>Original identity confirmation before external operations; accepted writes settle despite cancellation.</summary>
public interface IAsyncRunRecoveryArchive : IRunRecoveryArchive
{
    ValueTask InitializeAsync(RunRecoveryRecord record, CancellationToken cancellationToken = default);
}

public sealed record RunResourcePolicyRecord(bool GoalDirectedContinuationEnabled, int? HardCapTotalSteps,
    int? HardCapConsultations, int? AuthorizedTotalSteps, int? AuthorizedTotalConsultRounds,
    int RenewalThresholdSteps, int RenewalThresholdRounds)
{
    internal static RunResourcePolicyRecord Capture(AgentPlanPolicy policy) => new(policy.GoalDirectedContinuationEnabled,
        policy.HardCapTotalSteps, policy.HardCapConsultations, policy.AuthorizedTotalSteps,
        policy.AuthorizedTotalConsultRounds, policy.RenewalThresholdSteps, policy.RenewalThresholdRounds);
    internal string Fingerprint() => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(this))));
}

/// <summary>Original Owner and Runtime facts; not a serialized RunResourceBinding or RunState.</summary>
public sealed record RunRecoveryRecord(string Schema, RunIdentityRecord Run, RunResourceIdentity Identity,
    RunResourcePolicyRecord Policy, string RecordingId, string? RunStateRecordingId = null, string? EvidenceRecordingId = null,
    string? WorldRecordingId = null, string? ControlTraversalRecordingId = null, string? RuntimePlanRecordingId = null);

public static partial class RunResourceAccounting
{
    internal const string RecoverySchema = "uniclaw.runtime.original-identity.v5";

    /// <summary>Reconstitute only the read binding from the trusted original
    /// archive. No RunModel/Kernel is activated or restored as executable state.</summary>
    public static RunResourceBinding RestoreBinding(IRunRecoveryArchive archive, string expectedRunId)
    {
        var original = archive.Read();
        if ((original.Schema != RecoverySchema && original.Schema != "uniclaw.runtime.original-identity.v1"
                && original.Schema != "uniclaw.runtime.original-identity.v2" && original.Schema != "uniclaw.runtime.original-identity.v3"
                && original.Schema != "uniclaw.runtime.original-identity.v4")
            || !Guid.TryParseExact(original.RecordingId, "N", out _))
            throw new InvalidDataException("resource-original-identity-format");
        var view = RunModel.ReadArchivedIdentity(original.Run, expectedRunId);
        var identity = original.Identity;
        if (identity.RunId != original.Run.RunId || identity.TaskInstanceId != view.TaskInstanceId
            || identity.ProductSessionId != view.ProductSessionId || identity.ContractVersion != view.Version
            || identity.ContractViewRef != (view.AcceptedContractViewRef ?? view.ContractRef ?? view.Version)
            || identity.ContractGeneration != view.ContractGeneration || identity.InitialSteps != view.MaxTotalSteps
            || identity.InitialRounds != view.MaxConsultations || identity.PolicyFingerprint != original.Policy.Fingerprint())
            throw new InvalidDataException("resource-original-identity-conflict");
        return new(identity, original.RecordingId);
    }
}
