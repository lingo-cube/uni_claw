using UniClaw.Core.Observability;
using Xunit;

namespace UniClaw.Core.Tests.Observability;

/// <summary>
/// EngineStepSpanContextTests — 栈式 span 上下文（trace-correlated-logging task 1.5）:
/// Push/Pop 嵌套（栈顶即当前最内层 span）、AsyncLocal flow 隔离、空/null 栈读取与
/// Pop no-op。契约: CurrentSpanId == 栈顶（空栈/未初始化返回 null）；Pop 空/null 栈不抛异常。
/// </summary>
public class EngineStepSpanContextTests
{
    [Fact(DisplayName = "SpanContext: Push/Pop 嵌套 — 栈顶恢复父 span")]
    public void PushPop_Nesting_RestoresParent()
    {
        var context = EngineStepSpanContext.Instance;

        context.Push("A");
        context.Push("B");

        Assert.Equal("B", context.CurrentSpanId);

        context.Pop();
        Assert.Equal("A", context.CurrentSpanId);

        context.Pop();
        Assert.Null(context.CurrentSpanId);
    }

    [Fact(DisplayName = "SpanContext: AsyncLocal flow 隔离 — 外层不受内层 push 污染")]
    public async Task AsyncLocalFlow_InnerFlow_DoesNotPolluteOuter()
    {
        var context = EngineStepSpanContext.Instance;

        // PRD §7: AsyncLocal 按 async flow 流动（Task.Run 内读可见父 flow 值），
        // 每个 flow 拥有独立栈 —— 内层 Push/Pop 不得污染外层读取。
        context.Push("A");
        try
        {
            string? innerInitial = null;
            string? innerFinal = null;
            await Task.Run(() =>
            {
                innerInitial = context.CurrentSpanId;   // flow 流入: 应为 "A"
                context.Push("B");
                innerFinal = context.CurrentSpanId;     // 内层栈顶: 应为 "B"
            });

            Assert.Equal("A", innerInitial);
            Assert.Equal("B", innerFinal);
            Assert.Equal("A", context.CurrentSpanId);   // 外层不受内层污染: 应仍为 "A"
        }
        finally
        {
            context.Pop();
        }
    }

    [Fact(DisplayName = "SpanContext: 新 flow 空栈返回 null")]
    public void FreshFlow_CurrentSpanIdIsNull()
    {
        // 每个测试运行在独立的 async flow 上；本测试从未 Push → 栈未初始化 → null。
        Assert.Null(EngineStepSpanContext.Instance.CurrentSpanId);
    }

    [Fact(DisplayName = "SpanContext: 空栈 Pop 为 no-op（栈实例存在但已空）")]
    public void Pop_OnEmptyStack_IsNoOp()
    {
        var context = EngineStepSpanContext.Instance;

        context.Push("A");
        context.Pop();
        // 栈实例仍存在但已空 → 再次 Pop 不抛异常，CurrentSpanId 仍为 null。
        context.Pop();

        Assert.Null(context.CurrentSpanId);
    }

    [Fact(DisplayName = "SpanContext: 未初始化（null）栈 Pop 为 no-op")]
    public void Pop_OnNullStack_IsNoOp()
    {
        var context = EngineStepSpanContext.Instance;

        // 从未 Push → _stack.Value 为 null → Pop 不抛异常。
        context.Pop();

        Assert.Null(context.CurrentSpanId);
    }
}
