namespace UniClaw.Runtime.Model;

/// <summary>
/// Trap 种类（HG-1 冻结；Phase 2 Trap 一等模型 — 裁决 4 购买）。
/// Trap 表达「可信世界信念缺失」的分类原因；发射 authority 在 Agent（Phase 2 仅 Agent scope 发射）。
/// 本枚举是纯数据定义（Model 层），不携带任何恢复语义（HG-2：无 Recoverability）。
/// </summary>
public enum TrapKind
{
    /// <summary>观测到的语义页面与期望语义入口不一致（UnexpectedPage）。</summary>
    UnexpectedPage = 0,

    /// <summary>世界状态丢失 / 不可信：观测无法建立可信世界信念（WorldLost）。</summary>
    WorldLost = 1,

    /// <summary>观测到的元素状态与期望状态不匹配（StateMismatch）。</summary>
    StateMismatch = 2,

    /// <summary>目标元素（候选）在当前观测中丢失（TargetLost）。</summary>
    TargetLost = 3,

    /// <summary>Plan 无效：步数据无法执行（PlanInvalid）。</summary>
    PlanInvalid = 4,

    /// <summary>容器语义身份不匹配（ContainerMismatch）。</summary>
    ContainerMismatch = 5,
}
