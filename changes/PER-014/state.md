# PER-014 — Typed Semantic Consumer Migration（*.state 消费面 cutover）

lifecycle_state: verified · disposition: none · depth: standard · base: 156f9c96

## Intent（WHAT/WHY）

把仍在消费 legacy `*.state`（"on"/"off"/"partial" 字符串 claims）的生产
路径迁到 typed semantic path（`semantic.checked` / `rendered.*` /
Observed·Unknown·Unsupported·Partial 值域）。这是 PER-012 迁移契约的
consumer cutover 执行轮：PER-013 已完成 typed observation 落地与
observer 级（group-1）cutover，M-10 记录 5 处生产 reader 残留
（ConflictResolver / PostActionXmlRouter / UniKernel:489 /
LivePerception / MapTargetStateClaim）。

## Scope

- Phase-1 全量审计：`*.state` readers/writers、"on"/"off"/"partial"
  手工映射、CheckedState / semantic.checked / LegacyStateProjection
  消费面（生产代码为主，tests 单列）
- 生产 consumer 迁移：one consumer = one route、no dual-read、
  legacy only egress（PER-012 契约）
- 值域保真：Checked/Unchecked/Partial/Unknown/Unsupported 全保留，
  不可处理 → fail closed / insufficient evidence，不猜值
- T1–T10 测试 + G1–G5 门 + reader/writer 复算清单（可复算）

## Out of Scope

- PER-011 Fusion（不实现）
- WorldModel / Grounding authority 变更（G4 冻结）
- legacy surface 删除（只做 readiness 扫描；删除需 PER-012 四条件
  全满足 + 独立决策）
- Policy / Assurance / Verification 获得任何新 authority

## Decisions

- 上游契约：PER-012（cutover 顺序 observer → 非effect-critical →
  Control/verification/effect-critical；禁 dual-read；egress-only）、
  PER-010/011 FROZEN 设计（semantic/rendered 分轴；typed 唯一前向）、
  PER-013 M-10 reader inventory（起点事实）
- 分工（任务指令）：GLM-5.3-Flash 执行 inventory / file:line 审计 /
  机械迁移 / 测试 / 复算 / evidence（~95%）；GLM-5.3 裁决 G3
  semantic-rendered 边界、G4 authority 边界、G1/G2/G5 spot-check、
  final verdict
- G3 默认：`semantic.checked != rendered.toggleAppearance`，不同
  claim domain 不自动 Conflict；禁止 rendered ON/OFF → semantic
  Checked/Unchecked 复现
- G2 禁止：Unknown/Partial/Unsupported → false/off 压缩

## Acceptance

1. G1：生产 `*.state` reader 全部有明确处置（migrate / keep with
   explicit legacy-only justification / delete），零无主遗留；新 typed
   consumer 路径不偷读 `*.state`
2. G2：值域保真（T3/T4/T5 证明），无 Unknown/Partial/Unsupported →
   false/off 压缩点
3. G3：semantic/rendered 分轴保持（T6 证明两轴并存；无跨轴推导）
4. G4：Policy/Assurance/Verification 迁移后为纯 typed consumer，
   零新增 truth/conflict/WorldModel-write authority（T8/T9 + 调用链
   审计）
5. G5：production readers 复算 = 0（或返回 exact file:line + blocker）；
   legacy 删除四条件状态如实登记（不自动删除）
6. T1–T10 全绿；focused + full solution + certification +
   git diff --check

## Verification

```yaml
level: DETERMINISTIC（live 项 = ENVIRONMENT，env-skip 如实登记）
method: >
  Flash 五片实现（A 缝 / B 验证 cutover / C obligation cutover / D writer
  门控 / E 守卫+测试+复算）→ Leader 复核（G3/G4 裁决 + G1/G2/G5 spot-check
  + 独立复跑）。全量 dotnet test UniClaw.Kernel.slnx +
  scenario_certify --change PER-014 --all/--check + scenario-coverage
  --run + git diff --check
expected: >
  G1–G5 全 PASS；T1–T10 全绿；零 authority 变更；零折叠；零 dual-read；
  unjustified production readers = 0；legacy surface 不删
actual: >
  全 solution 1042/0（Kernel 634 · Host 63 · Simulation 184 · Agent.Dsh
  121 · Agent 17 · Core 14 · FSRealization 9）；certification 29/29 PASS
  （expectationsDigest 逐字节不变）；coverage 29/29 真值链 PASS；
  diff-check CLEAN；G1–G5 全 PASS（详见 evidence §6）；T1–T10 全 PASS
  （SemanticCheckedResolverTests 6 + Per014CutoverTests 8 +
  LegacyEgressGateTests 2 + LegacyStateSurfaceFreezeTests）
evidence: evidence/2026-09-26-per-014-cutover-evidence.md（§1–§6，含
  Leader 复核与两处 finding 处置）
```

## Status log

- 2026-09-26 · created·persisted · 任务指令落档（Leader GLM-5.3 /
  Worker GLM-5.3-Flash 分工；base 156f9c96 工作树干净）；Phase-1
  审计派发 Flash。
- 2026-09-26 · phase1-audit-done · Flash 全量审计（118 文件）：确认
  typed subject 为 `ui.node.{captureId}#{localIndex}.{field}`
  （occurrence-qualified，Unknown/Unsupported 不发 claim）、**role→typed
  checked 缝不存在**（Grounding 有 role→occurrence，无 occurrence→typed
  claim join）；legacy 面 2 生产 writer（MapTargetStateClaim XML→{role}.state、
  LivePerception wifi_on→switch.state）+ 消费链（UniKernel XML 验证 / 
  ConflictResolver / HostRunner obligation→RuntimeAssurance+ hollow guard /
  agent-context egress / Policy latent）；冻结守卫名单 8 文件（增删均 RED）。
- 2026-09-26 · leader-rulings（G3/G4 边界前置裁决，实现依此执行）：
  - **R1 新缝（consumer-side）**：Kernel 增只读 role→typed-checked 解析
    （owner-derived projection，同 PolicyEvaluationView 先例）：Role →
    （既有 occurrence/grounding 解析）→ occurrence → `ui.node.*.checked`
    claim → ObservedValue&lt;CheckedState&gt;。禁止：写回 / 持久化 / 缝内
    冲突裁决 / producer 侧 role 绑定（Grounding 保持 target binding 唯一）。
  - **R2 验证链迁移**：UniKernel post-action 验证改 typed（四门等价重述：
    时序=captureTime&gt;dispatchTime、身份唯一=occurrence 唯一解析、
    属性有效=ExactProof/capability、值=CheckedState）；Partial→不通过
    （insufficient）；Unknown/Unsupported→fail closed 回 occurrence 视觉
    路径。PostActionXmlRouter 保留为 rollback/egress（冻结在案，不再被
    生产调用）。
  - **R3 obligation/契约迁移**：TargetSpec.DesiredState → CheckedState 值域；
    HostRunner obligation → typed（role-scoped semantic.checked）；
    RuntimeAssurance satisfaction 走 R1 缝（CheckedState 相等；Unknown/
    Unsupported ≠ satisfied，零压缩）；HostOptions.TargetState 词汇
    on/off → checked/unchecked（greenfield，测试随迁）；hollow guard
    随 obligation 自动迁移（验证）。**语义后果显式登记**：Completion 权威
    由「任一 *.state claim」收窄为 widget semantic.checked；wifi_on 探针
    （设备无线电态，非 UI 语义）降为 egress writer（零生产读者），设备态
    obligation 词汇 = deferred，owner = 未来 effect-verification change
    （D13 合流遗留的正当拆分，G3 分轴同族）。
  - **R4 ConflictResolver**：*.state 权威裁决路径保留为显式 legacy-only
    （rollback 面 + 冻结在案）；正常 typed run 中 *.state 冲突不再发生
    （见 R5），路径休眠。
  - **R5 writer 处置**：MapTargetStateClaim legacy 发射以 legacy/degraded
    回滚旗门控（默认关，M-08 语义：开启即 run 标记 legacy/degraded）；
    wifi 探针 writer 保留（单一 producer egress，供测试/回滚观察）。
    正常 run 零 legacy 冲突。
  - **R6 Policy 禁区**：RUN-005/AGT-001 冻结的 Policy 词汇零改动；PolicyRuntime
    保持 subject-parametric 通用性（无 legacy 耦合、零新 authority）；
    role-checked predicate 词汇 = 未来 change（不在本轮）。
  - agent-context claim 投影（ConsultAgentV2）= 既有 F-H1 deferred
    （owner = PER-011/agent-context change），本轮保持 egress，G1 记
    explicit legacy-only justification。

- 2026-09-26 · implemented→reviewed→verified · Flash 五片全部落地（A
  SemanticCheckedResolver 只读缝 + T1–T5；B TryRouteTypedVerification
  四门 typed 重述、{role}.state 生产读取删除；C TargetSpec.DesiredState→
  CheckedState + obligation typed 化 + HostOptions 词汇迁移；D
  LegacyStateEgress 旗门控 + M-08 progress 侧标记；E 冻结守卫修订 +
  T6–T10 + legacy-pinning 测试迁移）。Leader 复核：G3 裁决
  FromPresentation = M-04 无损词对比较边缘适配器（非推导、非折叠）；
  G4 裁决 diff 零 authority 触碰（PolicyRuntime/Agent.Dsh 未动）；修正
  WorldModel doc 注释与实现矛盾；注释修正致源哈希位移 → 重认证 29 场景
  复绿。全量 1042/0 + certification 29/29 + coverage 29/29 + diff-check
  CLEAN。**STOP at Final Gate：G1–G5 全 PASS → ready for legacy
  removal decision（四条件中 rollback 观察窗未观测，不自动删除；不进入
  PER-011 Fusion）。**
