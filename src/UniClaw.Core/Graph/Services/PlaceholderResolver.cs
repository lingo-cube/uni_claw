using UniClaw.Core.Domain;

namespace UniClaw.Core.Graph.Services;

/// <summary>
/// 占位符解析器
/// </summary>
public static class PlaceholderResolver
{
    /// <summary>
    /// 支持的占位符:
    /// {{item_text}}      - UI元素文本
    /// {{item_index}}     - UI元素索引
    /// {{coordinate_x}}   - X坐标
    /// {{coordinate_y}}   - Y坐标
    /// {{parent_id}}      - 父节点ID
    /// </summary>
    private static readonly string[] KnownPlaceholders =
    [
        "item_text", "item_index", "coordinate_x", "coordinate_y",
        "parent_id", "node_name", "timestamp"
    ];

    /// <summary>
    /// 判断占位符是否为已知（受支持）的占位符
    /// </summary>
    /// <param name="placeholder">占位符名称</param>
    /// <returns>已知返回 true</returns>
    public static bool IsKnownPlaceholder(string placeholder) =>
        KnownPlaceholders.Contains(placeholder);

    /// <summary>
    /// 解析占位符
    /// </summary>
    public static object Resolve(object template, Dictionary<string, object> context)
    {
        return template switch
        {
            string str => ResolveString(str, context),
            Dictionary<string, object> dict => ResolveDictionary(dict, context),
            List<object> list => ResolveList(list, context),
            _ => template
        };
    }

    /// <summary>
    /// 解析字符串占位符
    /// </summary>
    private static string ResolveString(string template, Dictionary<string, object> context)
    {
        var result = template;
        foreach (var (key, value) in context)
        {
            var placeholder = $"{{{{{key}}}}}";
            result = result.Replace(placeholder, value?.ToString() ?? "");
        }

        // C-2: 替换后若仍有未解析占位符 → fail-fast（替代静默保留原占位符）。
        if (HasUnresolvedPlaceholders(result))
        {
            var unresolved = string.Join(", ", ExtractPlaceholders(result));
            throw new DomainValidationException("placeholder", unresolved);
        }

        return result;
    }

    /// <summary>
    /// 解析字典占位符
    /// </summary>
    private static Dictionary<string, object> ResolveDictionary(
        Dictionary<string, object> template,
        Dictionary<string, object> context)
    {
        var result = new Dictionary<string, object>();
        foreach (var (key, value) in template)
        {
            result[key] = Resolve(value, context);
        }
        return result;
    }

    /// <summary>
    /// 解析列表占位符
    /// </summary>
    private static List<object> ResolveList(
        List<object> template,
        Dictionary<string, object> context)
    {
        var result = new List<object>();
        foreach (var item in template)
        {
            result.Add(Resolve(item, context));
        }
        return result;
    }

    /// <summary>
    /// 检查是否还有未解析的占位符
    /// </summary>
    public static bool HasUnresolvedPlaceholders(string text)
    {
        return text.Contains("{{") && text.Contains("}}");
    }

    /// <summary>
    /// 提取占位符名称
    /// </summary>
    public static IEnumerable<string> ExtractPlaceholders(string text)
    {
        const string pattern = @"\{\{(\w+)\}\}";
        var matches = System.Text.RegularExpressions.Regex.Matches(text, pattern);
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            if (match.Groups.Count > 1)
            {
                yield return match.Groups[1].Value;
            }
        }
    }
}
