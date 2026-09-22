using UniClaw.Kernel.Control;

namespace UniClaw.Kernel.World;

/// <summary>
/// PER-009 S7：事后验证 XML 路由（mechanism.md ⑦ 四门收紧版，D9）。
/// 纯函数：判"用 XML 验证还是回视觉验证"。
/// 四门（全部通过才走 XML）：
///   ① 目标属性 ∈ XML 权威域（状态型目标，DesiredState ≠ null）
///   ② IdentityMatched——dispatch 后重新唯一解析目标节点（防同名错配）
///   ③ PropertyValid（checked 需 checkable=true，D12 官方 guard）
///   ④ dump 时序在 dispatch 之后（事后新鲜度 = 时序约束 ≠ 操作前窗口）
/// 任一门不过 → 回视觉验证（XML 失去本次验证资格 ≠ 视觉必胜）。
/// </summary>
internal static class PostActionXmlRouter
{
    public sealed record RouteResult(
        bool UseXml,
        string? ResolvedState,    // {on,off,partial}（XML 定案值）；null = 回视觉
        string? XmlLocalId,       // 唯一解析到的节点 local id
        string Basis);            // 审计依据

    /// <summary>
    /// 四门路由。dump 来自 post-action 观察的 XML claims（feed 已产出）；
    /// dispatchTime 由调用方（StepVerify 阶段）保证在 dispatch 之后。
    /// 身份解析约定：TargetSpec.Role → XML 节点 resource-id 尾段匹配
    /// （黄金路径约定；真实设备校准后可扩展为显式映射表）。
    /// </summary>
    public static RouteResult Route(
        TargetSpec target,
        ConflictResolver.XmlAuthoritySnapshot? snapshot,
        DateTimeOffset dispatchTime,
        DateTimeOffset dumpTime)
    {
        ArgumentNullException.ThrowIfNull(target);

        // ---- 门④：时序（D9 收紧版核心：事后 = 时序约束，非窗口）----
        if (dumpTime <= dispatchTime)
            return FallBack("时序门：dump 未发生在 dispatch 之后（D9）");

        // ---- 门①：字段权威域（状态型目标才走 XML 验证）----
        if (target.DesiredState is null)
            return FallBack("字段门：目标无期望终态（Click 型），非状态权威域");

        // ---- 无 XML 快照 → 缺席即数据，不参战 ----
        if (snapshot is null)
            return FallBack("无 XML 快照（缺席即数据）");

        // ---- 门②：IdentityMatched（dispatch 后重新唯一解析）----
        if (!snapshot.IdentityUnique)
            return FallBack("身份门：非唯一解析（防同名控件错配 → 假验证，D9）");

        // ---- 门③：PropertyValid（checkable guard，D12 官方语义）----
        if (!snapshot.Checkable)
            return FallBack("属性门：checked 需 checkable=true（D12）");

        // ---- 四门全过：XML 验证，提取状态 ----
        var raw = snapshot.Checked;
        var resolved = raw switch { "true" => "on", "false" => "off", _ => raw };
        return new RouteResult(
            UseXml: true,
            ResolvedState: resolved,
            XmlLocalId: snapshot.LocalId,
            Basis: $"四门通过（状态域 ∧ 身份唯一 ∧ 属性有效 ∧ 时序正确）·"
                + $"XML checked={raw} → {resolved}");
    }

    private static RouteResult FallBack(string basis) =>
        new(false, null, null, basis);
}
