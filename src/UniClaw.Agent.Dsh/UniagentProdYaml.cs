namespace UniClaw.Agent.Dsh;

/// <summary>
/// F2: the single runtime configuration source for the uniagent-prod
/// realization. Provider, model names, and the DSH service endpoint are read
/// from <c>.dsh/profiles/uniagent-prod.yaml</c> (or the path in
/// <c>UNICLAW_UNIAGENT_PROD_CONFIG</c>); changing them never requires
/// recompiling the Product assembly and never changes the Product protocol.
///
/// Profile capability identity remains compiled code authority
/// (<see cref="UniagentProdProfile"/> + <see cref="CapabilityManifest"/>):
/// the loader cross-checks the config's profile block against the frozen
/// identity and fails closed on drift. Credentials are NOT part of this file;
/// they come from environment/secure runtime config only.
/// </summary>
public static class UniagentProdYaml
{
    public const string DefaultConfigRelativePath = ".dsh/profiles/uniagent-prod.yaml";
    public const string ConfigPathEnvironmentVariable = "UNICLAW_UNIAGENT_PROD_CONFIG";

    /// <summary>Loads from the environment-configured path, or the repository
    /// profile file discovered by walking up from the current assembly.</summary>
    public static UniagentProdConfiguration LoadDefault()
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
                    $"uniagent-prod configuration not found at repository root: {DefaultConfigRelativePath}");
            directory = directory.Parent!;
        }
        throw new FileNotFoundException(
            $"uniagent-prod configuration not found; set {ConfigPathEnvironmentVariable} or run from the repository");
    }

    public static UniagentProdConfiguration Load(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("configuration path is required", nameof(path));
        if (!File.Exists(path))
            throw new FileNotFoundException("uniagent-prod configuration file not found", path);

        var document = YamlDocument.Parse(File.ReadAllLines(path));
        return MapConfiguration(document, path);
    }

    private static UniagentProdConfiguration MapConfiguration(YamlDocument document, string path)
    {
        var profileId = document.RequireScalar(new[] { "profileId" });
        var profileVersion = document.RequireScalar(new[] { "profileVersion" });
        var capabilities = document.RequireList(new[] { "capabilities" });

        // The compiled profile identity is the handshake authority; a config
        // file claiming a different profile is drift and fails closed.
        if (!string.Equals(profileId, UniagentProdProfile.ProfileId, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"config-profile-mismatch:profileId:{profileId}!={UniagentProdProfile.ProfileId} ({path})");
        if (!string.Equals(profileVersion, UniagentProdProfile.ProfileVersion, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"config-profile-mismatch:profileVersion:{profileVersion}!={UniagentProdProfile.ProfileVersion} ({path})");
        var expectedCapabilities = CapabilityManifest.ProductHeadless.NormalizedCapabilities;
        var reportedCapabilities = capabilities
            .Where(static c => !string.IsNullOrWhiteSpace(c))
            .Select(static c => c.Trim())
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (!expectedCapabilities.SequenceEqual(reportedCapabilities, StringComparer.Ordinal))
            throw new InvalidOperationException(
                $"config-profile-mismatch:capabilities ({path}): expected [{string.Join(",", expectedCapabilities)}]");

        var baseUrl = document.RequireScalar(new[] { "service", "baseUrl" });
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var serviceUri)
            || (serviceUri.Scheme != "http" && serviceUri.Scheme != "https"))
            throw new InvalidOperationException($"config-invalid:service.baseUrl:{baseUrl} ({path})");

        // CAP-007：保留完整 choices 目录（per-profile 选择按 key 解析；任何
        // choice 条目残缺都 fail-closed，不再只校验被选中者）。
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
        // profile 名的产品值域校验在 DshModelManagement（它拥有 LogicalProfileId 词汇）。
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

        return UniagentProdConfiguration.Create(
            selectedChoice,
            new DshServiceEndpoint(serviceUri),
            selected,
            choices,
            profileSelections.Count == 0 ? null : profileSelections);
    }

    private static string RequireEntryScalar(
        Dictionary<string, object> entry, string key, string diagnosticPath, string path)
        => entry.TryGetValue(key, out var value) && value is string text && !string.IsNullOrWhiteSpace(text)
            ? text
            : throw new InvalidOperationException($"config-missing:{diagnosticPath} ({path})");

    /// <summary>
    /// Minimal purpose-built YAML subset parser for the uniagent-prod config:
    /// two-space indentation nesting, scalar mappings, and single-level
    /// string lists ("- item"). Anything else fails closed with the
    /// offending line.
    /// </summary>
    private sealed class YamlDocument
    {
        private readonly Dictionary<string, object> _root = new(StringComparer.Ordinal);

        public static YamlDocument Parse(string[] lines)
        {
            var document = new YamlDocument();
            var stack = new List<(int Indent, Dictionary<string, object> Map)> { (0, document._root) };
            for (var index = 0; index < lines.Length; index++)
            {
                var rawLine = lines[index];
                var line = rawLine.TrimEnd();
                if (line.Length is 0 || line.TrimStart().StartsWith('#'))
                    continue;
                var indent = line.Length - line.TrimStart().Length;
                var content = line.TrimStart();
                while (stack.Count > 1 && indent < stack[^1].Indent)
                    stack.RemoveAt(stack.Count - 1);
                if (indent != stack[^1].Indent)
                    throw new InvalidOperationException($"config-syntax:unexpected-indent:{rawLine}");

                var separator = content.IndexOf(':', StringComparison.Ordinal);
                if (separator <= 0)
                    throw new InvalidOperationException($"config-syntax:expected-mapping:{rawLine}");
                var key = content[..separator].Trim();
                var value = content[(separator + 1)..].Trim();
                var map = stack[^1].Map;

                if (value.Length is not 0)
                {
                    map[key] = Unquote(value);
                    continue;
                }

                // Empty value: nested mapping or string list, decided by the
                // next significant line's indentation.
                var childIndent = -1;
                var childContent = "";
                for (var look = index + 1; look < lines.Length; look++)
                {
                    var candidate = lines[look].TrimEnd();
                    if (candidate.Length is 0 || candidate.TrimStart().StartsWith('#'))
                        continue;
                    childIndent = candidate.Length - candidate.TrimStart().Length;
                    childContent = candidate.TrimStart();
                    break;
                }
                if (childContent.StartsWith("- ", StringComparison.Ordinal))
                {
                    var items = new List<string>();
                    var listIndex = index + 1;
                    for (; listIndex < lines.Length; listIndex++)
                    {
                        var itemLine = lines[listIndex].TrimEnd();
                        if (itemLine.Length is 0 || itemLine.TrimStart().StartsWith('#'))
                            continue;
                        var itemIndent = itemLine.Length - itemLine.TrimStart().Length;
                        var item = itemLine.TrimStart();
                        if (itemIndent != childIndent || !item.StartsWith("- ", StringComparison.Ordinal))
                            break;
                        items.Add(Unquote(item["- ".Length..].Trim()));
                    }
                    map[key] = items.ToArray();
                    index = listIndex - 1;
                }
                else if (childIndent > indent)
                {
                    var nested = new Dictionary<string, object>(StringComparer.Ordinal);
                    map[key] = nested;
                    stack.Add((childIndent, nested));
                }
                else
                {
                    // Empty block (key with neither value nor children).
                    map[key] = "";
                }
            }
            return document;
        }

        public string RequireScalar(string[] path)
        {
            if (Walk(path) is string value && !string.IsNullOrWhiteSpace(value))
                return value;
            throw new InvalidOperationException($"config-missing:{string.Join(".", path)}");
        }

        /// <summary>CAP-007：可选路径读取（profiles 块缺省合法；返回原始节点）。</summary>
        public object? TryWalk(string[] path) => Walk(path);

        public IReadOnlyList<string> RequireList(string[] path)
        {
            if (Walk(path) is string[] list)
                return list;
            throw new InvalidOperationException($"config-missing-list:{string.Join(".", path)}");
        }

        private object? Walk(string[] path)
        {
            object? current = _root;
            foreach (var segment in path)
            {
                if (current is Dictionary<string, object> map
                    && map.TryGetValue(segment, out var next))
                {
                    current = next;
                }
                else
                {
                    return null;
                }
            }
            return current;
        }

        private static string Unquote(string value)
        {
            if (value.Length >= 2
                && ((value.StartsWith('"') && value.EndsWith('"'))
                    || (value.StartsWith('\'') && value.EndsWith('\''))))
                return value[1..^1];
            return value;
        }
    }
}
