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

        var selected = document.RequireScalar(new[] { "modelSelection", "selected" });
        var provider = document.RequireScalar(new[] { "modelSelection", "choices", selected, "provider" });
        var name = document.RequireScalar(new[] { "modelSelection", "choices", selected, "name" });

        return UniagentProdConfiguration.Create(
            new ModelConfiguration(provider, name),
            new DshServiceEndpoint(serviceUri),
            selected);
    }

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
