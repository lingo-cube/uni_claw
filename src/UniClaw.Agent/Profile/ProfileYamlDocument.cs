namespace UniClaw.Agent.Profile;

/// <summary>
/// PRF-002 — 产品侧共享的极简 YAML 子集解析器（自 <c>UniagentProdYaml</c>
/// 私有实现上移为产品/adapter 双 loader 共用）：两空格缩进嵌套、标量映射、
/// 单层字符串列表（<c>- item</c>）。其余形态带行号 fail-closed。
/// </summary>
public sealed class ProfileYamlDocument
{
    private readonly Dictionary<string, object> _root = new(StringComparer.Ordinal);

    /// <summary>Parse fails closed on any syntax this subset cannot represent.</summary>
    public static ProfileYamlDocument Parse(string[] lines)
    {
        var document = new ProfileYamlDocument();
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
            if (map.ContainsKey(key))
                throw new InvalidOperationException($"config-syntax:duplicate-key:{key}:{rawLine}");

            if (value == "[]")
            {
                map[key] = Array.Empty<string>();
                continue;
            }

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

    /// <summary>Required non-empty scalar at the given path.</summary>
    public string RequireScalar(string[] path)
    {
        if (Walk(path) is string value && !string.IsNullOrWhiteSpace(value))
            return value;
        throw new InvalidOperationException($"config-missing:{string.Join(".", path)}");
    }

    /// <summary>Optional node read; absent path is legal and returns null.</summary>
    public object? TryWalk(string[] path) => Walk(path);

    /// <summary>Required single-level string list at the given path.</summary>
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
