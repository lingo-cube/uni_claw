namespace UniClaw.Kernel.Control;

/// <summary>
/// Tactical Hypothesis — disposable hypothesis（Target §14.2，不变量 32）。
/// 仅存在于 Control Loop 内部的 append-only log；不是事实、authorization、
/// Effect 或 Completion 的来源；不得进入 Assurance / Binding / Reconciliation
/// 的任何输入签名（验收 7）。可废弃性为派生判定：同 target 的后来假设
/// 取代先者（latest-wins），历史不改写（§17）。
/// </summary>
public sealed record TacticalHypothesis(
    string HypothesisId,
    string TargetSubject,
    string Expectation,
    string BasisRevisionId);
