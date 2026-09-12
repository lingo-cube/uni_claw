# 0019 — Primary Run 由 Uni Kernel self-drive，不由 UniAgent 逐 cycle 驱动

Target Product Architecture 已锁定 UniAgent 拥有 Primary Goal、Execution
Contract authoring 与 Goal Evaluation，Uni Kernel 拥有一次 Primary Run 的
aggregate execution boundary；但协议 Deferred ① 尚未决定 Primary Run 合法
激活后由谁驱动 Kernel 操作面。Codex-backed 与 DSH-backed 两个完整 UniAgent
realization 使该问题出现真实 buyer。Human Gate 于 2026-09-11 接受以下决定。

## Decision

```text
UniAgent --P1 Contract Proposal--> Run Model admission
accepted Contract View
  → is a required precondition for one separately activated Primary Run
legal Primary Run activation
  → protocol/seam semantics deferred to R1; no new Authority class
legally activated Primary Run
  → Uni Kernel internal run driver sequences the bounded execution loop
  → Evidence / Belief / Control / Assurance / Effect 各自保持原有 sole Authority
terminal Outcome State
  → Uni Kernel emits P18 immutable Runtime Outcome exactly once
  → UniAgent performs Goal Evaluation
```

UniAgent 不逐 cycle 调用或编排 `Process / SelectIntent / Act /
EvaluateTerminal`。Codex/DSH Host loop、turn、step、round、resume 或 retry 也不得
成为 Primary Run driver。

P1 只负责 Contract Proposal/admission，不负责激活、恢复或推进 Run。重复 P1
admission 只能返回同一 accepted Contract View；它不得启动、恢复或推进 Primary
Run，不得产生现实 Effect。Host 对 P1 的 retry 同样不构成 lifecycle command。
accepted Contract View 到合法 Primary Run activation 的唯一 Producer、Consumer、
identity、correlation、重复/并发、失败与恢复语义由 R1 另行闭合；任何方案必须
服从现有 Authority Matrix，并只通过 Run Model typed legal transition 改变
canonical Run State，不得新增 activation Authority class。

Kernel internal run driver 只拥有 composition/lifecycle coordination，不拥有任何
canonical domain truth 或 judgment Authority；它必须通过既有 typed protocol
驱动六个 L2 Owners，所有现实 Effect 仍 exclusively pass through Effect Boundary。

未来若出现 cancel、pause、resume 或 escalation buyer，应建立独立、有界、幂等的
lifecycle command protocol。此类 command 不能演化成逐 cycle orchestration，也
不能直接签发 Control Intent、Assurance Judgment 或 Effect command。

## Considered Options

- **UniAgent/Host step-drives Kernel**：拒绝。它会把 Kernel L2 sequencing 泄漏到
  Codex/DSH realization，使 Host retry/resume 影响现实 Effect，并诱使 UniAgent
  取得 Control、Assurance 或 Run Authority。
- **独立 Product Session Coordinator 同时驱动 UniAgent 与 Kernel**：当前拒绝。
  单 Goal、单 Run baseline 没有足够 buyer 支撑第三个 orchestration owner，且容易
  形成共享 God Context。未来多 Goal/多 Run buyer 可用新证据重开，而不是扩张本
  ADR。
- **Uni Kernel self-driven**：接受。它形成最小、最深的 UniAgent↔Kernel seam，
  保持 Host replaceability，并把 effect safety 与 recovery 留在执行 Authority 内。

## Consequences

- 当前 UniAgent↔Kernel 主监督弧保持 `P1 Contract Proposal → P18 Runtime Outcome`；
  P19 Goal Evaluation 继续面向 user/session。
- Contract admission 不触发 run activation；accepted Contract View 只是合法激活的
  必要前置条件。R1 必须为 activation seam 定义唯一 Producer/Consumer，以及
  typed、幂等、可恢复的关联，确保同一目标 cardinality 下最多一个 Primary Run；
  R1 不得改写既有 Authority Matrix。
- Kernel 需要 internal run driver 与有界 progress/terminal observation surface；
  这不允许暴露 L2 sequencing 给 UniAgent。
- Host crash/resume/retry 不得直接重放 Kernel cycle 或 Effect；恢复必须依据
  canonical Product/Run state，而不是 transcript 位置。
- 本 ADR 只闭合目标协议与术语，不授权实现、Tracer Bullet 或详细路线图。
