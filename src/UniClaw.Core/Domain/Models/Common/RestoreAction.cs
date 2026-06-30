namespace UniClaw.Core.Domain.Models.Common;

/// <summary>
/// 状态恢复操作，用于在操作后恢复原始状态
/// </summary>
/// <param name="Action">恢复操作类型</param>
/// <param name="Target">目标元素</param>
/// <param name="Params">操作参数</param>
public sealed record class RestoreAction(
    OperationType Action,
    Target? Target = null,
    Dictionary<string, object>? Params = null)
{
    /// <summary>
    /// 转换为字典
    /// </summary>
    public Dictionary<string, object> ToDictionary()
    {
        var dict = new Dictionary<string, object>
        {
            ["action"] = Action.ToString().ToLowerInvariant()
        };

        if (Target != null)
            dict["target"] = Target.ToDictionary();

        if (Params != null && Params.Count > 0)
            dict["params"] = new Dictionary<string, object>(Params);

        return dict;
    }

    /// <summary>
    /// 从字典创建
    /// </summary>
    public static RestoreAction? FromDictionary(Dictionary<string, object> data)
    {
        try
        {
            Target? target = null;
            if (data.TryGetValue("target", out var t) && t is Dictionary<string, object> targetDict)
            {
                target = Target.FromDictionary(targetDict);
            }

            Dictionary<string, object>? parameters = null;
            if (data.TryGetValue("params", out var p) && p is Dictionary<string, object> paramsDict)
            {
                parameters = new Dictionary<string, object>(paramsDict);
            }

            return new RestoreAction(
                Action: Enum.Parse<OperationType>((data["action"] as string ?? "NoAction") ?? "NoAction", true),
                Target: target,
                Params: parameters
            );
        }
        catch
        {
            return null;
        }
    }
}
