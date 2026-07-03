using UniClaw.Core.Domain.Models.Content;

namespace UniClaw.Core.Graph.Models;

/// <summary>
/// 模板 - 可重用的节点模式
/// </summary>
/// <param name="TemplateId">模板ID</param>
/// <param name="NodeType">节点类型</param>
/// <param name="Operation">操作配置</param>
/// <param name="Precondition">前置条件配置</param>
/// <param name="ChildrenStrategy">子节点策略配置</param>
/// <param name="ErrorPolicy">错误策略配置</param>
/// <param name="ExitCondition">退出条件配置</param>
/// <param name="Meta">元数据</param>
public sealed record class Template(
    string TemplateId,
    NodeType NodeType,
    Dictionary<string, object> Operation,
    Dictionary<string, object>? Precondition = null,
    Dictionary<string, object>? ChildrenStrategy = null,
    Dictionary<string, object>? ErrorPolicy = null,
    Dictionary<string, object>? ExitCondition = null,
    Dictionary<string, object>? Meta = null);

/// <summary>
/// 模板注册表接口
/// </summary>
public interface ITemplateRegistry
{
    /// <summary>
    /// 获取模板
    /// </summary>
    Template? GetTemplate(string templateId);

    /// <summary>
    /// 检查模板是否存在
    /// </summary>
    bool HasTemplate(string templateId);

    /// <summary>
    /// 获取所有模板ID
    /// </summary>
    IEnumerable<string> GetTemplateIds();

    /// <summary>
    /// 从文件加载模板
    /// </summary>
    Task LoadFromFileAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// 实例化模板
    /// </summary>
    TraversalNode? Instantiate(string templateId, Dictionary<string, object> context);
}

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

/// <summary>
/// 模板验证器
/// </summary>
public static class TemplateValidator
{
    /// <summary>
    /// 验证模板
    /// </summary>
    public static bool Validate(Template template, out List<string> errors)
    {
        errors = new List<string>();

        if (string.IsNullOrWhiteSpace(template.TemplateId))
            errors.Add("TemplateId is required");

        if (!Enum.IsDefined(typeof(NodeType), template.NodeType))
            errors.Add($"Invalid NodeType: {template.NodeType}");

        if (template.Operation == null || template.Operation.Count == 0)
            errors.Add("Operation is required");

        // 验证操作结构
        if (template.Operation != null)
        {
            if (!template.Operation.ContainsKey("action"))
                errors.Add("Operation must contain 'action' key");
        }

        return errors.Count == 0;
    }

    /// <summary>
    /// 验证占位符
    /// </summary>
    public static bool ValidatePlaceholders(Dictionary<string, object> template, out List<string> errors)
    {
        errors = new List<string>();
        ValidatePlaceholdersRecursive(template, errors, "$");
        return errors.Count == 0;
    }

    private static void ValidatePlaceholdersRecursive(object obj, List<string> errors, string path)
    {
        switch (obj)
        {
            case string str:
                foreach (var placeholder in PlaceholderResolver.ExtractPlaceholders(str))
                {
                    if (!PlaceholderResolver.IsKnownPlaceholder(placeholder))
                    {
                        errors.Add($"Unknown placeholder '{{{{{placeholder}}}}}' at {path}");
                    }
                }
                break;

            case Dictionary<string, object> dict:
                foreach (var (key, value) in dict)
                {
                    ValidatePlaceholdersRecursive(value, errors, $"{path}.{key}");
                }
                break;

            case List<object> list:
                for (int i = 0; i < list.Count; i++)
                {
                    ValidatePlaceholdersRecursive(list[i], errors, $"{path}[{i}]");
                }
                break;
        }
    }
}
