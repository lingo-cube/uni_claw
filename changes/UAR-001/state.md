# UAR-001 — UniAgent Dual-Realization Architecture Candidate

lifecycle_state: closed · disposition: none · depth: decision-heavy · base: aa0a5a4b9df23917836204002100afdd131d0283

## Intent（WHAT/WHY）

**WHAT**：形成一份 `Authority: NONE` 的 UniAgent 双实现架构候选，明确
Codex-backed UniAgent 与 DSH-backed UniAgent 都是完整的 UniAgent
Realization，而不是 UniAgent 内部的认知 Adapter；前者定位为测试/模拟实现，
后者定位为目标产品实现。候选必须保持现有 Target Product Architecture 的
Owner、Authority、Lifecycle、Boundary 与 Invariants，并把 Host 专有语义限制在
各自 realization 内部。

**WHY**：可行性已经确认，但公共 realization contract、Host Session 与
Product Session 的关系，以及 Contract 被接受后由谁驱动 Kernel 操作面最初尚未
闭合。Human 已在本 change 中接受 Kernel self-driven；其余 contract gaps 仍使
详细路线图或 Adapter 实现建立在未经验证的 Host 假设上。

## Scope

- 新建 `docs/analysis/uniagent-dual-realization-architecture-v0.1.md`
- 定义 UniAgent Realization、Codex-backed UniAgent、DSH-backed UniAgent、
  Host Runtime、Host Session 与 Conformance Surface 的候选语义
- 给出双实现逻辑形态、状态/身份映射原则、失败边界与共同 Conformance Model
- 明确 Development Harness Context 与 Product Runtime Context 的隔离
- 将 Kernel 驱动模式收敛为一个首要 SOL/Human Decision
- 记录 Human 对 Kernel self-driven 的接受；新增 ADR-0019，同步 Protocol
  Deferred ①、补列 activation Deferred ⑰，并写入 `CONTEXT.md` canonical term
- 仅保留阶段级 Roadmap Skeleton，不产生实施计划

## Out of Scope

- 修改 Product Architecture L0–L3，或修改 ADR-0019 直接相关的
  P1/P18/Deferred/Scenario 收口以外的既有协议语义
- 除 Kernel self-driven 决策外，创建其他 ADR 或 canonical glossary 词条
- Codex/DSH Adapter、DSH Product Profile、Kernel Bridge 或产品代码实现
- Tracer Bullet、测试代码、WorkItem、详细路线图或交付日期
- 多 Primary Goal、多 Primary Run、terminal continuation、跨 Session identity
- 决定部署拓扑、物理存储、模型、Provider、具体类型或 namespace

## Decisions

| # | 当前决定 | 状态 |
|---|---|---|
| D1 | 产物驻留 `docs/analysis/`，声明 `CANDIDATE / NOT_AUTHORIZED` 与 `Authority: NONE` | fixed for this change |
| D2 | Codex/DSH 是两个完整 UniAgent Realization；Adapter 只描述 realization 内部 Host seam | candidate for review |
| D3 | Codex-backed UniAgent = Simulation Realization；DSH-backed UniAgent = Product Realization | candidate for review |
| D4 | Host Session 只能承载/关联 Product Session，不得因物理持久化位置取得产品语义 Authority | derived from current baseline; candidate wording |
| D5 | 先做设计与 Review，再做双实现 Tracer Bullet；详细路线图必须等待验证证据 | process boundary |
| D6 | accepted Contract View 是合法激活的必要前置条件；Primary Run 合法激活后，由 Uni Kernel internal run driver self-drive；P1 不负责 activation | Human accepted 2026-09-11；ADR-0019 |
| D7 | cancel/pause/resume/escalation 若出现 buyer，必须另立有界 lifecycle protocol，不得成为 step driver | ADR-0019；payload deferred ⑯ |

## Assumptions

- 当前 Target v0.1 cardinality 维持 `1 Product Session / 1 Primary Goal / 1 Primary Run`。
- Codex SDK/App Server 足以承载模拟所需的 thread、resume、event、approval 与结构化输出能力，但 Codex 的 coding-oriented semantics 需要被隔离。
- DSH 的 Profile/Bundle、可替换 Agent Loop、Session Event 与 capability seam 足以承载产品化探索，但其 Developer Preview 状态意味着 production readiness 尚未被证明。
- 两个 realization 的可比对象是 canonical Product records 与 lifecycle transitions，不是 transcript、token 使用量或内部步骤。

## Alternatives

1. **先做详细路线图**：拒绝；Session/activation contract 尚未裁决，路线图会建立在脆弱假设上。
2. **把 Codex/DSH 当成 UniAgent 内部认知 Adapter**：拒绝；与用户目标冲突，也会把完整 lifecycle 压缩成 provider 调用。
3. **只实现 DSH，不建设 Codex realization**：暂不采用；会失去第二实现带来的真实 seam 与差分验证价值。
4. **在 Human Decision 前写 ADR 冻结 Kernel driver**：拒绝；ADR 只能记录真实
   取舍结果。Human 接受后再创建 ADR-0019；双实现其余候选仍不提前冻结。

## Owner-Authority impact

- 不改变现有 Product Owner/Authority。
- UniAgent 继续独占 Primary Goal、Execution Contract authoring 与 Goal Evaluation。
- Uni Kernel 及其 L2 Owners 继续独占 Run、Evidence、Belief、Control、Assurance、Binding 与 Effect 相关 Authority。
- Codex Thread、Codex Goal、DSH Session、DSH Goal 或其 event log 均不得自动升级为 canonical Product state。
- 物理存储可位于 realization 内部，但语义写入路径必须仍由对应 canonical Owner 控制。

## ADR refs

- [ADR-0019](../../docs/adr/0019-primary-run-is-self-driven-by-uni-kernel.md)：
  Human 已接受 Kernel self-driven Primary Run；已同步 Protocol Deferred ①、
  activation Deferred ⑰与 `CONTEXT.md`。
- Session/activation semantic authority、双实现兼容等级等其余候选仍未满足
  决策 Gate，不提前创建 ADR。

## Acceptance

1. 文档明确 Codex/DSH 是完整 UniAgent Realization，而不是普通 AI Coding workflow 或认知 Adapter。
2. Codex 与 DSH 分别被限定为 Simulation Realization 与 Product Realization。
3. 文档不重新打开 Target Product Architecture L0–L3，不复制或改写已有 Owner/Authority。
4. Host Session 与 Product Session、Host Goal 与 Primary Goal 明确非同一概念。
5. Development Harness Context 与 Product Runtime Context 有显式隔离规则。
6. 外部 UniAgent seam 保持小而深；Host 专有 session/tool/transport/event 语义不进入共享产品契约。
7. Codex 与 DSH 的能力、限制及版本成熟度有可核验的一方资料依据。
8. Conformance 比较 canonical Product records/lifecycle，而不比较 transcript。
9. 记录单一 Human Decision H1 的接受结果、ADR、Protocol/CONTEXT 同步与被拒替代项。
10. 除记录已接受 H1 的 ADR-0019 外，不创建实现计划、WorkItem、产品代码或详细路线图。

## Constraints

- 遵守 `docs/README.md`：未冻结候选只允许驻留 `docs/analysis/`。
- 遵守 `AGENTS.md`：Host 专有语义不得进入共享 Harness 或产品层；Product Runtime 与 Development Harness 结构隔离。
- 保持现有未跟踪文件 `docs/analysis/uni-agent-perception-implementation-analysis.md` 与 `show-me-perception-arch-diff.html` 不变。
- 执行期间并行出现的 Kernel/测试工作树修改不属于 UAR-001；不读取为本决策
  依据、不修改、不回退，验证采用文档路径限定。
- DSH 本机源码为 dirty checkout，只使用已提交 `b150a551...` 内容作为上游证据，不把本机未提交修改当作事实来源。

## Residual risks

- Kernel self-driven 已由 Human 接受且 SOL re-review 通过；activation 与公共
  lifecycle interface 仍只定义到语义级，R1 未获 Human authorization。
- Product Session 的物理持久化 Owner、恢复协议和 identity algorithm 未决定。
- Codex 的 coding-oriented built-in behavior 能否稳定收敛到非 Coding Product scenario，需要 Tracer Bullet 证明。
- DSH 仍处 Developer Preview；兼容、隔离、审计、升级与生产运维能力需要独立 hardening evidence。
- 当前 C# Product Runtime 与 TypeScript/Python Host 之间的 transport、取消和错误语义未设计。

## SOL Review Findings（2026-09-11）

Result: `CHANGES_REQUIRED`

### Standards axis

1. **High — P1 seam/Authority 不完整**：Protocol P1 把 Kernel activation 塞入
   Contract Proposal，却没有独立定义 Run Model→Kernel 的 Producer、Consumer、
   validity 与 failure；候选图中的 `Run Model / Uni Kernel` 也弱化了 P1 sole
   admission。
2. **Medium — CONTEXT glossary 越界**：`Kernel Self-Driven Primary Run` 用十行
   描述顺序、Authority 和未来 command，违反 glossary 只定义 WHAT、每词一至两句
   的规则。
3. **Low — 状态陈旧**：候选仍出现“不创建 ADR”“Kernel driver 尚未裁决”与
   “Review 前不得写回 glossary”，与 H1/ADR-0019/CONTEXT 现状冲突。

### Spec axis

1. **High — Host retry 可变相取得 lifecycle/driver Authority**：P1 同 version
   重复 admission 幂等复用，但 Driver Relation 又允许启动/恢复且把 activation
   留给 realization；当前文本没有封死重复 activation 或第二 Effect 风险。
2. **Medium — Scope creep**：Protocol P2 新增 post-action observation sequence
   coordination 归属，超出 state 中 H1 对 P1/P18/Deferred/Scenario 的窄收口。
3. **Low — 同步声明不成立**：候选陈旧表述使 Verification 中“同步一致”的 actual
   不能成立。

### Required resolution before re-review

- 将 P1 保持为纯 Contract Proposal/admission；另行表达 admission 后的单次、幂等
  run activation，明确唯一 Producer/Consumer、identity、重复、failure 与恢复语义。
- 撤回本 change 对 P2 sequencing ownership 的扩张。
- 把 CONTEXT 词条压缩为 implementation-free 的一至两句定义。
- 清理候选与 state 中全部 H1 前状态表述，再重新跑 Standards/Spec 双轴 Review。

## Review Repair（2026-09-12）

Status: `APPLIED / SOL_REREVIEW_PASS`

- P1 已恢复为纯 proposal/admission；同 version 重复 admission 只返回同一
  Contract View，零 activation、零 Run progression、零 Effect 副作用。
- accepted Contract View → legal Primary Run activation 已独立列为 Deferred
  ⑰；R1 必须闭合唯一 Producer/Consumer、identity/correlation、重复/并发、失败
  与恢复语义，并保证当前 cardinality 下 at-most-one Primary Run；不得新增
  Authority class，canonical Run State 仍只经 Run Model typed legal transition
  更新。
- ADR-0019/H1 已限定为 post-activation driver 决策，不再暗示 P1 admission
  触发 activation。
- P2 sequencing ownership 扩张已撤回；P2 只锁 producer 与
  ingress/admission。
- `CONTEXT.md` 词条已压缩，候选中的 H1 前陈旧状态已清理。
- 保留首次 `CHANGES_REQUIRED` 作为 review history；修复未授权 R1、详细路线图、
  WorkItem 或产品实现。

## SOL Re-review（2026-09-12）

Result: `PASS`

- Standards axis：`PASS`；High/Medium/Low findings = `0/0/0`。
- Spec axis：`PASS`；High/Medium/Low findings = `0/0/0`；Acceptance 1–10
  全部通过。
- 首轮六项 findings 全部 `CLOSED`：P1 admission/activation 已拆分，Host retry
  无 lifecycle/Effect path，P2 scope 已撤回，CONTEXT 已压缩，状态与同步声明已
  对齐，Deferred ⑰ 不新增 Authority class。
- Review scope 固定为五个 UAR 路径；并行 PER 工作树与 base 后提交均排除。

## Human Closeout（2026-09-12）

Decision: `ACCEPTED_CURRENT_STEP / UAR-001_CLOSED / R1_NOT_AUTHORIZED`

- Human 接受当前小步结果并关闭 UAR-001。
- 本次关闭表示设计候选、H1、双轴 Review 与 CONTRACT verification 范围完成；
  不把候选升级为 architecture authority。
- R1 Contract Closure 明确不在本次授权内；未创建 R1 Change、Plan、WorkItem 或
  实现。

## Verification

```yaml
verification:
  level: CONTRACT
  method: >
    检查文档 metadata/authority/boundary；逐项映射现有 Product baseline
    §2/§3.1–3.4/§17–21 与 Protocol P1/P18/P19/Deferred ①⑨⑫⑰；检查
    Codex/DSH 一方资料链接；检查 git diff、git diff --check、未授权路径和
    既有 dirty files 无变化
  expected: >
    Acceptance 1–10 全满足；变更仅包含 UAR-001 state、候选设计文档、
    ADR-0019、Protocol 的 H1 窄收口与 CONTEXT 单一术语；无 Product
    Architecture L0-L3、计划、WorkItem 或产品代码变更
  actual: >
    2026-09-12 限定修复后，path-scoped whitespace/stale-wording/local-link checks
    全部通过；SOL Standards/Spec 双轴 re-review 均为 PASS，High/Medium/Low =
    0/0/0，Acceptance 1–10 全满足；未修改或验证任何并行 PER/产品代码
  evidence: >
    2026-09-12 git status --short；git diff --check；git diff --no-index --check；
    local-link existence；rg metadata/stale wording；
    Human Decision、ADR-0019、NOT_AUTHORIZED、Product Session/Host Session、
    P1/P18/P19；Protocol Deferred ①闭合与 Deferred ⑯/⑰新增；Standards/Spec
    独立 SOL re-review reports
```

## Status log

- 2026-09-11 · understanding→resolving · 复核 Product baseline、Protocol deferred questions、CONTEXT、Codex/DSH 一方资料与本机版本
- 2026-09-11 · resolving（durable checkpoint） · 候选设计已写入并通过内部 contract check；Kernel driver Human Gate 未决，保持 resolving
- 2026-09-11 · resolving→persisted · Human 接受 Kernel self-driven；ADR-0019、Protocol Deferred ①与 CONTEXT 术语同步完成；等待 SOL Review，不进入 R1
- 2026-09-11 · persisted→resolving · SOL 双轴 Review = CHANGES_REQUIRED；记录 P1 activation Authority、P2 scope creep、CONTEXT glossary 越界与陈旧状态，修正后重审
- 2026-09-12 · resolving→persisted · 完成 P1/activation 拆分、P2 撤回、CONTEXT 压缩与陈旧状态清理；等待 SOL 双轴复审，不进入 R1
- 2026-09-12 · persisted→reviewed · SOL Standards/Spec 双轴 re-review = PASS；High/Medium/Low = 0/0/0，首次 findings 全部 CLOSED
- 2026-09-12 · reviewed→verified · path-scoped contract checks 通过；UAR-001 保持 Authority NONE，R1 等待 Human authorization
- 2026-09-12 · verified→closed · Human 接受当前小步结果并关闭 UAR-001；R1 明确未授权、未启动
