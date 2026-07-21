namespace UniClaw.Core.Observability;

/// <summary>
/// TraceMetadata — 构建 handler metadata Dictionary 的链式辅助。
/// 统一 null skip、enum→string、key 拼写一致性。
/// </summary>
public static class TraceMetadata
{
    /// <summary>
    /// 开始构建 metadata 字典。
    /// </summary>
    public static Builder Build() => new();

    /// <summary>
    /// 链式 Builder — 3 重载 Add + ToDict。
    /// </summary>
    public sealed class Builder
    {
        private readonly Dictionary<string, object> _dict = new();

        /// <summary>添加 string? 值（null 时跳过）</summary>
        public Builder Add(string key, string? value)
        {
            if (value != null) _dict[key] = value;
            return this;
        }

        /// <summary>添加 object? 值（null 时跳过）</summary>
        public Builder Add(string key, object? value)
        {
            if (value != null) _dict[key] = value;
            return this;
        }

        /// <summary>添加 nullable enum 值（null 时跳过，非 null 时 ToString）</summary>
        public Builder Add<T>(string key, T? value) where T : struct, Enum
        {
            if (value.HasValue) _dict[key] = value.Value.ToString();
            return this;
        }

        /// <summary>添加 int? 值（null 时跳过）</summary>
        public Builder Add(string key, int? value)
        {
            if (value.HasValue) _dict[key] = value.Value;
            return this;
        }

        /// <summary>添加 double? 值（null 时跳过）</summary>
        public Builder Add(string key, double? value)
        {
            if (value.HasValue) _dict[key] = value.Value;
            return this;
        }

        /// <summary>添加 bool? 值（null 时跳过）</summary>
        public Builder Add(string key, bool? value)
        {
            if (value.HasValue) _dict[key] = value.Value;
            return this;
        }

        /// <summary>构建最终字典</summary>
        public Dictionary<string, object> ToDict() => _dict;
    }
}
