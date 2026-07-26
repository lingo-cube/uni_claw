using System.Collections.Immutable;
using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Observability;
using UniClaw.Core.Simulation;
using UniClaw.Core.UniBrain;
using Xunit;

namespace UniClaw.Core.Tests.UniBrain;

/// <summary>
/// decide_next_action 端到端测试 — task 4.3。
/// 组装真实组件全链：
///   decide_next_action.mock.json (磁盘 fixture) → MockModelFixture.FromJson
///   → MockModelProvider → ModelRouter（组装期套 ObservingModelProvider）
///   → PromptLibrary(decide_next_action 模板) → TraversalAdvisor
///   → 解析 JSON 响应为 ContextDecisionResult + AICallRecord 被观测层记录。
/// 对齐 OpenSpec change unibrain-traversaladvisor-vertical-slice 的 "Happy path decides next action" 场景。
/// </summary>
public sealed class DecideNextActionEndToEndTests
{
    [Fact(DisplayName = "端到端: decide_next_action 经全链路返回 ContextDecisionResult 且 AICallRecord 被观测层记录")]
    public async Task DecideNextAction_FullStack_ReturnsResultAndRecordsAICall()
    {
        // 共享 storage：InMemoryTraceRecorder 写入，用其 GetAICalls() 断言观测层记录
        var storage = new InMemoryTraceStorage();
        var recorder = new InMemoryTraceRecorder(storage);

        // 从磁盘 fixture 加载预设响应表（csproj 已拷贝 Fixtures/**/*.json 到输出目录）
        var fixturePath = Path.Combine(
            AppContext.BaseDirectory, "Fixtures", "decide_next_action.mock.json");
        var fixture = MockModelFixture.FromJson(File.ReadAllText(fixturePath));

        var provider = new MockModelProvider(fixture, "mock");

        // ModelRouter 组装期为裸 provider 套 ObservingModelProvider → 调用必产生 AICallRecord
        var router = new ModelRouter(
            ImmutableDictionary.CreateRange(new[]
            {
                KeyValuePair.Create(ModelCapabilities.DecideNextAction, "mock"),
            }),
            ImmutableDictionary.CreateRange<string, IModelProvider>(new[]
            {
                KeyValuePair.Create<string, IModelProvider>("mock", provider),
            }),
            recorder,
            "mock");

        // D-8: 装配期 router.Resolve → IModelProvider（已套 ObservingModelProvider）注入子接口
        var observedProvider = router.Resolve(ModelCapabilities.DecideNextAction);

        // 注册 decide_next_action prompt 模板（引自 PromptTemplateRegistry 单点真源）
        var promptLibrary = new PromptLibrary(PromptTemplateRegistry.DecideNextAction);

        var advisor = new TraversalAdvisor(observedProvider, promptLibrary);

        // 最小合法 PageAnalysis（仅需 Level1Dir + Level2Dir）
        var pageAnalysis = new PageAnalysis(Direction.Top, Direction.Bottom);

        // Act: 对齐 spec "Happy path decides next action" 场景
        var result = await advisor.DecideNextActionAsync("find WiFi settings", pageAnalysis, "node_1", 3);

        // Assert: ContextDecisionResult 全字段
        Assert.Equal(DecisionResult.Success, result.Result);
        Assert.Equal("tap", result.Action);
        Assert.Equal("wifi_item", result.Target);
        Assert.NotNull(result.Params);
        var timeout = Assert.IsType<double>(result.Params!["timeout"]);
        Assert.Equal(5000.0, timeout);
        Assert.Equal(0.9, result.Confidence, precision: 5);
        Assert.True(result.SafetyVerified);
        Assert.False(string.IsNullOrEmpty(result.Reasoning));

        // Assert: AICallRecord 被观测层记录（经 router 套的 ObservingModelProvider 产生）
        var record = Assert.Single(storage.GetAICalls());
        Assert.Equal(ModelCapabilities.DecideNextAction, record.Capability);
        Assert.Equal("mock", record.ProviderId);
        Assert.True(record.Success);
    }
}
