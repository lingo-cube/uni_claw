using UniClaw.Runtime.Model;
using RuntimeContainer = UniClaw.Runtime.Container.Container;
using RuntimeTraversal = UniClaw.Runtime.Traversal.Traversal;

namespace UniClaw.Runtime.Tests.Scenario;

/// <summary>
/// B9 共享 identity 基建（测试侧）：语义页面解析规则 + container identity 规则 + 容器工厂。
/// 切片 1 显式规则：页面身份由元素内容判别（Phase 5 语义解析算法 DEFER — 裁决 2）；
/// identity 规则 = 解析结果与页面名一致（语义自洽）。
/// 生产 Runtime 不硬编码场景字符串（裁决 11）；本文件是测试注入数据，可含场景字符串。
/// </summary>
public static class ScenarioIdentity
{
    /// <summary>语义页面解析规则（显式规则 — 切片 1）：由元素内容判别当前屏幕（null = Unknown — §10）。</summary>
    /// <param name="observation">观测证据（I-4：evidence，不是 semantic truth）。</param>
    /// <returns>语义页面名；无法判别 = null。</returns>
    public static string? ResolveSemanticPage(Observation observation)
    {
        if (observation.Elements.Length == 0)
            return observation.ForegroundApplication == "Launcher" ? "Launcher" : null;
        if (observation.Elements.Any(e => e.Text == "Network & Internet"))
            return "SettingsMain";
        if (observation.Elements.Length == 1)
            return observation.Elements[0].Text is "WiFi" or "Bluetooth" ? "NetworkSettings" : null;
        if (observation.Elements.Length == 2)
        {
            return observation.Elements[1].SwitchState == true ? "WiFiSettingsOn"
                : observation.Elements[1].SwitchState == false ? "WiFiSettings"
                : null;
        }
        return null;
    }

    /// <summary>container identity 规则：该观测是否仍显示指定语义页面（Container.IsStillMine 判定用）。</summary>
    /// <param name="pageName">语义页面名。</param>
    /// <returns>Observation → bool 的 identity 规则。</returns>
    public static Func<Observation, bool> IdentityRule(string pageName)
        => observation => string.Equals(ResolveSemanticPage(observation), pageName, StringComparison.Ordinal);

    /// <summary>容器工厂：页面名 → 已装配的 Container（identity 规则 + B6 Traversal step executor — Agent 注入用）。</summary>
    /// <param name="traversal">B6 Traversal 实例（step executor 方法组来源）。</param>
    /// <returns>Func&lt;string, Container&gt; 容器工厂。</returns>
    public static Func<string, RuntimeContainer> ContainerFactory(RuntimeTraversal traversal)
        => pageName => new RuntimeContainer(pageName, IdentityRule(pageName), traversal.ExecuteStep);
}
