using System.Reflection;
using System.Threading.Tasks;
using UniClaw.Core.Traversal;
using Xunit;

namespace UniClaw.Core.Tests.Traversal;

/// <summary>
/// M1 接口契约测试 —— 解决 OpenSpec change host-target-architecture 冲突 C1。
/// 锁定 IObservableScreenStateProvider 的接口形状：
///   1. 本身是 interface。
///   2. 继承锁定的 IScreenStateProvider（子接口，非平级替代）。
///   3. 仅追加一个新方法 RefreshAsync，返回 Core-lifted ScreenStateResult。
///   4. IScreenStateProvider 4 方法锁不变（回归守卫，与 ArchitectureGuardTests
///      UniBrainGuardTests.IScreenStateProvider_Has4Methods 同源断言，但这里独立再断一次，
///      确保子接口化未误改父接口）。
/// 反射按 DeclaringType 过滤，子接口的新方法不计入父接口锁。
/// </summary>
public sealed class IObservableScreenStateProviderContractTests
{
    [Fact(DisplayName = "C1: IObservableScreenStateProvider 是 interface")]
    public void IsInterface_True()
        => Assert.True(typeof(IObservableScreenStateProvider).IsInterface);

    [Fact(DisplayName = "C1: IObservableScreenStateProvider 继承锁定的 IScreenStateProvider")]
    public void InheritsLockedInterface()
        => Assert.True(
            typeof(IScreenStateProvider).IsAssignableFrom(typeof(IObservableScreenStateProvider)));

    [Fact(DisplayName = "C1: IObservableScreenStateProvider 仅追加一个新方法 RefreshAsync 返回 ScreenStateResult")]
    public void DeclaresExactlyOneNewMethod_RefreshAsync_ReturningScreenStateResult()
    {
        var newMethods = typeof(IObservableScreenStateProvider)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .ToList();

        Assert.Single(newMethods);

        var refresh = newMethods[0];
        Assert.Equal("RefreshAsync", refresh.Name);
        // RefreshAsync is async → reflection ReturnType is the constructed Task<>.
        // Assert the awaitable's result-argument is ScreenStateResult (the Core-lifted type).
        Assert.Equal(typeof(Task<>), refresh.ReturnType.GetGenericTypeDefinition());
        Assert.Equal(
            typeof(ScreenStateResult),
            refresh.ReturnType.GetGenericArguments()[0]);
    }

    [Fact(DisplayName = "C1: IScreenStateProvider 仍锁定 4 方法 (子接口化回归守卫)")]
    public void ScreenStateProvider_StillHas4LockedMethods()
    {
        var methods = typeof(IScreenStateProvider).GetMethods()
            .Where(m => m.DeclaringType == typeof(IScreenStateProvider))
            .ToList();

        Assert.Equal(4, methods.Count);

        var names = methods.Select(m => m.Name).OrderBy(n => n).ToList();
        Assert.Equal(
            new[] { "GetScrollProgress", "GetScrollSwipeConfig", "HasScroll", "IsEndOfList" },
            names);
    }
}