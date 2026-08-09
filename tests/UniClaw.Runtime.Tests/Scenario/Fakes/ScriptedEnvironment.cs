using System.Collections.Immutable;
using UniClaw.Runtime.Environment;
using UniClaw.Runtime.Model;

namespace UniClaw.Runtime.Tests.Scenario.Fakes;

/// <summary>触发转场的动作类型（Environment 侧配置数据；不是任务决策）。</summary>
public enum ScreenTransitionAction
{
    /// <summary>Tap 动作触发转场。</summary>
    Tap = 0,

    /// <summary>SetSwitch 动作（匹配 TargetState）触发转场。</summary>
    SetSwitch = 1,
}

/// <summary>
/// 元素上的转场配置：「某类动作作用于该元素 → 世界切换到 NextScreenName」（Screen A + Click X → Screen B — §33）。
/// Environment 按元素身份应用物理效果，不替 Runtime 做元素选择（SC-P1-005）。
/// </summary>
/// <param name="Action">触发转场的动作类型。</param>
/// <param name="NextScreenName">转场目标屏幕名。</param>
/// <param name="TargetState">SetSwitch 转场的期望目标状态（仅 Action=SetSwitch 时使用；Tap 转场为 null）。</param>
/// <param name="DispatchOutcome">测试侧配置的 dispatch outcome；默认 Dispatched 保持既有变体行为。
/// TimedOut 仅改变 transport 结果，不阻止已配置的世界转场（SC-P3-001 Task 1.1）。</param>
public sealed record TransitionConfig(
    ScreenTransitionAction Action,
    string NextScreenName,
    bool? TargetState = null,
    ActionResultOutcome DispatchOutcome = ActionResultOutcome.Dispatched);

/// <summary>屏幕内单个元素的配置：Text + SwitchState? + 转场。</summary>
/// <param name="Text">元素文本。</param>
/// <param name="SwitchState">开关状态；null = 非开关承载元素（SetSwitch 作用于它 → Rejected — SC-P1-005）。</param>
/// <param name="Transition">作用于该元素的动作转场；null = 动作无世界效果（dispatch 成功但世界不变）。</param>
public sealed record ElementConfig(
    string Text,
    bool? SwitchState,
    TransitionConfig? Transition);

/// <summary>
/// targetless viewport action 的 Fake 世界转场配置（SC-P3-003 Task 1.1）。
/// dispatch outcome 与世界转场独立：配置的目标屏幕先应用，再报告 outcome。
/// </summary>
public sealed record ViewportTransitionConfig(
    string NextScreenName,
    ActionResultOutcome DispatchOutcome = ActionResultOutcome.Dispatched);

/// <summary>单个屏幕的配置：名字 / 前台应用 / 元素列表（元素 Index = 列表内序位，观测间稳定 — 裁决 3）。</summary>
/// <param name="Name">屏幕名（转场目标引用）。</param>
/// <param name="ForegroundApplication">该屏幕可见时的前台应用；null = 未知。</param>
/// <param name="Elements">元素配置；Index 按列表顺序分配（0-based 稳定序位，非坐标）。</param>
/// <param name="ViewportTransition">一次 bounded forward viewport action 的可选世界转场。</param>
public sealed record ScreenConfig(
    string Name,
    string? ForegroundApplication,
    ImmutableArray<ElementConfig> Elements,
    ViewportTransitionConfig? ViewportTransition = null);

/// <summary>
/// Screen 配置驱动的确定性 IEnvironment 实现（宪章 §33 Fake；B3，IEnvironment 端口 — B2 — 的第一个实现者）。
/// 可变状态 owner：当前屏幕 / 观测序号 / action history 均为本 fake 独占（I-2 — 测试侧状态）。
/// 同一动作序列必然产生同一观察序列（确定性、可重放 — specs/environment SHALL）。
/// dispatch outcome ≠ world success：物理卡住（switch-stuck 变体）时 SetSwitch 仍返回 Dispatched 但世界不变（裁决 10）。
/// 指定观测序号的外部屏幕转场只属于 Fake World 数据，用于确定性表达无 Runtime 动作触发的外部事件
/// （SC-P3-002 Task 1.1 Popup appearance）；它不实现任何生产检测或恢复行为。
/// </summary>
public sealed class ScriptedEnvironment : IEnvironment
{
    private readonly ImmutableDictionary<string, ScreenConfig> _screens;
    private readonly string? _launchNextScreenName;
    private readonly Dictionary<long, (string Foreground, ImmutableArray<ObservedElement> Elements)>? _observeOverrides;
    private readonly Dictionary<long, string>? _observeScreenTransitions;
    private readonly Dictionary<long, long>? _observeSequenceOverrides;
    private readonly List<DeviceAction> _actionHistory = [];
    private readonly List<Observation> _observationHistory = [];
    private string _currentScreenName;
    private long _sequenceNumber;

    /// <summary>构造 ScriptedEnvironment。</summary>
    /// <param name="initialScreenName">初始屏幕名（LaunchApp 之前的当前屏幕）。</param>
    /// <param name="launchNextScreenName">LaunchApp 后的目标屏幕名；null = LaunchApp 不改变屏幕（如 startup-fg-fail：前台仍为 Launcher）。</param>
    /// <param name="screens">全部屏幕配置（按 Name 唯一）。</param>
    /// <param name="observeOverrides">一次性观测掩码：key = 观测序号，value = 该次观测替换的前台 + 元素
    /// （C1 launcher-drift 注入 — 仅替换当次观测；不改变当前屏幕、不记录进 ActionHistory；mask 消费后移除，默认 null = 原行为）。</param>
    /// <param name="observeScreenTransitions">确定性外部世界事件：key = 观测序号，value = 该次 Observe 前切换到的屏幕名；
    /// 不记录 ActionHistory，默认 null 保持既有行为（SC-P3-002 Task 1.1）。</param>
    /// <param name="observeSequenceOverrides">测试侧 stale-evidence 注入：key = Observe 调用的内部序号，
    /// value = 返回 Observation 的序号；默认 null 保持严格单调（SC-P3-003 Task 1.1）。</param>
    public ScriptedEnvironment(
        string initialScreenName,
        string? launchNextScreenName,
        IEnumerable<ScreenConfig> screens,
        IReadOnlyDictionary<long, (string Foreground, ImmutableArray<ObservedElement> Elements)>? observeOverrides = null,
        IReadOnlyDictionary<long, string>? observeScreenTransitions = null,
        IReadOnlyDictionary<long, long>? observeSequenceOverrides = null)
    {
        _screens = screens.ToImmutableDictionary(s => s.Name, StringComparer.Ordinal);
        _currentScreenName = initialScreenName;
        _launchNextScreenName = launchNextScreenName;
        _observeOverrides = observeOverrides is null ? null : new Dictionary<long, (string Foreground, ImmutableArray<ObservedElement> Elements)>(observeOverrides);
        _observeScreenTransitions = observeScreenTransitions is null ? null : new Dictionary<long, string>(observeScreenTransitions);
        _observeSequenceOverrides = observeSequenceOverrides is null ? null : new Dictionary<long, long>(observeSequenceOverrides);
    }

    /// <summary>已执行动作的追加式历史（含 Rejected），按执行顺序（SC-P1-002 断言 5 / SC-P1-004 断言 3 的观察面）。</summary>
    public IReadOnlyList<DeviceAction> ActionHistory => _actionHistory;

    /// <summary>已返回 Observation 的追加式历史；只用于确定性 Scenario replay 证明。</summary>
    public IReadOnlyList<Observation> ObservationHistory => _observationHistory;

    /// <summary>采集当前屏幕的观测快照；SequenceNumber 单调递增（1..N，确定性 — 裁决 6）。
    /// 命中观测掩码时（C1）：该次观测替换为 mask 的前台 + 元素（其余机制不变 — 序号照常推进）。</summary>
    public Task<Observation> ObserveAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var sequence = ++_sequenceNumber;
        if (_observeScreenTransitions is { } transitions && transitions.Remove(sequence, out var nextScreen))
        {
            if (!_screens.ContainsKey(nextScreen))
                throw new InvalidOperationException($"观测序号 {sequence} 的外部转场目标屏幕不存在：{nextScreen}");
            _currentScreenName = nextScreen;
        }
        Observation observation;
        if (_observeOverrides is { } overrides && overrides.Remove(sequence, out var mask))
        {
            // 一次性掩码：仅替换本次观测（drift 注入）；不改变当前屏幕、不记录动作
            observation = new Observation(mask.Elements, mask.Foreground, sequence);
        }
        else
        {
            var screen = _screens[_currentScreenName];
            var elements = screen.Elements
                .Select((element, index) => new ObservedElement(element.Text, element.SwitchState, index))
                .ToImmutableArray();
            observation = new Observation(elements, screen.ForegroundApplication, sequence);
        }
        if (_observeSequenceOverrides is { } sequenceOverrides
            && sequenceOverrides.Remove(sequence, out var returnedSequence))
        {
            observation = observation with { SequenceNumber = returnedSequence };
        }
        _observationHistory.Add(observation);
        return Task.FromResult(observation);
    }

    /// <summary>按元素身份（TargetElementIndex）应用动作的物理效果并记录 action history（含 Rejected）。
    /// 匹配的转场先更新测试世界，再返回其独立配置的 dispatch outcome（SC-P3-001 Task 1.1）。</summary>
    public Task<ActionResult> ExecuteAsync(DeviceAction action, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _actionHistory.Add(action);
        return Task.FromResult(action switch
        {
            DeviceAction.LaunchApp launch => Launch(launch),
            DeviceAction.Tap { TargetElementIndex: { } index } tap => Tap(tap, index),
            DeviceAction.SetSwitch { TargetElementIndex: { } index } setSwitch => SetSwitch(setSwitch, index),
            DeviceAction.ScrollForward scroll => ScrollForward(scroll),
            _ => new ActionResult(
                ActionResultOutcome.Rejected, Describe(action), "动作缺少 TargetElementIndex（未指定目标元素）。"),
        });
    }

    private ActionResult Launch(DeviceAction.LaunchApp launch)
    {
        if (_launchNextScreenName is { } next && _screens.ContainsKey(next))
            _currentScreenName = next;
        return new ActionResult(ActionResultOutcome.Dispatched, Describe(launch), "launch dispatched");
    }

    private ActionResult Tap(DeviceAction.Tap tap, int targetElementIndex)
    {
        var element = ElementAt(targetElementIndex);
        if (element is null)
            return new ActionResult(
                ActionResultOutcome.Rejected, Describe(tap), $"元素索引 {targetElementIndex} 超出当前屏幕元素范围。");
        if (element.Transition is { Action: ScreenTransitionAction.Tap } transition)
        {
            if (_screens.ContainsKey(transition.NextScreenName))
                _currentScreenName = transition.NextScreenName;
            return new ActionResult(transition.DispatchOutcome, Describe(tap), DescribeDispatch("tap", transition.DispatchOutcome));
        }
        return new ActionResult(ActionResultOutcome.Dispatched, Describe(tap), "tap dispatched");
    }

    private ActionResult SetSwitch(DeviceAction.SetSwitch setSwitch, int targetElementIndex)
    {
        var element = ElementAt(targetElementIndex);
        if (element is null)
            return new ActionResult(
                ActionResultOutcome.Rejected, Describe(setSwitch), $"元素索引 {targetElementIndex} 超出当前屏幕元素范围。");
        if (element.SwitchState is null)
            return new ActionResult(
                ActionResultOutcome.Rejected, Describe(setSwitch),
                "SetSwitch 作用于非开关承载元素（SwitchState=null）— 物理能力语义（SC-P1-005 错误路径，非任务决策）。");
        if (element.Transition is { Action: ScreenTransitionAction.SetSwitch } transition
            && transition.TargetState == setSwitch.TargetState)
        {
            if (_screens.ContainsKey(transition.NextScreenName))
                _currentScreenName = transition.NextScreenName;
            return new ActionResult(
                transition.DispatchOutcome,
                Describe(setSwitch),
                DescribeDispatch("set-switch", transition.DispatchOutcome));
        }
        return new ActionResult(ActionResultOutcome.Dispatched, Describe(setSwitch), "set-switch dispatched");
    }

    private ActionResult ScrollForward(DeviceAction.ScrollForward scroll)
    {
        var transition = _screens[_currentScreenName].ViewportTransition;
        if (transition is null)
        {
            return new ActionResult(
                ActionResultOutcome.Rejected,
                Describe(scroll),
                "当前 Fake screen 未配置 bounded forward viewport transition。");
        }
        if (_screens.ContainsKey(transition.NextScreenName))
            _currentScreenName = transition.NextScreenName;
        return new ActionResult(
            transition.DispatchOutcome,
            Describe(scroll),
            DescribeDispatch("scroll-forward", transition.DispatchOutcome));
    }

    private ElementConfig? ElementAt(int index)
    {
        var elements = _screens[_currentScreenName].Elements;
        if (index < 0 || index >= elements.Length)
            return null;
        return elements[index];
    }

    private static string Describe(DeviceAction action) => action switch
    {
        DeviceAction.LaunchApp launch => $"LaunchApp({launch.ApplicationId ?? "<unspecified>"})",
        DeviceAction.Tap tap => $"Tap({tap.TargetElementIndex?.ToString() ?? "<unspecified>"})",
        DeviceAction.SetSwitch setSwitch =>
            $"SetSwitch({setSwitch.TargetElementIndex?.ToString() ?? "<unspecified>"}, {setSwitch.TargetState})",
        DeviceAction.ScrollForward => "ScrollForward",
        _ => action.GetType().Name,
    };

    private static string DescribeDispatch(string action, ActionResultOutcome outcome)
        => outcome == ActionResultOutcome.TimedOut
            ? $"{action} timed out after dispatch attempt"
            : $"{action} dispatched";
}
