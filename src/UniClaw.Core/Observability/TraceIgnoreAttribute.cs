namespace UniClaw.Core.Observability;

/// <summary>
/// TraceIgnoreAttribute — 标记 property 不进入生成的 trace metadata。
/// 源生成器在自动提取 return type 属性时跳过标记了此属性的属性。
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class TraceIgnoreAttribute : Attribute
{
}
