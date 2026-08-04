using System.Text.RegularExpressions;
using UniClaw.Core.Traversal;
using UniClaw.Host.Commands;
using Xunit;

namespace UniClaw.Host.Tests.Commands;

/// <summary>
/// host-target-architecture (8.26 / 8.27) 架构 guard —— Host 组合根防 bypass 回归。
///
/// 8.26 装配 guard：Host 不得绕过 IUniBrain 直接 new 真实 provider/analyzer。
///   - <c>new PageAnalyzer(</c> 在 Host 源码中完全禁止 —— PageAnalyzer 只能由
///     Core 的 UniBrainFactory 内部创建（UniBrainFactory.cs:119）。
///   - 真实 IModelProvider 构造只允许作为 UniBrainFactory.Create 的装配输入，
///     即仅可出现在 CreateProviders 方法体内（经 CreateUniBrain →
///     UniBrainFactory.Create 装配）。MockModelProvider 白名单豁免（mock 是 Core
///     的 replay provider，非真实 provider）。
///   - 已登记例外（acknowledged）：CreateIntentExtractor 内为 IIntentExtractor
///     （独立于 IUniBrain 的既有能力，931e385 之前即存在）构造 vision provider
///     属于该组件的既有设计，非 8.26 治理范围 —— 与 DependencyDirectionGuardTests
///     的 acknowledged-upward-reference 先例同型。是否纳入治理需顶层裁决。
///
/// 8.27 单 decorated executor guard：HostRunServices 恰好 1 个 IActionExecutor 属性
/// （ActionExecutor）；IActionExecutor 实例只经 SafeActionExecutor 装饰链产生
/// （Safe → PageInvalidating → Adb，源码顺序锁死），裸执行器不得传入组合根。
///
/// 与任务字面规则的两处偏差（以实际代码为准，防误报）：
///   - 8.26 字面规则「凡非 Mock 的 new XxxModelProvider( 即失败」会误报 CreateProviders
///     内 DeepSeekModelProvider/AnthropicModelProvider/OpenAiCompatibleVisionProvider/
///     LocalVisionProvider 构造 —— 这些是 UniBrainFactory.Create 的合法装配输入
///     （HostCommands.cs:1403-1414），不构成绕过。故改为「构造位置限定 CreateProviders 内」。
///   - 8.27 字面规则「IActionExecutor 实例只通过 new SafeActionExecutor( 产生」与装饰链
///     现实冲突 —— AdbActionExecutor / PageInvalidatingActionExecutor 是 SafeActionExecutor
///     的装饰内层构造。故改为「Safe 唯一出口 + 全链构造各恰好 1 次 + 源码顺序断言」。
/// </summary>
public sealed class HostArchitectureGuardTests
{
    // ===== 8.26 装配 guard：Host 不得绕过 IUniBrain 直接 new 真实 provider/analyzer =====

    /// <summary>
    /// PageAnalyzer 只能由 Core 的 UniBrainFactory 创建（UniBrainFactory.cs:119）；
    /// Host 必须经 IUniBrain.PageAnalyzer 消费分析能力，不得手写 new PageAnalyzer。
    /// </summary>
    [Fact]
    public void Host_DoesNotConstructPageAnalyzerDirectly()
    {
        var hostDir = Path.Combine(FindSourceRoot(), "src", "UniClaw.Host");
        Assert.True(Directory.Exists(hostDir), $"UniClaw.Host source dir not found at {hostDir}");

        foreach (var file in Directory.GetFiles(hostDir, "*.cs", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(file);
            Assert.DoesNotContain(
                "new PageAnalyzer(",
                source);
        }
    }

    /// <summary>
    /// 真实 IModelProvider 构造只允许存在于 CreateProviders 方法体内 —— 即仅作为
    /// UniBrainFactory.Create 的装配输入（CreateUniBrain → UniBrainFactory.Create）。
    /// MockModelProvider 白名单豁免（mock 是 Core 的 replay provider，非真实 provider）。
    /// 在 CreateProviders 之外的任何 `new XxxProvider(` 构造（含在其它方法直接 new
    /// 真实 provider 并使用）都会使本 guard 失败。
    /// </summary>
    [Fact]
    public void Host_ConstructsRealModelProvidersOnlyInsideCreateProviders()
    {
        var hostDir = Path.Combine(FindSourceRoot(), "src", "UniClaw.Host");
        Assert.True(Directory.Exists(hostDir), $"UniClaw.Host source dir not found at {hostDir}");

        foreach (var file in Directory.GetFiles(hostDir, "*.cs", SearchOption.AllDirectories))
        {
            var source = File.ReadAllText(file);

            // 匹配真实模型 provider 构造：`new XxxModelProvider(` 与 `new XxxVisionProvider(`
            // （OpenAiCompatibleVisionProvider / LocalVisionProvider 等视觉 provider 是
            // IModelProvider 实现）。命名不以 ModelProvider/VisionProvider 结尾的非模型
            // Provider 类（PhysicalFileProvider、*ScreenStateProvider 等）天然排除。
            // 新增真实 IModelProvider 实现若采用其它命名约定，须同步本正则。
            // 白名单：MockModelProvider（Core 的 replay provider，不限位置）。
            var realProviders = Regex.Matches(source, @"new [A-Za-z.]+(ModelProvider|VisionProvider)\(")
                .Cast<Match>()
                .Where(m => !m.Value.StartsWith("new MockModelProvider(", StringComparison.Ordinal))
                .ToList();
            if (realProviders.Count == 0)
                continue;

            // 允许的构造区间（锚点均唯一，见各 helper 注释）：
            //   1. CreateProviders 方法体 —— UniBrainFactory.Create 的装配输入
            //      （CreateUniBrain → UniBrainFactory.Create），8.26 的正当路径。
            //   2. CreateIntentExtractor 方法体 —— 已登记例外（acknowledged，同
            //      DependencyDirectionGuardTests 先例）：IIntentExtractor 是独立于
            //      IUniBrain 的既有能力（931e385 之前即存在，非 host-target-architecture
            //      引入），其构造真实 vision provider 属于该组件的既有设计。
            var allowedRanges = new List<(int Start, int End)>
            {
                FindMethodBodyRange(source, "IModelProvider> CreateProviders("),
                FindMethodBodyRange(source, "IIntentExtractor? CreateIntentExtractor("),
            };
            Assert.True(allowedRanges.All(r => r.Start >= 0),
                $"{Path.GetFileName(file)} contains real provider constructions "
                + $"({string.Join(", ", realProviders.Select(m => m.Value))}) but the "
                + "allowed-assembly method(s) are missing — Host must not bypass "
                + "IUniBrain / IIntentExtractor with a directly constructed provider.");

            foreach (var m in realProviders)
            {
                Assert.True(
                    allowedRanges.Any(r => m.Index >= r.Start && m.Index <= r.End),
                    $"Real provider construction '{m.Value}' at char {m.Index} in "
                    + $"{Path.GetFileName(file)} is outside the allowed assembly methods "
                    + "(CreateProviders / CreateIntentExtractor) — providers may only be "
                    + "assembled as UniBrainFactory.Create / IIntentExtractor input (8.26).");
            }
        }
    }

    /// <summary>
    /// 返回 anchorText（方法声明签名）第一次出现位置到方法体闭合大括号的区间。
    /// anchorText 必须为声明签名而非调用点（含返回类型前缀，调用点不含），保证唯一。
    /// 返回 (-1, -1) 表示锚点缺失。
    /// </summary>
    private static (int Start, int End) FindMethodBodyRange(string source, string anchorText)
    {
        var declStart = source.IndexOf(anchorText, StringComparison.Ordinal);
        if (declStart < 0)
            return (-1, -1);

        var bodyStart = source.IndexOf('{', declStart);
        if (bodyStart < declStart)
            return (-1, -1);

        var depth = 0;
        for (var i = bodyStart; i < source.Length; i++)
        {
            if (source[i] == '{')
                depth++;
            else if (source[i] == '}')
            {
                depth--;
                if (depth == 0)
                    return (declStart, i);
            }
        }
        return (-1, -1);
    }

    // ===== 8.27 单 decorated executor guard =====

    /// <summary>
    /// 8.27a 反射断言：HostRunServices 恰好 1 个 IActionExecutor 属性（ActionExecutor）。
    /// 防止第二个未装饰 IActionExecutor 属性加入组合根 record。
    /// </summary>
    [Fact]
    public void HostRunServices_HasExactlyOneActionExecutorProperty()
    {
        var actionExecutorProps = typeof(HostRunServices).GetProperties()
            .Where(p => p.PropertyType == typeof(IActionExecutor))
            .ToList();

        Assert.Single(actionExecutorProps);
        Assert.Equal("ActionExecutor", actionExecutorProps[0].Name);
    }

    /// <summary>
    /// 8.27b 源码断言：IActionExecutor 实例只经 SafeActionExecutor 装饰链产生。
    ///   - 组合根中 `new HostRunServices(` 恰好 1 次（单一装配点）；
    ///   - Safe / PageInvalidating / Adb 三个构造各恰好 1 次（全链唯一）；
    ///   - 源码顺序 Safe &lt; PageInvalidating &lt; Adb —— AdbActionExecutor 构造位于
    ///     SafeActionExecutor 调用参数区内，不可能作为裸 IActionExecutor 传入
    ///     HostRunServices（组合根传入的是装饰后实例 safeActions，HostCommands.cs:537-541）。
    /// </summary>
    [Fact]
    public void Host_ActionExecutor_OnlyViaSafeDecoratorChain()
    {
        var sourcePath = Path.Combine(
            FindSourceRoot(), "src", "UniClaw.Host", "Commands", "HostCommands.cs");
        Assert.True(File.Exists(sourcePath), $"HostCommands.cs not found at {sourcePath}");
        var source = File.ReadAllText(sourcePath);

        // 全链构造唯一性
        Assert.Equal(1, CountOccurrences(source, "new SafeActionExecutor("));
        Assert.Equal(1, CountOccurrences(source, "new PageInvalidatingActionExecutor("));
        Assert.Equal(1, CountOccurrences(source, "new AdbActionExecutor("));

        // 装饰链源码顺序（计数为 1 保证 IndexOf >= 0）
        var safeIdx = source.IndexOf("new SafeActionExecutor(", StringComparison.Ordinal);
        var invalidatingIdx = source.IndexOf(
            "new PageInvalidatingActionExecutor(", StringComparison.Ordinal);
        var adbIdx = source.IndexOf("new AdbActionExecutor(", StringComparison.Ordinal);
        Assert.True(
            invalidatingIdx > safeIdx && adbIdx > invalidatingIdx,
            "IActionExecutor 装饰链顺序必须为 "
            + "SafeActionExecutor(PageInvalidatingActionExecutor(AdbActionExecutor)) —— "
            + "裸执行器不得绕过 SafeActionExecutor 直接进入组合根 (8.27)。");

        // 组合根单一装配点
        Assert.Equal(1, CountOccurrences(source, "new HostRunServices("));
    }

    private static int CountOccurrences(string source, string needle)
    {
        var count = 0;
        var idx = 0;
        while ((idx = source.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += needle.Length;
        }
        return count;
    }

    private static string FindSourceRoot()
    {
        // 从测试 bin 目录向上找到仓库根（含 src/UniClaw.Core.sln）
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "src", "UniClaw.Core.sln")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new InvalidOperationException(
            "Cannot find source root (src/UniClaw.Core.sln) from test bin directory.");
    }
}
