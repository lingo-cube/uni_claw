# SIM-003 — Executable Expectation Binding（spec）

> 设计输入：`plans/2026-09-24-sim-003-executable-expectation-binding.md`（Step 1，
> verdict READY_TO_IMPLEMENT）；前置审计：SIM-003 Step 0（G2 gap，verdict B）。

## Root Cause

`scenarios/*.json expectations`（有 C8 认证、无消费方）与
`GoldenScenarioBundles.cs bundle.Expected`（有消费方、无认证）是两个无派生/
无比对关系的独立真源——`Integrity ✓ / JSON Certification ✓ / Executable
Expectation Binding ✗`。改 C# 期望可绕过认证；测试里的值级断言构成第三份真源。

## Scope（G1–G10）

- **G1** `runtimeSourceHash` 跨 checkout 可复现：Python/C# 两侧对文本源统一
  CRLF→LF 后哈希（correctness mechanism）；`.gitattributes` eol 钉扎仅作
  repository hygiene 第二道防线。
- **G2** certification schema v2：`expectationsDigest`（测什么结果）与
  `executionDigest`（用什么 executable carrier 测，cert-exec-v1：
  kind/carrier/options）职责分离；Python/C# canonical 逐字节一致；
  schemaVersion 1 fail-closed 废弃。
- **G3** 场景库 `execution` 绑定块（kind=golden-bundle|none；carrier；
  options），schema `additionalProperties:false` 执法；18 条全量迁移。
- **G4** `SCN-WIFI-003` 期望数据修正：`status: AlreadyTerminal → Completed`
  （幂等探针事实回归 Type-B 断言），`version: 1 → 2`。
- **G5** `ScenarioExpectations.Load` 唯一投影入口（fail-closed：v2 校验、
  双 digest 校验、enum 可解析校验；复用 `ScenarioCertification`，不造第三套 DTO）。
- **G6** `ScenarioLibrary` carrier 解析器：certified JSON 驱动构造，
  `Expected = certified projection` 永远是 bundle 构造的最后一步。
- **G7** `GoldenScenarioBundles` 删除全部独立 Expected 字面量（8 处投影、
  0 字面量），+2 注册 carrier（wifi-two-step-contradictory-post、
  wifi-off-to-on-generated）。
- **G8** Type-A（六字段复述断言）清除，由 `AcceptancePassed` +
  `DescribeAcceptance` 承载；Type-B（期望模型外维度：协议格式/顺序/身份/
  Reason/host 侧事实）保留并逐处注明理由。
- **G9** `ExecutableExpectationBindingTests` tripwire：executable digest ==
  certified digest、carrier/options 绑定、死 carrier 检查、kind=none 拒绝解析。
- **G10** M1–M7 mutation 机械证明。

## Out of Scope（非目标）

PER-009 closure · AGT-001 · Phase B 新能力 · simulation/product baseline 修改 ·
PERC harness 重构 · 无关代码清理 · ScenarioRunner acceptance 语义改动 ·
以"把 tests/ 全部加入 runtimeSourceHash"掩盖双真源 · 保留 C# golden literal
再靠比对维持同步。

## Executable Binding Guarantee 边界（必须随闭包携带）

**保证仅覆盖 `execution.kind = golden-bundle`（10 条：WIFI-001..006、
SMOKE-001、BARRIER-001..003）。`kind = none`（PERC-001..008，8 条）显式在
executable expectation projection 保证之外**——其认证保护的是文档化 golden
记录与 G3 测试状态真值链，非可执行期望值。禁止宣称 18/18 executable
expectation binding。

## 数据修正记录（随 SIM-003 因果章）

- `SCN-WIFI-003`：status AlreadyTerminal→Completed + version 1→2（授权项）。
- `SCN-BARRIER-002`：goalSatisfaction null→Unsatisfied（同类认证记录错误，
  实测 fail 消息 `goal=Unsatisfied (expected none)` 坐实；version 2 为
  RUN-004 既有，本次未再 bump）。
- 其余 16 条 version 未因认证算法迁移而 bump（经 `HEAD~1` 对照确认）。
