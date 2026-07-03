namespace UniClaw.Core.Graph.Models;

/// <summary>
/// 节点数据（遍历决策返回）— 从 AI 层移入 Graph 层 (F-1 修正)。
/// </summary>
/// <param name="NodeId">节点ID</param>
/// <param name="Action">操作</param>
/// <param name="Target">目标</param>
/// <param name="Reasoning">推理说明</param>
public sealed record class NodeData(
    string? NodeId = null,
    string? Action = null,
    object? Target = null,
    string? Reasoning = null);
