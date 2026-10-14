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

    // ---- 牙③：payload ref 可解析性（D7 ② / D5 零漂移）----
    // type: record → C# 反射存在性；type: schema → schemas/workspace 文件
    // 存在且 $id 匹配（D7 归属执法：record 反射失败路径 / schema $id 不匹配
    // 路径都 RED）。逻辑收敛到纯函数 ValidatePayloadRefs，负向自证在内存中
    // 变异产物后复用同一路径，不改磁盘产物。

    private static List<string> ValidatePayloadRefs(JsonElement doc, string repoRoot)
    {
        var errors = new List<string>();
        var edges = doc.GetProperty("edges").EnumerateArray().ToList();
        Assert.True(edges.Count > 0, "edges 登记为空（转移引用无主）");
        foreach (var edge in edges)
        {
            var id = edge.GetProperty("id").GetString();
            var pref = edge.GetProperty("payloadRef");
            if (pref.ValueKind == JsonValueKind.Null)
                continue; // status: track 且尚无 realization 的边（payload_ref 缺席）
            var type = pref.GetProperty("type").GetString();
            var name = pref.GetProperty("name").GetString()!;
            if (type == "record")
            {
                var found = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } })
                    .FirstOrDefault(t => t.FullName == name);
                if (found is null)
                    errors.Add($"{id}: payload_ref {name} 无法反射解析");
            }
            else if (type == "schema")
            {
                var matched = Directory
                    .EnumerateFiles(Path.Combine(repoRoot, "schemas", "workspace"), "*.json")
                    .Any(f => File.ReadAllText(f).Contains($"\"$id\": \"{name}\""));
                if (!matched)
                    errors.Add($"{id}: payload_ref schema {name} 未命中任何 schemas/workspace $id（悬空 ref）");
            }
            else
            {
                errors.Add($"{id}: payloadRef type {type} 非法（record|schema）");
            }
        }
        return errors;
    }

    [Fact]
    public void PayloadRefs_ResolveToRealTypesOrSchemas()
    {
        var errors = ValidatePayloadRefs(LoadMachine(), RepoRoot());
        Assert.True(errors.Count == 0, string.Join("\n", errors));
    }

    [Fact]
    public void DanglingRecordRef_IsRejected_NegativeSelfProof()
    {
        // 悬空 ref RED（反射失败路径）：内存变异一条 record 引用后同一路径必须报错。
        var root = RepoRoot();
        var raw = File.ReadAllText(Path.Combine(
            root, "tests", "UniClaw.Kernel.Tests", "Protocol",
            "protocol-machine.generated.json"));
        Assert.Contains("UniClaw.Kernel.Effects.DispatchRequest", raw);
        var mutated = JsonDocument.Parse(
            raw.Replace("UniClaw.Kernel.Effects.DispatchRequest", "UniClaw.Kernel.Effects.NoSuchRecord"))
            .RootElement;
        var errors = ValidatePayloadRefs(mutated, root);
        Assert.Contains(errors, e => e.StartsWith("P14:") && e.Contains("无法反射解析"));
    }

    [Fact]
    public void DanglingSchemaRef_IsRejected_NegativeSelfProof()
    {
        // 悬空 ref RED（schema $id 不匹配路径）。
        var root = RepoRoot();
        var raw = File.ReadAllText(Path.Combine(
            root, "tests", "UniClaw.Kernel.Tests", "Protocol",
            "protocol-machine.generated.json"));
        Assert.Contains("https://uniclaw.dev/schemas/workspace/task-launch-request/v1", raw);
        var mutated = JsonDocument.Parse(
            raw.Replace("https://uniclaw.dev/schemas/workspace/task-launch-request/v1",
                        "https://uniclaw.dev/schemas/workspace/no-such-schema/v1"))
            .RootElement;
        var errors = ValidatePayloadRefs(mutated, root);
        Assert.Contains(errors, e => e.StartsWith("O1:") && e.Contains("悬空 ref"));
    }

    // ---- 牙④（Slice 2）：object 机形状 + gap 纪律 ----

    private static readonly string[] ObjectMachines =
    {
        "binding", "judgment", "attempt", "contract-view",
        "activation-record", "consultation-journal",
    };

    [Fact]
    public void ObjectMachines_AreDeclaredWithInitialAndTerminal()
    {
        var machines = LoadMachine().GetProperty("machines");
        foreach (var name in ObjectMachines)
        {
            Assert.True(machines.TryGetProperty(name, out var m), $"object 机 {name} 缺席");
            Assert.True(m.GetProperty("terminalNodes").GetArrayLength() > 0,
                $"{name}: terminal_nodes 必须声明（Slice 3 图审计输入）");
            var initial = m.GetProperty("initial").GetString()!;
            var nodes = m.GetProperty("nodes").EnumerateArray().Select(n => n.GetString()!).ToHashSet();
            Assert.Contains(initial, nodes);
            var table = DeclaredTransitionsFor(m);
            foreach (var absorbing in m.GetProperty("absorbing").EnumerateArray())
                Assert.DoesNotContain(absorbing.GetString()!, table.Select(t => t.From).ToList());
        }
    }

    [Fact]
    public void ObjectMachine_CodeFacts_ArePinned()
    {
        var machines = LoadMachine().GetProperty("machines");
        // binding：rejected 出口 + derived-invalid（D8 派生失效）都在状态集。
        var binding = machines.GetProperty("binding").GetProperty("nodes")
            .EnumerateArray().Select(n => n.GetString()!).ToHashSet();
        Assert.Equal(new HashSet<string> { "candidate", "canonical", "consumed", "derived-invalid", "rejected" }, binding);
        // attempt：UnknownOutcome 出口（DSE-001 三态 outcome 实证）。
        var attempt = machines.GetProperty("attempt").GetProperty("nodes")
            .EnumerateArray().Select(n => n.GetString()!).ToHashSet();
        Assert.Contains("unknown-outcome", attempt);
        // contract-view：P24 已消费 admitted，不再将其作为悬挂白名单。
        var cv = machines.GetProperty("contract-view");
        var cvNodes = cv.GetProperty("nodes").EnumerateArray().Select(n => n.GetString()!).ToHashSet();
        Assert.Contains("frozen", cvNodes);
        var whitelist = cv.GetProperty("hangWhitelist").EnumerateArray().Select(n => n.GetString()!).ToList();
        Assert.Empty(whitelist);
        Assert.Contains(("admitted", "activated"), DeclaredTransitionsFor(cv));

        var activation = machines.GetProperty("activation-record");
        Assert.Equal(new HashSet<string> { "reserved", "finalized", "failed", "blocked" },
            activation.GetProperty("nodes").EnumerateArray().Select(n => n.GetString()!).ToHashSet());
        Assert.Contains(("reserved", "finalized"), DeclaredTransitionsFor(activation));
        Assert.Contains(("reserved", "failed"), DeclaredTransitionsFor(activation));
        Assert.Contains(("reserved", "blocked"), DeclaredTransitionsFor(activation));

        var journal = machines.GetProperty("consultation-journal");
        Assert.Equal(new HashSet<string> { "pending", "completed", "rejected" },
            journal.GetProperty("nodes").EnumerateArray().Select(n => n.GetString()!).ToHashSet());
        Assert.Contains(("pending", "completed"), DeclaredTransitionsFor(journal));
        Assert.Contains(("pending", "rejected"), DeclaredTransitionsFor(journal));
    }

    [Fact]
    public void GapEdges_NeverEnterTransitionLegality()
    {
        var machine = LoadMachine();
        var gapIds = machine.GetProperty("edges").EnumerateArray()
            .Where(e => e.GetProperty("kind").GetString() == "gap")
            .Select(e => e.GetProperty("id").GetString()!).ToHashSet();
        Assert.True(gapIds.Count > 0, "G 系缺边登记缺席");
        foreach (var m in machine.GetProperty("machines").EnumerateObject())
            foreach (var t in m.Value.GetProperty("transitions").EnumerateArray())
            {
                var edge = t.GetProperty("trigger").GetProperty("edge").GetString();
                Assert.True(edge is null || !gapIds.Contains(edge),
                    $"{m.Name}.{t.GetProperty("id").GetString()}: gap 边 {edge} 不得进入转移合法性（D11）");
            }
    }

    private static List<(string From, string To)> DeclaredTransitionsFor(JsonElement machine)
        => machine.GetProperty("transitions").EnumerateArray()
            .Select(t => (t.GetProperty("from").GetString()!, t.GetProperty("to").GetString()!))
            .ToList();
}
