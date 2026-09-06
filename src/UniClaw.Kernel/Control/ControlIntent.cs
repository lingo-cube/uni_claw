namespace UniClaw.Kernel.Control;

/// <summary>Control intent 种类（Target §14；D9 最小面）。</summary>
public enum ControlIntentKind
{
    /// <summary>触发一次观察（Observation Control 最小占位）。</summary>
    Observe,

    /// <summary>申请一次受约束的外部动作。</summary>
    Act,

    /// <summary>有界恢复：重新进入 Evidence/Belief/Assurance/Effect 闭环（D10）。</summary>
    Recovery,
}

/// <summary>
/// Control intent — Control Loop 的唯一产出（Target §14，不变量 21）。
/// act-intent 只携带 target hint（非 canonical binding）；与 Tactical
/// Hypothesis 零附着（P1）；BasisRevisionId 表达形成该 intent 所依据的
/// WorldBelief revision（Assurance freshness 判定输入）。
/// </summary>
public sealed record ControlIntent(
    string IntentId,
    ControlIntentKind Kind,
    string? EffectClass,
    string? TargetSubject,
    string BasisRevisionId);
