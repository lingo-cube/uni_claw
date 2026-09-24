# SIM-003 — Executable Expectation Binding

lifecycle_state: closed · disposition: completed · depth: decision-heavy · base: 7a801e67
implementation_commit: fc571f42

## 最终事实（闭包依据）

- **JSON 是唯一 expectation authoring truth**：`GoldenScenarioBundles.cs` 中
  `new ScenarioExpectation(` = 0 处；全部 8 处 `Expected = ScenarioExpectations.Load(...)` 投影。
  改期望的唯一路径 = 改 `scenarios/*.json`，而 JSON 被 C8 认证钉住。
- **executable Expected 只能经 certified projection 产生**：
  `ScenarioExpectations.Load`（fail-closed：schemaVersion==2、expectationsDigest
  自洽、executionDigest 自洽、enum 可解析）→ 工厂内最后一步赋值 → Seal。
  `ScenarioLibrary.Load` 无投影后变异路径；外部 `with` 变异均产 test-local
  变体（本地 ScenarioId），tripwire 兜底。
- **certification v2 trust chain** = `expectationsDigest`（值）+
  `executionDigest`（kind/carrier/options）+ `runtimeSourceHash`
  （Kernel/Agent 源，CRLF→LF 归一化，跨 checkout 可复现）；Python/C# canonical
  逐字节一致（python 盖章 18/18 与 C# 复算全等；M5 `--print-source-hash` 对拍同值）。
- **绑定范围**：golden-bundle bound = **10**；PERC `kind:none` = **8**（保证之外）。
- **期望迁移**：WIFI-003 status→Completed + version 2；BARRIER-002
  goalSatisfaction→Unsatisfied（version 2 为 RUN-004 既有）；其余 16 条未 bump。

## Verification

```yaml
verification:
  level: SCENARIO
  method: dotnet test UniClaw.Kernel.slnx + python tools/scenario_certify.py --check + python -X utf8 tools/scenario-coverage.py --run
  expected: 绑定链全绿；认证/覆盖全过；除既有平台失败外零回归
  actual: |
    tripwire + M1–M7 全绿（ExecutableExpectationBindingTests）
    scenario_certify: 18/18 PASS（v2，0 violations）
    scenario coverage: 18/18 passing，truth chain verified（certified by SIM-003）
    Agent 17/17 · Host 18/18 · Simulation 162/162
    Core 14/14 · FileSystemRealization 9/9
    Kernel 475/480 —— 非全绿：4× VisionServiceHostTests（Windows 平台失败）
    + 1× Tcp_ConnectionRefused（并行时序 flake，隔离复跑通过，2s）
  evidence: |
    implementation_commit fc571f42（32 files，+1270/−235）
    TRX tests/UniClaw.Simulation.Tests/TestResults/coverage.trx（--run 新鲜真值）
    首轮 coverage 曾误抓陈旧 sim003.trx（按文件名序取最新），删除后复跑 exit 0
```

### M1–M7 摘要（全绿）

M1 期望篡改不重认证→投影拒绝+认证违规；M2 合法重认证→期望自动流入零 C# 改动；
M3 投影后手改→tripwire digest 失配；M4 源码哈希过期→认证失败；
M5 LF/CRLF 同哈希+跨语言同值；M6 仅改测试源→认证不可见；
M7 carrier/options 篡改不重认证→executionDigest 失配（carrier 与 options 双臂）。

## 环境证据缺口（如实记录，不在 SIM-003 内修复）

- 4× `VisionServiceHostTests`：Process.Start 执行生成的 `.sh` → Windows 上
  "not a valid application for this OS platform"，机制性不可通过。
  **当前无 Linux/WSL/CI 证据**（本机无 WSL 发行版；仓库无 CI workflow）；
  隔离判定依据 = 失败机制 + SIM-003 diff 未触及任何 Kernel.Tests 文件。
- 1× `Tcp_ConnectionRefused_InfrastructureFailure`：全套并行下 7s 超时失败，
  单独复跑通过——时序 flake，未修改。
- `DevLoopRunner.cs` 一行 `using`（journal 处置）：Windows 上 `Directory.Delete`
  因句柄占用失败的既有缺陷的最小修复；必要性（修复前 DevLoop 4/5 失败）与
  行为中性（journalBytes 在 dispose 前读取）均已证明；代码注释留痕。

## Review Gate（2026-09-24，全项通过）

```text
unexpected diff                = NO   （32 文件全部因果映射 G1–G10 + 2 数据修正 + DevLoopRunner 裁决保留）
expectation second source      = NO   （构造 3 处：1 投影 + 2 test-local fixture；变异 6 处全归类，无第四类）
post-projection mutation path  = NO   （8 工厂 certified-最后投影；generated carrier Build 后重投影）
certification v2 sound         = YES  （值/执行摘要分离；跨语言逐字节一致；v1 fail-closed；M7 双臂）
workspace clean                = YES  （*.bak 移出仓库至 D:\space-x\uni_claw-leftover\；无 ignore 规则）
platform failures isolated     = YES  （4 Windows 平台 + 1 时序 flake，均未修改）
```

## Constraints

- 未重新 certification；closure 不修改 fc571f42 内容（产品代码/测试逻辑/
  scenario 数据/认证零改动）。
- C8 搭乘：全库重认证 `--change SIM-003`（certifiedByChange=SIM-003: 18）。

## Status log

- 2026-09-24 · understand→resolved · Step 0 审计：G2 gap verdict B（双真源确认）
- 2026-09-24 · resolved→planned · Step 1 设计：`plans/2026-09-24-sim-003-*.md`（commit 6cc0f712，READY_TO_IMPLEMENT）
- 2026-09-24 · planned→implemented · G1–G10 + 全库 v2 认证迁移（commit fc571f42）
- 2026-09-24 · implemented→reviewed · Review Gate 10 项全过（六判定全 YES/NO 达标）
- 2026-09-24 · reviewed→verified · 全套复跑（certify 18/18 PASS；coverage 18/18 passing；Kernel 475/480 含 4 平台失败 + 1 flake，如实记录）
- 2026-09-24 · verified→closed · 本 closure commit（仅生命周期文档）
