using UniClaw.Agent.Profile;

namespace UniClaw.Agent.Dsh;

/// <summary>
/// PRF-002/ADR-0041 — DSH realization 绑定 loader。读取
/// <c>.dsh/product/uniagent-prod-bindings.yaml</c>（或
/// <c>UNICLAW_UNIAGENT_DSH_BINDINGS</c> 指定路径）：modelSelection
/// （CAP-007 语义：selected 缺省 + choices 目录 + profiles per-role 单选/
/// 偏好序，任何 choice 残缺 fail-closed）与 service.baseUrl。产品身份与
/// 模型角色声明不在此文件（见 <see cref="UniAgentProfileYaml"/>）。
/// </summary>
public static class UniagentDshBindingsYaml
{
    public const string DefaultConfigRelativePath = ".dsh/product/uniagent-prod-bindings.yaml";
    public const string ConfigPathEnvironmentVariable = "UNICLAW_UNIAGENT_DSH_BINDINGS";

    /// <summary>Loads from the environment-configured path, or the repository
    /// binding file discovered by walking up from the current assembly.</summary>
    public static UniagentDshBindings LoadDefault()
    {
        var configured = Environment.GetEnvironmentVariable(ConfigPathEnvironmentVariable);
        return Load(configured is { Length: > 0 } ? configured : ResolveDefaultPath());
    }

    public static string ResolveDefaultPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, DefaultConfigRelativePath);
            if (File.Exists(candidate))
                return candidate;
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                throw new FileNotFoundException(
                    $"uniagent-prod DSH bindings not found at repository root: {DefaultConfigRelativePath}");
            directory = directory.Parent!;
        }
        throw new FileNotFoundException(
            $"uniagent-prod DSH bindings not found; set {ConfigPathEnvironmentVariable} or run from the repository");
    }

    public static UniagentDshBindings Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("bindings path is required", nameof(path));
        if (!File.Exists(path))
            throw new FileNotFoundException("uniagent-prod DSH bindings file not found", path);

        var document = ProfileYamlDocument.Parse(File.ReadAllLines(path));

        var baseUrl = document.RequireScalar(new[] { "service", "baseUrl" });
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var serviceUri)
            || (serviceUri.Scheme != "http" && serviceUri.Scheme != "https"))
            throw new InvalidOperationException($"config-invalid:service.baseUrl:{baseUrl} ({path})");

        // CAP-007：保留完整 choices 目录（per-profile 选择按 key 解析；任何
        // choice 条目残缺都 fail-closed，不只校验被选中者）。
        var choices = new Dictionary<string, ModelConfiguration>(StringComparer.Ordinal);
        if (document.TryWalk(new[] { "modelSelection", "choices" }) is Dictionary<string, object> choiceMap)
        {
            foreach (var (choiceKey, value) in choiceMap)
            {
                if (value is not Dictionary<string, object> entry)
                    throw new InvalidOperationException(
                        $"config-invalid:modelSelection.choices.{choiceKey} ({path})");
                choices[choiceKey] = new ModelConfiguration(
                    RequireEntryScalar(entry, "provider", $"modelSelection.choices.{choiceKey}.provider", path),
                    RequireEntryScalar(entry, "name", $"modelSelection.choices.{choiceKey}.name", path));
            }
        }
        if (choices.Count == 0)
            throw new InvalidOperationException($"config-missing:modelSelection.choices ({path})");

        var selected = document.RequireScalar(new[] { "modelSelection", "selected" });
        if (!choices.TryGetValue(selected, out var selectedChoice))
            throw new InvalidOperationException(
                $"config-missing:modelSelection.choices.{selected} ({path})");

        // CAP-007：可选 per-profile 选择块——profile → choice key（标量=单选；
        // "- " 列表=有序偏好，首选在前）。引用不存在的 choice 一律 fail-closed。
        // PRF-002：profile 名的值域校验移至 FromBindings（按产品 modelRoles
        // 声明核对，绑定引用产品未声明的角色同样 fail-closed）。
        var profileSelections = new List<ModelProfileSelection>();
        if (document.TryWalk(new[] { "modelSelection", "profiles" }) is Dictionary<string, object> profiles)
        {
            foreach (var (profile, value) in profiles)
            {
                if (string.IsNullOrWhiteSpace(profile))
                    throw new InvalidOperationException(
                        $"config-invalid:modelSelection.profiles.<empty> ({path})");
                IReadOnlyList<string> keys = value switch
                {
                    string single => new[] { single },
                    string[] ordered => ordered,
                    _ => throw new InvalidOperationException(
                        $"config-invalid:modelSelection.profiles.{profile} ({path}): scalar choice key or ordered list required"),
                };
                var selection = new ModelProfileSelection(profile, keys);
                if (!selection.IsValid)
                    throw new InvalidOperationException(
                        $"config-invalid:modelSelection.profiles.{profile} ({path}): at least one non-empty choice key required");
                foreach (var key in selection.ChoiceKeys)
                    if (!choices.ContainsKey(key))
                        throw new InvalidOperationException(
                            $"config-invalid:modelSelection.profiles.{profile} ({path}): unknown choice '{key}'");
                profileSelections.Add(selection);
            }
        }

        return UniagentDshBindings.Create(
            selectedChoice,
            new DshServiceEndpoint(serviceUri),
            selected,
            choices,
            profileSelections.Count == 0 ? null : profileSelections.ToArray());
    }

    private static string RequireEntryScalar(
        Dictionary<string, object> entry, string key, string diagnosticPath, string path)
        => entry.TryGetValue(key, out var value) && value is string text && !string.IsNullOrWhiteSpace(text)
            ? text
            : throw new InvalidOperationException($"config-missing:{diagnosticPath} ({path})");
}
