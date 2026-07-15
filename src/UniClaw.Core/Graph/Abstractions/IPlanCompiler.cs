using UniClaw.Core.Graph.Models;

namespace UniClaw.Core.Graph.Abstractions;

/// <summary>
/// 计划编译器接口 — 确定性 IntentSlots → TraversalPlan 映射，无 AI 依赖。
/// </summary>
public interface IPlanCompiler
{
    /// <summary>
    /// compile — 6-step deterministic TraversalPlan generation from IntentSlots。
    /// </summary>
    TraversalPlan Compile(IntentSlots slots);
}
