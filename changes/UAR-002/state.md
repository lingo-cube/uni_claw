# UAR-002 — UniAgent Dual-Realization Baseline Adoption

lifecycle_state: closed · disposition: none · depth: decision-heavy · base: c973b3e074841fb1ce76616e44dd2f130759f71b

## Intent（WHAT/WHY）

**WHAT**：把 UAR-001 已通过 Human/SOL Review 的稳定决策提炼为冻结的
UniAgent Realization 产品架构基线：Codex-backed 与 DSH-backed 都是完整
UniAgent Realization，前者承担 Simulation Realization，后者承担 Product
Realization；两者共享 Host-neutral Conformance Surface，并严格隔离 Host state、
Development Harness 与 Product Authority。

**WHY**：UAR-001 已关闭设计评审，但原文仍是 `Authority: NONE` 且驻
`docs/analysis/`，不能作为 R1 或后续 realization change 的架构输入。当前
`docs/README.md` 五层分类学进一步规定：前瞻候选属于 `design/`，稳定架构只有
进入 `architecture/` 才具有规范效力。因此需要一次独立、文档限定的 adoption
change，而不是在已关闭的 UAR-001 中原地升格。

## Scope

- 新增 `docs/architecture/uniagent-realization-baseline-v0.1.md`，只冻结稳定的
  realization contract、角色分工、identity/lifecycle 隔离与 conformance invariants
- 新增 ADR-0022，记录“双完整 realization + Codex Simulation / DSH Product”
  的不可逆取舍及被拒替代项
- 将 UAR-001 长文从 `docs/analysis/` 迁入 `docs/design/`，标记为被冻结基线
  supersede 的非权威设计历史，并同步 `docs/design/README.md`
- 在 `CONTEXT.md` 增补已定型的 Realization、Host Session 与 Conformance Surface
  术语；保持一至两句 WHAT-only 定义
- 只更新因迁移产生的直接文档引用与 adoption metadata

## Out of Scope

- R1 Contract Closure，或创建任何 R1 Change、Plan、WorkItem
- Codex SDK/App Server 组合、DSH Profile/plugin/package 切分或具体 Adapter 设计
- Product Session persistence、identity algorithm、Goal revision、multi-run、
  continuation、deployment、tenant、SLA 或运维设计
- Deferred ⑰ legal Primary Run activation 的 Producer/Consumer、载荷、状态转移、
  重复/并发、失败或恢复协议
- lifecycle command vocabulary、progress/event surface、transport/serialization
- 产品代码、测试行为、Harness 机制、model routing 或第三方依赖变更
- 把候选中的阶段级 Roadmap Skeleton 升格为实施路线图

## Decisions

| # | 决策 | 状态 |
|---|---|---|
| D1 | Codex-backed 与 DSH-backed 都是完整 UniAgent Realization，不是 reasoning/model Adapter 或普通 AI Coding workflow | Human authorized 2026-09-12 |
| D2 | Codex-backed UniAgent = Simulation Realization；DSH-backed UniAgent = Product Realization | Human authorized 2026-09-12 |
| D3 | realization external seam = Host-neutral UniAgent Conformance Surface；Host 专有 session/tool/event/transport 留在 realization 内部 | adopted |
| D4 | Host Session 只能承载并显式关联 Product Session，不得成为 Product Session、Primary Goal、Primary Run 或任何 Product Authority | adopted |
| D5 | Product Runtime 与 Development Harness 即使复用同一 Host 可执行体，也必须拥有不同 state、instruction/tool set、lifecycle 与 completion semantics | adopted |
| D6 | ADR-0019 继续独立锁定 post-activation Kernel self-drive；P1 admission 不触发 activation，Deferred ⑰ 保持未决 | inherited, not reopened |
| D7 | 采用“精炼稳定基线 + 保留设计历史”，拒绝把整份 UAR-001 长文原样冻结 | fixed for adoption |

## Assumptions

- AGENTS.md 指向的 Target Product Architecture 与 Inter-Component Protocol
  Baseline 继续是上游权威；本基线只在 realization 维度细化，不覆盖其
  Owner/Authority/Lifecycle/Boundary。
- 当前 cardinality 仍为 `1 Product Session / 1 Primary Goal / 1 Primary Run`。
- UAR-001 的 SOL Standards/Spec re-review 与 CONTRACT verification 证据有效；
  UAR-002 仍需对提炼结果重新 Review/Verify，不能直接继承 PASS。
- `docs/README.md` 的五层分类学与 DocsMetadataTests 是当前文档治理权威。

## Alternatives

1. **继续让候选驻 analysis 并把它当事实使用**：拒绝；`analysis/` 永非规范，
   会形成隐式 authority。
2. **把 UAR-001 长文整体移动到 architecture 并改 FROZEN**：拒绝；长文混有
   上游能力证据、内部候选形态、阶段 skeleton 与 deferred 细节，会冻结过多。
3. **只写 ADR，不建立 baseline**：拒绝；ADR 解释 WHY，但不能替代调用者需要的
   realization Interface、invariants、failure/identity rules 与 conformance surface。
4. **提炼冻结基线并保留 design history**：接受；小 Interface 隐藏 Host 差异，
   同时保留推理证据且避免双 authority。

## Owner-Authority impact

- 不新增 Product Owner 或 Authority class。
- UniAgent 继续独占 Primary Goal、Execution Contract authoring 与 Goal Evaluation。
- Run Model、Evidence Ledger、World Model、Control Loop、Assurance、Effect Boundary
  与 Uni Kernel 保持既有 sole Owner/Authority。
- Codex/DSH Host、Host Session、transcript、event log、tool loop、模型与 Adapter
  均不得取得 Product Authority。
- 本基线只拥有 UniAgent realization contract 的架构权威；冲突时服从 Target
  Product Architecture、Inter-Component Protocol 与既有 ADR。

## ADR refs

- ADR-0019：Primary Run 合法激活后由 Uni Kernel self-drive；P1 不负责 activation。
- ADR-0022：双完整 UniAgent Realization 与 Codex/DSH 角色分工。

## Acceptance

1. 冻结基线明确 Codex/DSH 都是完整 UniAgent Realization，而非内部认知 Adapter。
2. 冻结基线明确 Codex = Simulation、DSH = Product，且不宣称任一已 production-ready。
3. 基线保持上游 Owner/Authority/Cardinality/Effect Boundary 与 terminal invariants。
4. Host Session/Product Session、Host Goal/Primary Goal、Host turn/Primary Run 明确非同义。
5. Development Harness 与 Product Runtime 的 state/tool/lifecycle/completion 明确隔离。
6. Conformance Surface 只比较 canonical records、lifecycle、failure 与 evidence，
   不比较 transcript、tool sequence、Host id、token 或 step 数。
7. ADR-0022 记录真实替代项与角色分工理由；ADR-0019 不被重开或复制。
8. 原 UAR-001 长文进入 `docs/design/` 并明确非权威、被新基线 supersede；索引同步。
9. CONTEXT 新词符合 WHAT-only、一至两句规则，无实现和 roadmap 细节。
10. 无 R1、实现、测试行为、Harness 或 model-routing 变更；并行 FSV 工作树不受影响。

## Constraints

- 只修改本 state、UAR 设计文档及索引、新 baseline、新 ADR、CONTEXT 术语。
- 不修改 `src/`、`tests/`、`platforms/`、`plans/`、`workitems/`、schemas 或 routing。
- 保留并行 FSV-001/感知文件及 `show-me-perception-arch-diff.html`，不修改、不回退、
  不纳入验证结论。
- 文件迁移与内容修改使用精确路径；不执行 destructive git 操作。

## Residual risks

- R1 尚未授权，Session correlation、Goal→Contract authoring 与 legal activation
  protocol 仍未闭合。
- Codex 非 Coding Product scenario 的稳定性和 DSH production hardening 均需未来
  Tracer Bullet/Environment evidence，不能由本次文档冻结证明。
- 具体 Host SDK、Profile、plugin、persistence 与 transport 仍是 realization 选择。

## Review（2026-09-12）

Result: `PASS`

- Standards 首审与 Spec 首审共同发现一项 authority duplication：冻结基线曾逐项
  复述 ADR-0019 / Protocol 已拥有的 P1 non-activation、Kernel self-drive 与
  terminal 规则。
- 修复把基线 §7 收缩为单向继承约束：ADR-0019 与 Protocol 是该语义的唯一
  authority；realization 只须遵守，不复制、不扩张或重新解释。
- Standards/Spec 双轴复审均为 `PASS`；High/Medium/Low findings = `0/0/0`，
  Acceptance 1–10 全部通过。
- 最终通读将 ADR-0022 接受项中的误称“两个真实 Adapter”修正为“两个真实
  realization”；极窄 Standards/Spec 复审再次 PASS，Adapter 只保留为 realization
  内部 seam 术语。
- Review 与修复均限定在 UAR-002 文档范围；未把 Deferred ⑰、Host 选型、DSH
  内部切分或路线图带入冻结决策。

## Verification

```yaml
verification:
  level: CONTRACT
  method: >
    对照 UAR-001 Acceptance/Review 与上游 Product/Protocol/ADR；执行
    DocsMetadataTests、路径限定 diff/stale-link/metadata 检查和 Standards/Spec
    双轴 Review；复核 git status 未触及并行 FSV 路径
  expected: >
    Acceptance 1-10 全满足；新 architecture baseline 与 ADR 成为唯一稳定权威，
    design history 保持 Authority NONE；无 R1/代码/Harness 变更
  actual: >
    2026-09-12 DocsMetadataTests 4/4 通过；path-scoped whitespace、local-link、
    active-doc stale analysis path、metadata 与变更范围检查通过；Standards/Spec 双轴复审
    均为 PASS，Acceptance 1-10 全满足；未创建 R1 资产，未修改产品代码、
    Harness 机制或并行 FSV 路径
  evidence: >
    dotnet test UniClaw.Kernel.Tests --filter FullyQualifiedName~DocsMetadataTests
    （4/4）；git diff --check；git diff --no-index --check；local Markdown link
    existence checker；rg active-doc stale path/metadata（UAR-001 state 历史记载豁免）；UAR-002 Standards/Spec re-review
    reports；git status --short 的精确路径复核
```

## Status log

- 2026-09-12 · enter→understanding · 复核 UAR-001 CLOSED、ARCH-DOC-015 五层分类学、当前 HEAD/dirty ownership 与 ADR 编号
- 2026-09-12 · understanding→resolved→persisted · Human 授权仅锁稳定双实现决策；冻结面、deferred 面与文档迁移策略明确，不进入 R1/实现
- 2026-09-12 · persisted→resolving · Standards/Spec 首审发现基线 §7 复制 ADR-0019 / Protocol authority；收缩为单向继承约束
- 2026-09-12 · resolving→reviewed · Standards/Spec 双轴复审 PASS；High/Medium/Low = 0/0/0，Acceptance 1-10 全通过
- 2026-09-12 · reviewed→verified→closed · CONTRACT checks 通过；稳定双实现决策完成升格，R1 与实现保持未授权、未启动
