namespace UniClaw.Simulation.Tests;

/// <summary>库场景解析失败（fail-closed；不得静默降级）。</summary>
internal sealed class ScenarioLibraryException(string message, Exception? inner = null)
    : Exception(message, inner);

/// <summary>注册场景的 executable 投影产物：bundle + RunOptions + 实际 carrier。</summary>
internal sealed record LibraryScenario(
    string ScenarioId,
    string Carrier,
    MinimalScenarioBundle Bundle,
    RunOptions Options);

/// <summary>
/// SIM-003 G6：SCN 条目 → executable carrier 的唯一解析器。
///
/// 数据流（certified JSON 驱动，期望投影永远是 bundle 构造的最后一步）：
///   Load scenario → 读取 execution → 选择 carrier → 应用 options
///     → carrier/builder 变换 → Expected = certified projection → Seal
///
/// 注册表只声明 carrier 键 → 工厂（工厂接受期望来源 scenarioId）。
/// 本类型不拥有第二份期望——所有期望值经 ScenarioExpectations 投影。
/// </summary>
internal static class ScenarioLibrary
{
    /// <summary>carrier 注册键 → bundle 工厂（参数 = 期望来源 scenarioId）。</summary>
    private static readonly IReadOnlyDictionary<string, Func<string, MinimalScenarioBundle>> Carriers =
        new Dictionary<string, Func<string, MinimalScenarioBundle>>(StringComparer.Ordinal)
        {
            ["wifi-off-to-on"] = id => GoldenScenarioBundles.WifiToggleOffToOn(id),
            ["wifi-already-on"] = id => GoldenScenarioBundles.AlreadyOnZeroEffect(id),
            ["wifi-missing-post-action"] = id => GoldenScenarioBundles.MissingPostActionStimulus(id),
            ["wifi-cancel-late"] = id => GoldenScenarioBundles.CancelThenLateStimulus(id),
            ["wifi-two-step-toggle-menu"] = id => GoldenScenarioBundles.TwoStepToggleThenMenuItem(id),
            ["wifi-two-step-missing-middle"] = id => GoldenScenarioBundles.TwoStepMissingMiddleEvidence(id),
            ["wifi-two-step-contradictory-post"] = id => GoldenScenarioBundles.TwoStepContradictoryPost(id),
            ["wifi-off-to-on-generated"] = id => GoldenScenarioBundles.WifiToggleOffToOnGenerated(id),
        };

    /// <summary>
    /// 解析注册场景：certified JSON（execution 绑定 + 期望投影）→ bundle + RunOptions。
    /// kind=none（如 PERC 自有 harness）显式拒绝——executable expectation
    /// binding 保证只覆盖 kind=golden-bundle。
    /// </summary>
    public static LibraryScenario Load(string scenarioId, string? scenariosDirectory = null)
    {
        var verified = ScenarioExpectations.Verify(scenarioId, scenariosDirectory);
        var execution = verified.Execution;

        if (execution.Kind != "golden-bundle")
            throw new ScenarioLibraryException(
                $"{scenarioId}: execution.kind={execution.Kind} 无 bundle carrier（kind=none 场景不在 executable expectation projection 保证内）");
        if (string.IsNullOrEmpty(execution.Carrier))
            throw new ScenarioLibraryException($"{scenarioId}: execution.kind=golden-bundle 缺 carrier");
        if (!Carriers.TryGetValue(execution.Carrier!, out var factory))
            throw new ScenarioLibraryException(
                $"{scenarioId}: execution.carrier '{execution.Carrier}' 未注册（GoldenScenarioBundles 无此工厂键）");
        if (!ScenarioCertification.TryRunOptions(execution, out var options))
            throw new ScenarioLibraryException(
                $"{scenarioId}: execution.options 含未知 key（legal: duplicateActivation|phased）");

        // 期望投影 = bundle 构造的最后一步（G6 规则：certified Expected
        // 是 registered bundle 的最终投影，之后不得再被 Expect(...) 改写）
        var bundle = factory(scenarioId);
        return new LibraryScenario(scenarioId, execution.Carrier!, bundle, options);
    }

    /// <summary>全部 golden-bundle 注册键（tripwire / 一致性检查用）。</summary>
    internal static IReadOnlyCollection<string> RegisteredCarriers => Carriers.Keys.ToArray();
}
