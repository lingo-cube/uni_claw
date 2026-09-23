# scenarios/ — 仿真场景库

> 独立的仿真基础设施。按职能定位（模拟/回放/验证），不依附于任何文档。
> 150 场景推理文档（ARCH-DOC-017）是来源之一——派生场景可选标注 srRef。

## 场景来源类型

| source           | 含义                     |
|------------------|--------------------------|
| recorded         | 真机/模拟器录制回放       |
| generated        | 参数化动态生成            |
| derived-from-doc | 从推理文档派生（带 srRef）|
| bug-repro        | 缺陷复现                 |
| component-test   | 组件级隔离测试            |

## 元数据字段（8 + 期望值 + 认证 + Agent realization）

见 `schema.json`（schema v2 重建归 SIM-002 S5）。status 由覆盖率工具从
TRX 真实执行派生（G3）。每个场景还带 Agent 侧拆分标注（G4 /
simulation-baseline v0.2 C7）：`agentDecisionRealization` 与
`goalEvaluationRealization`（legal: real|double；当前构成 hybrid——
decision=double，evaluation=真件；与实际构成的一致性由
`ScenarioRealizationAnnotationTests` 执法）。

## Golden 认证（SIM-002 G2）

每个场景的 `expectations` 经 `certification` 块钉扎（C8 golden 期望更新
协议的机械执法）：

| 字段 | 含义 |
|------|------|
| `expectationsDigest` | 期望值 canonical rendering 的 SHA-256（改期望必须重认证） |
| `runtimeSourceHash` | 认证时 Kernel+Agent **源码**状态哈希（源码而非 DLL：跨机可复现） |
| `certifiedByChange` | 致因 change 引用（必填——期望迁移搭乘致因 change） |
| `certifiedAt` | 认证日期 |

唯一写入口（test runtime 只 Verify、无写回路径）：

```bash
# 重认证（必须显式携带致因 change；无 --change 拒绝执行）
python3 tools/scenario_certify.py --change <致因change> --all

# 仅验证
python3 tools/scenario_certify.py --check
```

双执法：C# 验证器（`ScenarioCertificationTests`，与 python 双语言 canonical
实现互为一致性检查）+ 覆盖率工具（违规 exit 1）。Kernel/Agent 源变更同样
使认证失效——重认证经致因 change，期望值是否随动由该 change 评审裁决。

## 使用

```bash
# 能力覆盖率报告（SIM-002 G3：status 由 TRX 真实执行派生，非 JSON 自报）
python3 tools/scenario-coverage.py --run    # 跑测试 + 出报告（推荐）
python3 tools/scenario-coverage.py --trx <path>  # 用既有 TRX

# 无结果 / 结果不匹配 / schema 违规 / TRX 陈旧 / 认证违规 → exit 1
```

## 测试承载（SIM-002 G3）

每个场景条目必须有一个（或多个）承载测试，映射以
`[Trait("Scenario", "SCN-…")]` 钉在测试方法上——测试改名/删除，映射随
之消失，覆盖率工具报「无结果」exit 1；C# 侧由
`ScenarioCertificationTests` 双执法（含反向：trait 指向不存在的场景也
违规）。工具经 `dotnet test --list-tests --filter Scenario=<id>` 从二进制
发现映射（TRX 不携带 xUnit traits——vstest 局限），再与 TRX 的
FQN→outcome join 派生 status。
