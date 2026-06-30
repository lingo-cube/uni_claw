namespace UniClaw.Core.Domain.Models.Common;

/// <summary>
/// 操作类型枚举
/// </summary>
public enum OperationType
{
    /// <summary>点击操作</summary>
    Click,

    /// <summary>滑动操作</summary>
    Swipe,

    /// <summary>返回操作</summary>
    Back,

    /// <summary>文本输入</summary>
    InputText,

    /// <summary>无操作</summary>
    NoAction,

    /// <summary>等待</summary>
    Wait,

    /// <summary>长按</summary>
    LongPress
}

/// <summary>
/// 定义在节点上执行的操作
/// </summary>
/// <param name="Action">操作类型</param>
/// <param name="Target">目标元素定位方式</param>
/// <param name="Params">操作参数</param>
/// <param name="Restore">可选的状态恢复操作</param>
public sealed record class Operation(
    OperationType Action,
    Target? Target = null,
    Dictionary<string, object>? Params = null,
    RestoreAction? Restore = null)
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

        if (Restore != null)
            dict["restore"] = Restore.ToDictionary();

        return dict;
    }

    /// <summary>
    /// 从字典创建
    /// </summary>
    public static Operation FromDictionary(Dictionary<string, object> data)
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

        RestoreAction? restore = null;
        if (data.TryGetValue("restore", out var r) && r is Dictionary<string, object> restoreDict)
        {
            restore = RestoreAction.FromDictionary(restoreDict);
        }

        return new Operation(
            Action: Enum.Parse<OperationType>((data["action"] as string ?? "NoAction") ?? "NoAction", true),
            Target: target,
            Params: parameters,
            Restore: restore
        );
    }
}
