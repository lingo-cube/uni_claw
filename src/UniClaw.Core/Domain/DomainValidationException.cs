namespace UniClaw.Core.Domain;

/// <summary>
/// 领域对象构造期校验失败时抛出。携带非法字段名与非法值，fail-fast，不静默构造非法对象。
/// (Thrown on construction-time validation failure. Carries the offending field name and value; fail-fast.)
/// </summary>
public sealed class DomainValidationException : Exception
{
    /// <summary>
    /// 非法字段名
    /// </summary>
    public string FieldName { get; }

    /// <summary>
    /// 非法值（可能为 null）
    /// </summary>
    public object? IllegalValue { get; }

    /// <param name="fieldName">非法字段名</param>
    /// <param name="illegalValue">非法值（可能为 null）</param>
    public DomainValidationException(string fieldName, object? illegalValue)
        : base($"Domain validation failed for field '{fieldName}': illegal value '{FormatValue(illegalValue)}'.")
    {
        FieldName = fieldName;
        IllegalValue = illegalValue;
    }

    /// <param name="fieldName">非法字段名</param>
    /// <param name="illegalValue">非法值（可能为 null）</param>
    /// <param name="message">自定义错误消息</param>
    public DomainValidationException(string fieldName, object? illegalValue, string message)
        : base(message)
    {
        FieldName = fieldName;
        IllegalValue = illegalValue;
    }

    private static string FormatValue(object? value) => value is null ? "null" : value.ToString() ?? value.GetType().Name;
}
