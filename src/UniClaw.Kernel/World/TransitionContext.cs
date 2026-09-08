namespace UniClaw.Kernel.World;

/// <summary>
/// Transition epistemic strength（P22 / UWM-009 §12）：actual runtime
/// attempt/effect 上下文的来源语义，保留 epistemic 腿，不与 accepted
/// evidence 压平（ADR-0012）。
/// </summary>
public enum TransitionStrength
{
    /// <summary>actual runtime attempt 已发生（attempt 腿）。</summary>
    Attempt,

    /// <summary>runtime effect flow 已发生（effect-flow 腿）。</summary>
    EffectFlow,
}

/// <summary>
/// P22 — Runtime Transition Context（Runtime Effect Flow → World Model；
/// ADR-0012 / UWM-009 §12.1）。actual runtime attempt/effect 的
/// non-evidentiary 上下文，仅作为 Container Association 的 candidate
/// prior/ranking 输入。不是 EvidenceRecord、不是 World claim、不是
/// ContainerIdentity truth；不得单独 establish Matched/New，不得
/// create/replace ContainerIdentity，不得 mutate canonical WorldState。
/// ControlIntent / 期望的 transition / Control plan 永不进入本类型。
/// ephemeral：单次 association 消费作用域，无独立生命周期。
/// </summary>
public sealed record TransitionContext(
    string TransitionKind,
    string AttemptCorrelation,
    TransitionStrength Strength);
