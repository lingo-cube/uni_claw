using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace UniClaw.Core.Domain.Models.Common;

/// <summary>
/// 状态恢复操作，用于在操作后恢复原始状态。action 同 Operation 校验；Params 默认空；
/// 无 ToDictionary/FromDictionary（PRD §5.3）。
/// </summary>
public sealed record class RestoreAction
{
    /// <summary>恢复操作类型</summary>
    [JsonPropertyName("action")]
    public OperationType Action { get; init; }

    /// <summary>目标元素</summary>
    [JsonPropertyName("target")]
    public Target? Target { get; init; }

    /// <summary>操作参数（默认空，不可变）</summary>
    [JsonPropertyName("params")]
    public ImmutableDictionary<string, object> Params { get; init; } = ImmutableDictionary<string, object>.Empty;

    /// <param name="Action">恢复操作类型（受限集合，越界抛异常）</param>
    /// <param name="Target">目标元素</param>
    /// <param name="Params">操作参数（默认空）</param>
    public RestoreAction(
        OperationType Action,
        Target? Target = null,
        ImmutableDictionary<string, object>? Params = null)
    {
        if (!Enum.IsDefined(Action))
            throw new DomainValidationException(nameof(Action), Action);

        this.Action = Action;
        this.Target = Target;
        this.Params = Params ?? ImmutableDictionary<string, object>.Empty;
    }
}
