using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace UniClaw.Core.Domain.Models.Common;

/// <summary>
/// 操作类型枚举（PRD §5.3 受限集合）。无 Wait / LongPress。
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
    NoAction
}

/// <summary>
/// 定义在节点上执行的操作。action 受限（枚举 + 越界校验）；Params 默认空不可变字典；
/// 无 ToDictionary/FromDictionary（PRD §4.4/§5.3）。
/// </summary>
public sealed record class Operation
{
    /// <summary>操作类型</summary>
    [JsonPropertyName("action")]
    public OperationType Action { get; init; }

    /// <summary>目标元素定位方式</summary>
    [JsonPropertyName("target")]
    public Target? Target { get; init; }

    /// <summary>操作参数（默认空，不可变）</summary>
    [JsonPropertyName("params")]
    public ImmutableDictionary<string, object> Params { get; init; } = ImmutableDictionary<string, object>.Empty;

    /// <summary>可选的状态恢复操作</summary>
    [JsonPropertyName("restore")]
    public RestoreAction? Restore { get; init; }

    /// <param name="Action">操作类型（受限集合，越界抛异常）</param>
    /// <param name="Target">目标元素定位方式</param>
    /// <param name="Params">操作参数（默认空）</param>
    /// <param name="Restore">可选的状态恢复操作</param>
    public Operation(
        OperationType Action,
        Target? Target = null,
        ImmutableDictionary<string, object>? Params = null,
        RestoreAction? Restore = null)
    {
        if (!Enum.IsDefined(Action))
            throw new DomainValidationException(nameof(Action), Action);

        this.Action = Action;
        this.Target = Target;
        this.Params = Params ?? ImmutableDictionary<string, object>.Empty;
        this.Restore = Restore;
    }
}
