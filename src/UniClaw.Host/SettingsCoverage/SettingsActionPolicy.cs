using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UniClaw.Kernel.Runtime;

namespace UniClaw.Host.SettingsCoverage;

/// <summary>
/// AGT-013：Settings profile 在首次咨询前提供的只读动作策略。
/// 策略不是模型提示词，也不是 EffectBoundary 的替代品；它是 Host 将
/// profile contract 投影给 Agent 并在 dispatch 前做语义 guard 的输入。
/// PRF-005（ADR-0041）：canonical 副本迁 product/policy/（产品声明面，
/// 有 owner、有版本）；本 loader 在消费方加载点 fail-closed——分类底线
/// 缺失、缺 safetyPolicyRevision、默认拒绝缺失一律拒载（Q14 裁决）。
/// </summary>
public sealed record SettingsActionPolicy(
    string SchemaVersion,
    string PolicyRef,
    int SafetyPolicyRevision,
    bool GeneratedBeforeFirstConsultation,
    IReadOnlySet<string> SafeActionClasses,
    IReadOnlySet<string> TargetedActionClasses,
    IReadOnlySet<string> ForbiddenActionClasses,
    IReadOnlyList<string> ForbiddenTargetPatterns,
    string UnknownTargetDisposition,
    string Digest,
    string SourcePath)
{
    public const string SupportedSchemaVersion = "android-settings-action-policy.v1";
    public const string RejectUnknown = "reject";

    /// <summary>PRF-005：危险动作分类底线（Q14「分类完备」的机械定义）——
    /// 任何合法 policy 的 forbidden 集必须完整包含这些类别；缺失即拒载，
    /// 不允许策略作者无意或故意缩小危险面。</summary>
    public static readonly IReadOnlySet<string> RequiredForbiddenFloor =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "destructive", "account-removal", "credential-change",
            "developer-debug", "permission-grant", "unknown-action",
        };

    /// <summary>AGT-015：traversal 目标必须与策略声明的 targeted toggle 绑定。
    /// 返回 null 表示一致；否则返回冲突原因（含声明面），供运行入口在任何
    /// 设备动作/咨询前 fail-closed。静态预评审授权不随 CLI 参数动态扩大。</summary>
    public string? ValidateTraversalTargetDescriptor(string? descriptor)
    {
        var normalized = descriptor?.Trim();
        var declared = string.Join(",", TargetedActionClasses.OrderBy(x => x, StringComparer.Ordinal));
        if (string.IsNullOrWhiteSpace(normalized))
            return $"traversal target descriptor is empty; policy declares: {declared}";
        var semantic = $"toggle:{normalized}";
        if (TargetedActionClasses.Contains(semantic))
            return null;
        return $"traversal target '{normalized}' is not declared by the action policy "
            + $"(required: {semantic}; declared: {declared})";
    }

    /// <summary>策略声明的目标开关文字（"toggle:Wi-Fi" → "Wi-Fi"）。
    /// 可见性冻结规则只对声明目标生效，不绑定任何字面量。</summary>
    public IReadOnlyList<string> DeclaredTargetSwitchLabels =>
        TargetedActionClasses
            .Where(c => c.StartsWith("toggle:", StringComparison.OrdinalIgnoreCase))
            .Select(c => c["toggle:".Length..].Trim())
            .Where(l => l.Length > 0)
            .ToList();

    public string AgentProjection() =>
        $"policyRef={PolicyRef}; safetyPolicyRevision={SafetyPolicyRevision}; policyDigest={Digest}; "
        + $"safe=[{string.Join(",", SafeActionClasses.OrderBy(x => x, StringComparer.Ordinal))}]; "
        + $"targeted=[{string.Join(",", TargetedActionClasses.OrderBy(x => x, StringComparer.Ordinal))}]; "
        + $"forbidden=[{string.Join(",", ForbiddenActionClasses.OrderBy(x => x, StringComparer.Ordinal))}]; "
        + $"unknownTargetDisposition={UnknownTargetDisposition}; "
        + "effectClass=runtime token from context.allowedEffects; "
        + "semanticMapping={navigate:tap,back:tap,scroll:swipe-up}";

    public static SettingsActionPolicy Load(string path)
    {
        if (!File.Exists(path))
            throw NotReady($"policy file not found: {path}");

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(File.ReadAllText(path));
        }
        catch (Exception error) when (error is JsonException or IOException)
        {
            throw NotReady($"policy file invalid: {path}: {error.Message}");
        }

        using (document)
        {
            var root = document.RootElement;
            var schema = RequiredString(root, "schemaVersion", path);
            if (!string.Equals(schema, SupportedSchemaVersion, StringComparison.Ordinal))
                throw NotReady($"unsupported policy schema: {schema}");
            var policyRef = RequiredString(root, "policyRef", path);
            // PRF-005：policy 必须携带可审计修订号（正整数）。
            if (!root.TryGetProperty("safetyPolicyRevision", out var revisionValue)
                || revisionValue.ValueKind != JsonValueKind.Number
                || !revisionValue.TryGetInt32(out var revision)
                || revision < 1)
                throw NotReady($"safetyPolicyRevision must be a positive integer [{path}]");
            var generated = root.TryGetProperty("generatedBeforeFirstConsultation", out var generatedValue)
                && generatedValue.ValueKind == JsonValueKind.True;
            if (!generated)
                throw NotReady("generatedBeforeFirstConsultation must be true");

            var safe = RequiredList(root, "safeActionClasses", path);
            var targeted = RequiredList(root, "targetedActionClasses", path);
            var forbidden = RequiredList(root, "forbiddenActionClasses", path);
            var patterns = RequiredList(root, "forbiddenTargetPatterns", path);
            var unknown = RequiredString(root, "unknownTargetDisposition", path);
            if (!string.Equals(unknown, RejectUnknown, StringComparison.Ordinal))
                throw NotReady($"unknownTargetDisposition must be '{RejectUnknown}'");
            if (safe.Count == 0 || targeted.Count == 0 || forbidden.Count == 0)
                throw NotReady("safe/targeted/forbidden action sets must be non-empty");
            if (patterns.Count == 0)
                throw NotReady("forbiddenTargetPatterns must be non-empty");
            if (safe.Intersect(targeted, StringComparer.OrdinalIgnoreCase).Any()
                || safe.Intersect(forbidden, StringComparer.OrdinalIgnoreCase).Any()
                || targeted.Intersect(forbidden, StringComparer.OrdinalIgnoreCase).Any())
                throw NotReady("action sets must be disjoint");
            // PRF-005：分类底线执法——危险类别词汇缺失 = 拒载。
            var forbiddenSet = new HashSet<string>(forbidden, StringComparer.OrdinalIgnoreCase);
            var missingFloor = RequiredForbiddenFloor
                .Where(@class => !forbiddenSet.Contains(@class))
                .Order(StringComparer.Ordinal).ToArray();
            if (missingFloor.Length > 0)
                throw NotReady("forbiddenActionClasses is missing required safety floor: "
                    + $"[{string.Join(", ", missingFloor)}]; a policy must not narrow the "
                    + $"danger taxonomy [{string.Join(", ", RequiredForbiddenFloor.Order(StringComparer.Ordinal))}]");

            var canonical = string.Join("\n", new[]
            {
                schema,
                policyRef,
                revision.ToString(System.Globalization.CultureInfo.InvariantCulture),
                string.Join("|", safe.OrderBy(x => x, StringComparer.Ordinal)),
                string.Join("|", targeted.OrderBy(x => x, StringComparer.Ordinal)),
                string.Join("|", forbidden.OrderBy(x => x, StringComparer.Ordinal)),
                string.Join("|", patterns.OrderBy(x => x, StringComparer.OrdinalIgnoreCase)),
                unknown,
            });
            var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
            return new SettingsActionPolicy(
                schema, policyRef, revision, generated,
                new HashSet<string>(safe, StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(targeted, StringComparer.OrdinalIgnoreCase),
                new HashSet<string>(forbidden, StringComparer.OrdinalIgnoreCase),
                patterns,
                unknown,
                digest,
                Path.GetFullPath(path));
        }
    }

    private static string RequiredString(JsonElement root, string name, string path)
    {
        if (!root.TryGetProperty(name, out var value)
            || value.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(value.GetString()))
            throw NotReady($"policy field missing or invalid: {name} [{path}]");
        return value.GetString()!;
    }

    private static List<string> RequiredList(JsonElement root, string name, string path)
    {
        if (!root.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
            throw NotReady($"policy list missing or invalid: {name} [{path}]");
        var items = value.EnumerateArray().ToList();
        if (items.Any(item => item.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(item.GetString())))
            throw NotReady($"policy list contains non-string/empty item: {name} [{path}]");
        var values = items
            .Select(item => item.GetString()!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (values.Count == 0)
            throw NotReady($"policy list empty: {name} [{path}]");
        return values;
    }

    private static InvalidOperationException NotReady(string detail) =>
        new($"PROFILE_CONTRACT_NOT_READY: {detail}");
}

public enum SettingsActionGuardVerdict
{
    Allow,
    NoAction,
    Reject,
}

public sealed record SettingsActionGuardResult(
    SettingsActionGuardVerdict Verdict,
    string SemanticAction,
    string Reason,
    string PolicyDigest);

/// <summary>
/// AGT-013：把 AgentActionStep 映射为 Settings 语义动作并做最终 Host-side
/// dispatch 前 guard。Kernel 仍继续执行自身的 Allowed/Forbidden effect checks。
/// </summary>
public static class SettingsActionGuard
{
    public static AgentDecision? GuardDecision(
        AgentDecision? decision,
        AgentDecisionContext context,
        SettingsActionPolicy policy,
        out SettingsActionGuardResult? result)
    {
        result = null;
        if (decision is null or AgentDecision.NoAction or AgentDecision.Defer)
            return decision;

        AgentActionStep? step = null;
        if (decision is AgentDecision.Act act)
        {
            if (act.Proposal.Steps.Count != 1)
            {
                result = Reject("unknown-action", "multi-step act is not policy-guardable", policy);
                return null;
            }
            step = act.Proposal.Steps[0];
        }
        else if (decision is AgentDecision.Plan plan)
        {
            var acts = plan.Proposal.Items.OfType<PlanItem.ActItem>().Take(2).ToList();
            if (acts.Count > 1)
            {
                result = Reject("unknown-action", "multi-act plan is not policy-guardable", policy);
                return null;
            }
            if (acts.Count == 1)
            {
                var item = acts[0];
                step = new AgentActionStep(item.TargetRole, item.TargetDescriptor,
                    item.EffectClass, item.DesiredState);
            }
        }
        else if (decision is AgentDecision.Policy)
        {
            result = Reject("unknown-action", "policy proposal is not policy-guardable", policy);
            return null;
        }
        if (step is null)
            return decision;

        result = Evaluate(step, context, policy);
        return result.Verdict switch
        {
            SettingsActionGuardVerdict.Allow => decision,
            SettingsActionGuardVerdict.NoAction => new AgentDecision.NoAction(
                new AgentNoActionProposal(context.DecisionId, $"settings-policy:{result.Reason}")),
            _ => null,
        };
    }

    public static SettingsActionGuardResult Evaluate(
        AgentActionStep step,
        AgentDecisionContext context,
        SettingsActionPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(policy);

        var target = step.TargetDescriptor?.Trim();
        if (string.IsNullOrWhiteSpace(target))
            return Reject("unknown-action", "missing target descriptor", policy);
        if (policy.ForbiddenTargetPatterns.Any(pattern =>
            target.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
            return Reject("forbidden-target", $"target matches forbidden pattern: {target}", policy);

        if (step.DesiredState is not null)
        {
            var semantic = $"toggle:{target}";
            if (!policy.TargetedActionClasses.Contains(semantic))
                return Reject("toggle-non-target", $"targeted toggle is not declared: {semantic}", policy);
            if (step.DesiredState is not ("checked" or "unchecked"))
                return Reject("unknown-action", $"unsupported desiredState: {step.DesiredState}", policy);

            var matching = (context.Elements ?? Array.Empty<ElementSummary>())
                .Where(element => string.Equals(element.Text?.Trim(), target, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (matching.Count == 1
                && string.Equals(matching[0].State, step.DesiredState, StringComparison.OrdinalIgnoreCase))
                return new SettingsActionGuardResult(
                    SettingsActionGuardVerdict.NoAction, semantic,
                    "desired state already satisfied", policy.Digest);
            return policy.TargetedActionClasses.Contains(semantic)
                ? new SettingsActionGuardResult(SettingsActionGuardVerdict.Allow, semantic,
                    "declared targeted toggle with unsatisfied or unknown current state", policy.Digest)
                : Reject("toggle-non-target", semantic, policy);
        }

        // A switch is never a navigation control.  Requiring the typed state
        // here prevents an agent from turning a missing desiredState into a
        // safe-looking tap and bypassing the targeted-toggle policy.
        var switchTarget = string.Equals(step.TargetRole, "switch", StringComparison.OrdinalIgnoreCase)
            || (context.Elements ?? Array.Empty<ElementSummary>()).Any(element =>
                string.Equals(element.Text?.Trim(), target, StringComparison.OrdinalIgnoreCase)
                && (string.Equals(element.Role, "switch", StringComparison.OrdinalIgnoreCase)
                    || element.Checkable == true));
        if (switchTarget)
            return Reject("unknown-action", "switch target requires typed desiredState", policy);

        // Only the policy-declared target switch freezes navigation (AGT-015：
        // 字面 "Wi-Fi" 通用化——非声明目标的可见开关不得冻结其他任务的导航）。
        var declaredLabels = policy.DeclaredTargetSwitchLabels;
        var targetedSwitchVisible = (context.Elements ?? Array.Empty<ElementSummary>()).Any(element =>
            string.Equals(element.Role, "switch", StringComparison.OrdinalIgnoreCase)
            && declaredLabels.Any(label =>
                string.Equals(element.Text?.Trim(), label, StringComparison.OrdinalIgnoreCase)));
        if (targetedSwitchVisible && step.EffectClass is "tap" or "click")
            return Reject("unknown-action", "target switch is visible; choose typed desiredState before another navigation", policy);

        var safeSemantic = step.EffectClass switch
        {
            "swipe-up" => "scroll",
            "tap" or "click" when string.Equals(target, "Navigate up", StringComparison.OrdinalIgnoreCase) => "back",
            "tap" or "click" => "navigate",
            _ => "unknown-action",
        };
        if (policy.SafeActionClasses.Contains(safeSemantic))
            return new SettingsActionGuardResult(SettingsActionGuardVerdict.Allow, safeSemantic,
                "safe action class", policy.Digest);
        return Reject(safeSemantic, $"action class not declared: {step.EffectClass}", policy);
    }

    private static SettingsActionGuardResult Reject(
        string semantic,
        string reason,
        SettingsActionPolicy policy) =>
        new(SettingsActionGuardVerdict.Reject, semantic, reason, policy.Digest);
}
