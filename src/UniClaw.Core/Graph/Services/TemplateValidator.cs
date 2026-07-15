using UniClaw.Core.Domain.Models.Content;
using UniClaw.Core.Graph.Models;

namespace UniClaw.Core.Graph.Services;

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
