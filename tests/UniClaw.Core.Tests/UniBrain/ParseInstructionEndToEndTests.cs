using System.Collections.Immutable;
using UniClaw.Core.Observability;
using UniClaw.Core.Simulation;
using UniClaw.Core.UniBrain;
using Xunit;

namespace UniClaw.Core.Tests.UniBrain;

/// <summary>
/// parse_instruction 端到端测试 — task 7.3。
/// 组装真实组件全链：
///   TextUnderstandingRequest → PromptLibrary → TextUnderstanding
///   → ModelRouter(MockModelProvider) → ObservingModelProvider（router 组装期套上）
///   → AICallRecord（观测层记录）。
/// 断言 (a) TextUnderstandingResult 正确；(b) AICallRecord 被观测层记录，
/// Capability==parse_instruction / ProviderId==mock。
/// 对齐 OpenSpec change unibrain-modelprovider-vertical-slice。
/// </summary>
public class ParseInstructionEndToEndTests
{
    [Fact(DisplayName = "端到端: parse_instruction 经全链路返回结果且 AICallRecord 被观测层记录")]
    public async Task ParseInstruction_FullStack_ReturnsResultAndRecordsAICall()
    {
        // 共享 storage：InMemoryTraceRecorder 写入，用其 GetAICalls() 读 API 断言观测层记录
        var storage = new InMemoryTraceStorage();
        var recorder = new InMemoryTraceRecorder(storage);

        var content = """{"category":"open_settings","confidence":0.9,"entities":["设置"],"summary":"打开设置"}""";
        var provider = new MockModelProvider(
            new MockModelFixture(ImmutableDictionary.CreateRange(new[]
            {
                KeyValuePair.Create(ModelCapabilities.ParseInstruction, new MockModelEntry(content)),
            })),
            "mock");

        // ModelRouter 组装期为裸 provider 套 ObservingModelProvider → 调用必产生 AICallRecord
        var router = new ModelRouter(
            ImmutableDictionary.CreateRange(new[]
            {
                KeyValuePair.Create(ModelCapabilities.ParseInstruction, "mock"),
            }),
            ImmutableDictionary.CreateRange<string, IModelProvider>(new[]
            {
                KeyValuePair.Create<string, IModelProvider>("mock", provider),
            }),
            recorder,
            "mock");

        // D-8: 装配期 router.Resolve → IModelProvider（已套 ObservingModelProvider）注入子接口
        var observedProvider = router.Resolve(ModelCapabilities.ParseInstruction);

        var promptLibrary = new PromptLibrary(PromptTemplateRegistry.ParseInstruction);

        var tu = new TextUnderstanding(observedProvider, promptLibrary);

        // (a) TextUnderstandingResult 正确
        var result = await tu.UnderstandTextAsync(new TextUnderstandingRequest("打开设置", "主页"));

        Assert.Equal("open_settings", result.Category);
        Assert.Equal(0.9, result.Confidence);
        Assert.Contains("设置", result.Entities);
        Assert.Equal("打开设置", result.Summary);

        // (b) AICallRecord 被观测层记录（经 router 套的 ObservingModelProvider 产生）
        var record = Assert.Single(storage.GetAICalls());
        Assert.Equal(ModelCapabilities.ParseInstruction, record.Capability);
        Assert.Equal("mock", record.ProviderId);
        Assert.True(record.Success);
    }
}
