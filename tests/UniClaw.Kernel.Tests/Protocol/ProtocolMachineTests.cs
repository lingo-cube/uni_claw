using System.Text.Json;
using UniClaw.Kernel.Runtime;
using Xunit;

namespace UniClaw.Kernel.Tests.Protocol;

/// <summary>
/// PROTOCOL-MACHINE-001 Slice 1 — C# 执法牙三件（D12：牙在 C#，读生成产物
/// tests/UniClaw.Kernel.Tests/Protocol/protocol-machine.generated.json；该产物
/// 由 tools/gen-protocol-machine.py 从 schemas/protocol/state-machine.yaml 唯一
/// 生成，手改会被 python --check 漂移检测抓出）：
/// ① 枚举同步（双向）：RunDriveStatus 成员 ⊆ emits ∧ emits ⊆ 成员；
/// ② 转移合法性：生成转移表驱动，已知非法对负向自证（RED 路径可证）；
/// ③ payload type: record 反射存在性（D5：不复制语义，直指 C# 类型）。
/// </summary>
public sealed class ProtocolMachineTests
{
    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))
                && File.Exists(Path.Combine(directory.FullName, "UniClaw.Kernel.slnx")))
                return directory.FullName;
            directory = directory.Parent!;
        }
        throw new InvalidOperationException("未定位到仓库根");
    }

    private static JsonElement LoadMachine()
    {
        var path = Path.Combine(
            RepoRoot(), "tests", "UniClaw.Kernel.Tests", "Protocol",
            "protocol-machine.generated.json");
        return JsonDocument.Parse(File.ReadAllText(path)).RootElement;
    }

    private static HashSet<string> DeclaredEmits(JsonElement machine)
    {
        var emits = new HashSet<string>();
        foreach (var t in machine.GetProperty("machines").GetProperty("run")
                     .GetProperty("transitions").EnumerateArray())
            foreach (var e in t.GetProperty("emits").EnumerateArray())
                emits.Add(e.GetString()!);
        return emits;
    }

    private static HashSet<(string From, string To)> DeclaredTransitions(JsonElement machine)
    {
        var table = new HashSet<(string, string)>();
        foreach (var t in machine.GetProperty("machines").GetProperty("run")
                     .GetProperty("transitions").EnumerateArray())
            table.Add((t.GetProperty("from").GetString()!, t.GetProperty("to").GetString()!));
        return table;
    }

    private static bool IsLegal(HashSet<(string From, string To)> table, string from, string to)
        => table.Contains((from, to));

    // ---- 牙①：RunDriveStatus 枚举同步（双向）----

    [Fact]
    public void RunDriveStatus_Members_AreAllDeclaredInEmits()
    {
        var emits = DeclaredEmits(LoadMachine());
        var missing = Enum.GetNames<RunDriveStatus>().Where(n => !emits.Contains(n)).ToList();
        Assert.True(missing.Count == 0,
            "枚举成员未出现在任何转移的 emits（观察投影缺登记）: " + string.Join(", ", missing));
    }

    [Fact]
    public void Emits_AreAllRealRunDriveStatusMembers()
    {
        var members = Enum.GetNames<RunDriveStatus>().ToHashSet();
        var unknown = DeclaredEmits(LoadMachine()).Where(n => !members.Contains(n)).ToList();
        Assert.True(unknown.Count == 0,
            "emits 含非 RunDriveStatus 成员（第二状态权威破口）: " + string.Join(", ", unknown));
    }

    // ---- 牙②：转移合法性（表驱动 + 负向自证）----

    [Fact]
    public void DeclaredTransitions_AreInternallyLegal()
    {
        var machine = LoadMachine();
        var table = DeclaredTransitions(machine);
        var nodes = machine.GetProperty("machines").GetProperty("run")
            .GetProperty("nodes").EnumerateArray().Select(n => n.GetString()!).ToHashSet();
        foreach (var (from, to) in table)
        {
            Assert.True(nodes.Contains(from), $"未声明节点 {from}");
            Assert.True(nodes.Contains(to), $"未声明节点 {to}");
        }
        // 主干正例：声明链上关键步必须合法。
        Assert.True(IsLegal(table, "created", "admitted"));
        Assert.True(IsLegal(table, "admitted", "activated"));
        Assert.True(IsLegal(table, "activated", "observing"));
        Assert.True(IsLegal(table, "observing", "deciding"));
        Assert.True(IsLegal(table, "verifying", "terminal"));
        Assert.True(IsLegal(table, "terminal", "closed"));
    }

    [Fact]
    public void IllegalTransitions_AreRejected_NegativeSelfProof()
    {
        var table = DeclaredTransitions(LoadMachine());
        // 已知非法对（负向自证：若这些断言失败，说明表被污染或测试失效）：
        // closed 是吸收态——任何离开（含 closed→acting）均非法。
        Assert.False(IsLegal(table, "closed", "acting"));
        // admitted 未经 P24（admitted→activated）不得直接进入 cycle。
        Assert.False(IsLegal(table, "admitted", "acting"));
        Assert.False(IsLegal(table, "admitted", "deciding"));
        // 未激活不得观察；terminal 前不得直接 closed 之外的吸收转移。
        Assert.False(IsLegal(table, "created", "observing"));
        Assert.False(IsLegal(table, "closed", "closed"));
    }

    // ---- 牙③：payload type: record 反射存在性（D5 零漂移）----

    [Fact]
    public void RecordPayloadRefs_ResolveToRealTypes()
    {
        var edges = LoadMachine().GetProperty("edges").EnumerateArray().ToList();
        Assert.True(edges.Count > 0, "edges 登记为空（run 机转移引用无主）");
        foreach (var edge in edges)
        {
            var pref = edge.GetProperty("payloadRef");
            Assert.Equal("record", pref.GetProperty("type").GetString());
            var fullName = pref.GetProperty("name").GetString();
            var found = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } })
                .FirstOrDefault(t => t.FullName == fullName);
            Assert.True(found is not null,
                $"{edge.GetProperty("id").GetString()}: payload_ref {fullName} 无法反射解析");
        }
    }
}
