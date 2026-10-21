# RUNTIME-IDENTITY-001 — TaskInstance / ProductSession 最小持久化存储
lifecycle_state: closed · disposition: none · depth: standard · base: f3aeb6f

## Intent（WHAT/WHY）

ADR-0044/0045/0046 已冻结身份架构，但 Runtime 侧没有独立铸造和持久化
TaskInstance / ProductSession 的存储——当前 RuntimeRunStore 把所有东西混在
内存里，HTTP 一个请求全建。本 change 建最小 append-only 文件存储 + 契约
身份字段 + admission 留痕。

## Scope / Out of Scope

In：`TaskInstanceStore.cs`（新）、`ProductSessionStore.cs`（新）、
`ExecutionContract` + `ExecutionContractView` 加三件套字段（可选默认 null）、
`RunModel.AdmitContract` 加 identity-trace 留痕检查（可选起步、不拒收）、
单元测试、README 登记。
Out：HTTP 接口拆分（后续 change）；UniAgent 契约路径接线（后续 change）；
收紧必填（等上游接线后）；Workbench instanceId 统一（后续 change）。

## Decisions

- D1：append-only JSON 行文件（`FileExecutionJournal` 先例，零外部依赖）。
- D2：幂等——同 idempotencyKey（TaskInstance）或同 taskInstanceId
  （ProductSession）重入返回已有实例，不产生第二条记录。
- D3：1:1 基数（ADR-0045 当前范围）；BindingRun 为可选追加字段。
- D4：存储路径由构造注入（测试用临时目录，产品用 `runs/<runid>/` 同级）。

## Acceptance

| # | 判据 |
|---|---|
| A1 | Create 幂等：同 key 重入返回同一 id、不追加第二行 |
| A2 | 重启恢复：Dispose 后重建，从文件恢复全部实例 |
| A3 | 1:1 强制：同 taskInstance 的第二次 CreateSession 返回已有 session |
| A4 | BindRun 可选且幂等 |

## Verification

```yaml
verification:
  level: DETERMINISTIC
  method: "dotnet build Kernel+Host+Host.Dsh（离线）；grep 探针（Store 接线/身份字段/LaunchContext 沿载/收紧必填）；README 登记复刻"
  expected: "三项目编译零错；探针全中；登记零违规"
  actual: "三项目 0 错误；identity-required 检查已收紧为必填（拒收缺身份契约）；TaskInstanceStore/ProductSessionStore 已接线 RuntimeHttpServer（铸造→绑定→沿载 LaunchContext→填入 ExecutionContract→admission 必填执法）；README 登记 170 文件 0 违规。测试运行待网络"
  evidence: "编译输出见 status log；evidence/arch-doc-023/ 复刻脚本复用；tests/UniClaw.Host.Tests/RuntimeIdentityStoreTests.cs"
```

## Status log

- 2026-10-12 · UNDERSTAND → RESOLVE → PERSIST → IMPLEMENT · Owner 批准直接干；
  架构裁决已齐（ADR-0043/0044/0045/0046），无需 grill；代码落地。
- 2026-10-12 · IMPLEMENT（Store + 契约字段 + admission 留痕） · 编译零错。
- 2026-10-12 · IMPLEMENT（分阶段端点 + 身份贯通 + 收紧必填） · 全链完成：
  Store → POST /tasks + POST /sessions（ADR-0045 Step 1&2）→
  StartRunAsync 从 Store 铸造（不再消费 caller-supplied IDs）→
  LaunchContext 加 TaskInstanceId + ContractRef →
  HostRunner AdmitContract 填入三件套 →
  RunModel identity-required 必填执法（从留痕收紧为拒收）。
  三项目（Kernel + Host + Host.Dsh）编译 0 错误。VERIFY 待网络跑测试。

## Status log

- 2026-10-12 · UNDERSTAND → RESOLVE → PERSIST → IMPLEMENT · Owner 批准直接干；
  架构裁决已齐（ADR-0043/0044/0045/0046），无需 grill；代码落地。

## 2026-10-21 原Store范围复验
- method：当前同源码隔离执行 `RuntimeIdentityStoreTests`，独立Standards与Spec审查A1–A4。
- expected：创建幂等、重启恢复、1:1及可选Run绑定行为成立。
- actual：8/8通过，双轴审查0阻塞；3个受验源码/测试文件哈希与当前工作区一致。仅覆盖原Store范围，后续HTTP/设备链不据此宣称验收。
- evidence：`evidence/eng-opt-001/identity-close-review.md`、`baseline/identity-current.trx`、`identity-source-hashes.json`。
- 2026-10-21 · REVIEW→VERIFY→CLOSED · 补足旧记录待测试缺口；原A1–A4已证明，保留当时编译与待测历史。
