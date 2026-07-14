using UniClaw.Core.Domain.Models.Content;

namespace UniClaw.Core.Simulation.Scroll;

/// <summary>
/// 模拟内容项 (mock-only, sealed record, 见设计 §15.3): 由 <see cref="IScrollContentSource"/>
/// 按页生成的最小元素描述。<see cref="SimulatedScreen"/> 把它映射为 <see cref="MenuItem"/>。
/// </summary>
/// <param name="Name">元素 id / 显示文本 (须在页内唯一)</param>
/// <param name="X">归一化 X 坐标</param>
/// <param name="Y">归一化 Y 坐标</param>
/// <param name="Type">元素类型 (默认 Button, 与滚动基线场景一致)</param>
public sealed record class MockItem(
    string Name,
    double X,
    double Y,
    MenuItemType Type = MenuItemType.Button);
