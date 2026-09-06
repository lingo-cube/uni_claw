using UniClaw.Kernel.Run;
using UniClaw.Kernel.World;

namespace UniClaw.Kernel.Control;

/// <summary>
/// Control Inputs — Control Intent Authority 的输入三元组
/// （Target §19：Contract View、WorldBelief Slice、Run State）。
/// </summary>
public sealed record ControlInputs(ExecutionContractView ContractView, Slice Slice, RunState RunState);

/// <summary>策略决定（不含 basis——basis 由 Control Loop 从 slice 填入）。</summary>
public sealed record ControlDecision(ControlIntentKind Kind, string? EffectClass, string? TargetSubject);

/// <summary>
/// 可注入的策略缝（D9）：intent 选择可无需 AI，用确定性策略表达。
/// 策略只产生 proposal；intent 的签发权仍在 Control Loop。
/// </summary>
public interface IControlPolicy
{
    ControlDecision Decide(ControlInputs inputs);
}
