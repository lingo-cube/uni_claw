using UniClaw.Kernel.Evidence;
using UniClaw.Kernel.World;
using Xunit;

namespace UniClaw.Kernel.Tests;

/// <summary>
/// PER-009 S3：裁决器冻结规则回归（mechanism.md 字段表/三道门/两类冲突/
/// confidence 盲/不升档）。
/// </summary>
public sealed class ConflictResolverTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Window = TimeSpan.FromMilliseconds(3500);

    private static ConflictResolver.XmlAuthoritySnapshot Snapshot(
        bool identityUnique = true,
        DateTimeOffset? dumpTime = null,
        bool checkable = true,
        string checkedValue = "false") =>
        new("wifi_switch", identityUnique, dumpTime ?? T0, checkable, checkedValue,
            Enabled: true, Selected: false, Focused: false);

    private static ConflictResolver.ConflictCase SwitchConflict(string visionValue = "on") =>
        new(SharedSubjects.State("switch"), new[]
        {
            new ConflictResolver.ConflictingClaim("perception.live.vision", visionValue, T0),
        });

    // ---- 权威域内：Tier 0 定案，confidence 盲，不升档 ----

    [Fact]
    public void StateConflict_ValidAuthority_ClosesByXml_OverrulesVision()
    {
        var d = ConflictResolver.Resolve(SwitchConflict("on"), Snapshot(checkedValue: "false"), Window);
        Assert.Equal(ConflictResolver.Tier.CategoryAuthority, d.Tier);
        Assert.Equal("off", d.ResolvedValue);            // XML checked=false 定案（值域映射）
        Assert.Equal("perception.live.vision", d.OverruledProducer);
        Assert.DoesNotContain("escalate", d.Basis, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StateConflict_StructurallyConfidenceBlind()
    {
        // confidence 盲是类型级事实：ConflictingClaim 无分数字段——
        // 这里固定该不变量，防止未来有人加字段绕开 D13。
        var claimType = typeof(ConflictResolver.ConflictingClaim);
        Assert.Empty(claimType.GetProperties().Where(p =>
            p.Name.Contains("confidence", StringComparison.OrdinalIgnoreCase) ||
            p.Name.Contains("score", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void StateConflict_TriStatePartial_PassesThrough()
    {
        var d = ConflictResolver.Resolve(SwitchConflict(), Snapshot(checkedValue: "partial"), Window);
        Assert.Equal(ConflictResolver.Tier.CategoryAuthority, d.Tier);
        Assert.Equal("partial", d.ResolvedValue);        // D7 三态不压扁
    }

    // ---- 三道门：任一不过 → 剥夺权威（≠视觉获胜）----

    [Fact]
    public void StateConflict_CheckableFalse_RevokesAuthority()
    {
        var d = ConflictResolver.Resolve(SwitchConflict(), Snapshot(checkable: false), Window);
        Assert.Equal(ConflictResolver.Tier.VisionDomain, d.Tier);
        Assert.Null(d.ResolvedValue);
        Assert.Contains("checkable", d.Basis);           // D12 guard：官方语义
    }

    [Fact]
    public void StateConflict_NonUniqueIdentity_RevokesAuthority()
    {
        // 双同名 Switch（S2 fixture 同款场景）：非唯一解析 → 剥夺（防错配）
        var d = ConflictResolver.Resolve(SwitchConflict(), Snapshot(identityUnique: false), Window);
        Assert.Equal(ConflictResolver.Tier.VisionDomain, d.Tier);
        Assert.Contains("IdentityMatched", d.Basis);
    }

    [Fact]
    public void StateConflict_StaleDump_RevokesAuthority()
    {
        // D14：dump 与争议时刻可能描述两个时刻
        var d = ConflictResolver.Resolve(
            SwitchConflict(), Snapshot(dumpTime: T0 - TimeSpan.FromSeconds(10)), Window);
        Assert.Equal(ConflictResolver.Tier.VisionDomain, d.Tier);
        Assert.Contains("FreshEnough", d.Basis);
    }

    // ---- 权威域外：XML 直接不参战 ----

    [Fact]
    public void NonAuthoritySubject_GoesStraightToVisionDomain()
    {
        var frameConflict = new ConflictResolver.ConflictCase(SharedSubjects.Frame, new[]
        {
            new ConflictResolver.ConflictingClaim("perception.live.vision", "{}", T0),
        });
        var d = ConflictResolver.Resolve(frameConflict, Snapshot(), Window);
        Assert.Equal(ConflictResolver.Tier.VisionDomain, d.Tier);
        Assert.Contains("非权威域", d.Basis);
    }

    [Fact]
    public void MissingSnapshot_IsAbsenceNotDefeat()
    {
        var d = ConflictResolver.Resolve(SwitchConflict(), null, Window);
        Assert.Equal(ConflictResolver.Tier.VisionDomain, d.Tier);
        Assert.Contains("缺席", d.Basis);
    }

    // ---- 其他权威字段 ----

    [Fact]
    public void EnabledConflict_ResolvesViaSnapshot_BoolPassthrough()
    {
        var conflict = new ConflictResolver.ConflictCase("button.enabled", new[]
        {
            new ConflictResolver.ConflictingClaim("perception.live.vision", "false", T0),
        });
        var d = ConflictResolver.Resolve(conflict, Snapshot(), Window);
        Assert.Equal(ConflictResolver.Tier.CategoryAuthority, d.Tier);
        Assert.Equal("true", d.ResolvedValue);           // 快照 Enabled=true 定案；布尔透传（值域映射仅 *.state）
        Assert.Equal("perception.live.vision", d.OverruledProducer);
    }
}
