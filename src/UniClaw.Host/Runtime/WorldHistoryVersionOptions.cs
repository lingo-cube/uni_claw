namespace UniClaw.Host.Runtime;

/// <summary>Product-side World file policy; revisions, not outer operations, count toward the bound.</summary>
public sealed record WorldHistoryVersionOptions
{
    public int BaselineInterval { get; init; } = 32;
}
