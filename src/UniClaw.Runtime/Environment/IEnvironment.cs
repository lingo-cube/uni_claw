using System.Threading;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.Environment;

/// <summary>
/// 外部世界能力边界端口（宪章 §8 / §56，Spine 最底层：Agent → Container → Traversal → Environment — I-1）。
/// 端口只回答「现在能看到什么（ObserveAsync）/ 请执行这个动作（ExecuteAsync）」，
/// 不回答「下一步做什么」——IEnvironment 不承担任何任务决策（specs/environment SHALL；§8：
/// Environment = Observation capabilities + Action capabilities，不拥有任务决策）。
/// 纯端口：无状态、无实现、无默认方法；具体适配实现（第一阶段为测试侧 ScriptedEnvironment Fake，§33）不在此文件。
/// </summary>
public interface IEnvironment
{
    /// <summary>
    /// 采集当前外部世界的观测快照。Observation 是 evidence，不是 semantic truth（I-4）；
    /// 观测有序性由 Observation.SequenceNumber 表达（确定性、单调递增 — 裁决 6）。
    /// 执行动作后必须重新调用本方法再推进判断，不得信任动作成功记录之外的状态（§3）。
    /// </summary>
    /// <param name="cancellationToken">取消信号；取消时实现应尽快放弃本次观测。</param>
    /// <returns>当前外部世界的 <see cref="Observation"/> 快照。</returns>
    Task<Model.Observation> ObserveAsync(CancellationToken cancellationToken);

    /// <summary>
    /// 将 <see cref="DeviceAction"/> 分发给外部世界执行并返回 dispatch 结果。
    /// 返回的 <see cref="ActionResult"/> 只表达 dispatch outcome（Dispatched / TimedOut / Rejected — 裁决 10），
    /// 任何 dispatch 结果都不直接证明世界状态或 Goal 完成；世界状态只能通过后续 ObserveAsync 重新确认（§3）。
    /// Environment 按元素身份（TargetElementIndex）应用物理效果，不替 Runtime 做元素选择或任务决策（SC-P1-005）。
    /// </summary>
    /// <param name="action">要执行的设备动作（LaunchApp | Tap | SetSwitch）。</param>
    /// <param name="cancellationToken">取消信号；取消时实现应尽快放弃本次 dispatch。</param>
    /// <returns>动作 dispatch 结果。</returns>
    Task<Model.ActionResult> ExecuteAsync(Model.DeviceAction action, CancellationToken cancellationToken);
}
