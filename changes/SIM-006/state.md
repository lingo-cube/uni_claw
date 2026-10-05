# SIM-006 — 本地测试链路、仿真基线与反馈工程化

lifecycle_state: persisted · disposition: none · depth: decision-heavy · base: bc3bd984ae5833c88b0643dd3b9909014f352f53

## Intent（WHAT/WHY）

用户需要在本地重复运行 UniClaw 测试，知道每个组件和完整闭环实际证明了什么，能追溯本次采用的测试资产、配置和实现，并根据失败证据改进 profile、prompt、能力实现或 Runtime。

现有仿真、组件测试、场景认证、覆盖率工具和 Android Settings 测试集已经存在，但它们还缺少一条可查询的测试任务到执行证据关联，以及统一、可复核的配置和反馈记录。不能把散落的 PASS 合成“系统全链路已通过”，也不能把 deterministic double 的输出当成真实模型能力证明。

目标：复用已有 Runtime、typed seams 和验证工具，先把首批 8 个仿真场景及组件契约组织成可重复运行、可解释失败的基线，再以独立证据接入本地 Product Host 和真实模型/设备。用户已确认本方案并要求先完成 to-spec/PERSIST，后续由 GLM-5.3 接手。当前只持久化 WHAT/WHY/ACCEPTANCE，不推进 PLAN、IMPLEMENT 或产品完成声明。

## Scope / Out of Scope

### 后续实现范围

- 首批 8 个既有场景的基线矩阵：被测能力、组件范围、真实组件与 double、外部行为断言、执行载体、证据和已知缺口。
- 组件契约测试与闭环场景的关联；保留独立组件测试，不维护第二套产品逻辑或重复 golden。
- 测试任务到 scenario/契约测试、测试 FQN、实际运行和 evidence 的显式关联；映射只维护一个声明式来源，反向关系派生。
- 脱敏的解析配置快照：记录真正被执行入口消费的配置、资产版本、实现绑定、参数和环境结果，而非另外拼一份未被消费的配置。
- 本地快速反馈与完整验收入口：复用现有工具，分组结果与聚合结论分离，避免重复执行同一构建/测试。
- 机器可读运行反馈：expected、actual、第一处可证实分歧、责任定位依据和证据引用；未知如实保留。
- 以上能力所需的有界 schema/validator/adapter 修改和文档同步。字段及物理载体由现有读取者和真实测试证据确定。

### 本轮 PERSIST 修改范围

- 本 Change State 与派生 open-change index。后续实现验收与本轮文档检查分别记录。

### 不在本 Change 内

- Android Settings 安全策略生成、profile/context 注入和 Runtime 执法接入；AGT-012 第三项继续为 PROFILE_CONTRACT_NOT_READY，由后续独立 Change 承接。
- 字面意义的全部 Android Settings 树遍历；当前 coverage profile 的有界覆盖不等于全树。
- 新建 Host Catalog 管理 UI、Workspace 写入控制面或第二套任务/结果数据库。
- 改写冻结 Product/Simulation Owner、Authority、生命周期或协议语义。
- 从零重写 8 个已有场景、批量修改 golden 让失败变绿，或自动重认证。
- 真实模型质量评分、真实设备测试通过、生产就绪声明、性能门槛或新增性能基准。此处只预留独立执行与报告边界。
- 新的多设备/多 Run 产品语义、部署仿真、假设修正注入。
- 当前 session 自动派发 GLM、创建 WorkItem 或替用户向另一个 chat 发消息。

## Current facts（2026-10-05 核对；不是本轮执行结果）

- 基线版本：simulation-baseline-v0.1 继续有效，C7 由 v0.2 delta 取代。整装仿真六个 L2 为真件，只替换外部缝；组件隔离允许完整契约的相邻 double。
- 场景 JSON 已有 components、agentDecisionRealization、goalEvaluationRealization、expectations、execution 和 certification。首批 8 个条目当前标注 decision=double、evaluation=real；status 声明不是新鲜执行证据。
- ScenarioRunner 经 AdmitContract → Activate → Drive 运行，再读取结果生成 ScenarioReport；Goal Evaluation 经真实 UniClaw.Agent.UniAgent 执行。不能绕过该 seam 直接驱动内部 cycle 来伪造闭环。
- scenario-coverage 从二进制 trait 获取 scenario→FQN，再与 TRX 结果关联；scenario_certify 校验 expectationsDigest、executionDigest、runtimeSourceHash 和致因 Change。
- SCN-PERC-001 的 execution.kind=none，使用专有测试载体；不能宣称它已有与 golden-bundle 相同的期望绑定保证。
- verify-change 已提供 Simulation、完整 solution、认证、coverage 和可选 live，focused 目前明确 SKIPPED；当前 Simulation、完整 solution、coverage 可能重复跑测试。
- verify-change 的 classify 当前先检查 live 环境不可用，再检查仿真失败；有同时失败被环境分类遮蔽的风险。后续须保留两者结果，不把此风险当成本轮已修复。
- verify-live 有设备启动/选择和真实 Host gates；默认完整 solution 绿不自动证明 DSH/模型或设备 gates 已执行。
- testsets 当前是 Host Catalog 未就绪前的声明式来源；manifest 使用 default revision，可附 sourceRevision。repo-default 不能单独定位历史内容，快照须补充实际内容版本/digest。
- UniClaw Test Lab 的规范 projectRef 为 project/uni-claw-test-lab；Android Settings 资产现用 project/android-settings。本 Change 不未经裁决重命名既有 Project identity。

## Decisions（已确认）

1. 分层验证：组件契约、闭环仿真、本地 Product Host、真实模型/设备分别表达覆盖和执行结果。测试层不是新的 UniFlow verification level，也不新增未经实现的 Product Outcome 标签。
2. 采用完整闭环与组件 seam 契约两个定位维度。独立组件测试保留；闭环场景为可版本化的场景基线，组件维度引用既有契约断言，不复制 golden。
3. 首批复用下表 8 个场景，先审核现有断言与缺口，再补有明确买家和验收的测试。不要求每个组件必须新增一个接口、scene 或场景文件。
4. 断言只证明被测真件的外部行为。double 输出断言仅为 double 自检；Agent decision 与 Goal Evaluation realization 分别标注。仿真 digest 对等不证明 live model 的语义质量或性能。
5. 语义结果和关键链路不变量是基线主体，截图为可选辅助；性能测量独立，VirtualClock 延迟不冒充真实性能。
6. 配置按字段责任管理，不采用任意覆盖链：冻结基线约束不可配置覆盖；profile 管 realization 装配；testset/scenario 管 fixture、范围、输入与期望；环境提供设备、路径和可用性。显式运行参数只能修改入口公开允许的字段。安全限制、期望与授权冲突须在 effect 前拒绝或明确请求裁决，不能静默放宽。
7. 每次执行保留解析配置快照，记录与真实消费一致的输入、来源和版本；运行中发生允许的 binding 变化须另记来源和实际使用关系。快照为运行证据，不回写 profile，不提交生成运行文件，不成为 Product truth。
8. 测试关联链为 Task/Test Set revision → scenario 或组件契约 → 当前二进制测试 FQN → 实际 execution result/evidence。FQN 由测试枚举派生，不人工复制易漂移的清单；无对应测试如实表达缺口。
9. 映射须表达覆盖范围。SCN-POLICY-009 证明 effect class 契约拒绝，不证明 Android 危险菜单识别；Wi-Fi 仿真不证明菜单路径真实可达。
10. 默认本地反馈不依赖凭证、网络或设备。快速反馈执行相关契约与选定场景；完整验收执行完整回归、认证/coverage 和本地 Host 组。真实模型与设备分别显式门控，缺少环境时保留未运行/不可用结果。
11. 聚合结论不得遮蔽任何失败：PASS 仅对实际声明并执行的范围成立；请求的必需组跳过或环境不可用时，不能称完整验收通过。阶段失败后的下游可不执行，但必须记录未执行原因；不以执行顺序替代因果诊断。
12. 反馈先回答 expected、actual、第一处有证据的分歧和责任依据。复用已有总分类 BEHAVIOR_REGRESSION、CERTIFICATION_STALE_ONLY、ENVIRONMENT_UNAVAILABLE；无法支持分类时保留 UNCLASSIFIED，无法定位时保留 unknown。暂不冻结大套细分类。
13. 反馈报告记录定位建议和证据，不自动修产品、不自动变更 profile/prompt、不改 scenario status/期望。修复经致因 Change、回归测试和适当证据完成。
14. 期望或执行绑定变更继续沿用 C8；认证通过不等于行为通过。先查根因，再评审是否改变预期；重认证必须有明确致因 Change，禁止无记录 bulk-update。
15. 文档按真实长期取舍更新：ADR 只有满足难逆转、需要解释且有真实替代方案时才创建；CONTEXT 只记录定型领域词，不保存实现结构、字段列表或执行计划。

## Initial baseline matrix（首批范围；未重跑，不声明 PASS）

| 场景 | 拟复用的行为证明 | 需核对的证据边界 |
|---|---|---|
| SCN-SMOKE-001 | 同一 Runtime 的 Host 整装闭环，到 Outcome/Evaluation | 现用 wifi-off-to-on carrier，含 1 次 effect；不是零动作烟测，不能当成与 WIFI-001 独立的新能力 |
| SCN-WIFI-001 | off→on，后置观察支持完成 | driver/decision 为 double，不能证明真实设备切换 |
| SCN-WIFI-002 | 已满足 desired state 时零 dispatch | 保留观察和满足证据；不靠零动作数单独判成功 |
| SCN-PERC-001 | Fast-only 感知到验证闭环 | execution.kind=none；需确认专有 harness 的实际断言与资产来源，不冒充真实感知模型基线 |
| SCN-BARRIER-001 | 两步之间有验证证据，才允许下一步 | 同时关联既有负向契约/场景，否则正向案例不能证明阻断有效 |
| SCN-POLICY-006 | guard Unknown 时零新 effect | 冲突/Unknown 不折叠为 false 或安全授权 |
| SCN-POLICY-007 | 后置观察违背 desired state，policy 失效 | 已送达不等于已完成，terminal absence 不能伪造 SafeStop/Failure |
| SCN-POLICY-009 | effect class 超出 Execution Contract 被拒绝 | 不是未知 Settings 菜单的语义风险识别测试 |

## Candidate choices（不当作已冻结接口）

- testLayer、requiredEvidence、testSetRefs、scenarioRefs 等字段及所在文件，须由消费路径和最小 schema diff 决定；不预先在 scenario 和 manifest 两边维护可写映射。
- resolved-config.json 为候选文件名。最小语义须覆盖本次选择、来源、内容版本、实际绑定、门状态和脱敏环境；未参与执行的配置标为不适用，不凭字段名猜 truth。
- 机器报告格式优先复用现有 ScenarioReport（真实类型名）与工具输出；需要 adapter 时限定在 Host/Harness 对应层，不为报告暴露 canonical owner 内部状态。
- 快速/完整运行参数、哪些本地 Host 测试纳入、选择性执行与 coverage freshness 的兼容方式，需接手方先核对现有入口，再在 PLAN 固定；subset 不能伪装成全场景 coverage。
- 不新增 PRODUCT_LOCAL 等 Outcome；新增测试组名如有需要只属于验证工具报告。

## Acceptance（后续实现完成的判据）

| ID | 用户可观察行为 | 预期与证据 |
|---|---|---|
| A1 | 能列出首批 8 场景及相关组件契约 | 每项有来源、能力、真件/double、carrier/test、断言与缺口；无无依据的 baseline PASS |
| A2 | 能重复执行既有闭环 | 相同 fixture/config 下确定性场景重复运行 semantic digest 一致；真实断言执行通过，证据定位到当前源码/资产 |
| A3 | 能从任务追到执行证据 | 实际采用的 task/testset revision 可追到 scenario/组件契约、二进制 FQN、TRX/报告；未知引用、悬空映射或不匹配范围不能静默通过 |
| A4 | 能复原一次运行的配置依据 | 快照与实际消费一致且脱敏，输入有来源/内容版本；运行 A 不能被运行 B 的配置或证据覆盖；配置变化不伪称相同基线 |
| A5 | 配置冲突不会扩大授权 | 不允许的参数覆盖、安全策略冲突、未知必需配置在 effect 前拒绝；快照不含测试提供的哨兵凭证或原始 secret |
| A6 | 能选择快速反馈或完整验收 | scope 明确，各组有执行/跳过/不可用结果；复用测试结果有当前二进制/输入关联；不重复运行只为拼报告 |
| A7 | 多种同时失败仍可见 | 注入仿真失败与 live 环境不可用并存的工具结果，二者均保留且聚合不能 PASS；FIRST_FAILURE 不冒充首个行为分歧 |
| A8 | 失败能被定位或诚实保留未知 | 提供 expected/actual、证据支持的 first divergence、owner 依据；证据不足时 unknown/UNCLASSIFIED；不产生自动修复或自动期望迁移 |
| A9 | 认证、执行和覆盖仍各自有效 | unchanged expectationsDigest；若确需期望/绑定迁移，有评审和致因 Change。新鲜完整 coverage 保持原真值链，不拿旧 TRX/JSON status 冒充执行 |
| A10 | Settings 缺口不会被仿真覆盖 | 三任务仍为真实测试目标；禁止 effect 场景仅是部分先决证明，第三项 policy 未接入仍为 PROFILE_CONTRACT_NOT_READY |
| A11 | 产品与 Harness/Simulation 边界保持 | 无仿真分支进入 Product Host，无工具/模型/Host-specific 语义污染共享协议；文档与实现一致，恰当回归通过 |

## Constraints / Owner-Authority impact

- 本 Change 是测试资产、执行入口和诊断证据工程化，不取得 canonical WorldBelief、Run State、Assurance、Effect、Outcome 或 Goal Evaluation authority。
- Product Runtime artifact 不分叉；Simulation Host 是独立 composition root；组件 double 必须完整履行既有 typed seam，偏差显式记录。
- 仿真 grant 使用预录制/预评审静态资产，测试工具不能动态签发产品授权。raw 设备资产导入遵循现有敏感内容评审与 lineage 规则。
- Host/Task Catalog 仍是 Project/Test Set 长期 owner；仓库 testsets 只是临时声明式来源，Workspace 只读投影，不从目录推导 Product Session/runId。
- 每个测试运行/验证批次的 correlation 与 Runtime 产生的 runId 分开；进程退出码/工具 PASS 不改写 terminal Runtime Outcome。
- 本轮只写 state/index；后续扩大源码、schema 或 frozen baseline 修改前须明确 buyer/Acceptance 和文件归属，不借工程化名义重写核心。

## Assumptions / Alternatives（含被拒）

- 假设：8 个现有场景可复用其载体和断言；接手后必须新鲜执行，若事实不符回 RESOLVE，不为匹配本表而改 golden。
- 拒绝：新建统一测试管理平台或第二结果存储。理由：现有 testsets/scenarios/运行证据已具备各自 owner，先补关联。
- 拒绝：所有组件测试强制转 scenario。理由：独立 seam 契约有定位价值，且不应重造第二 golden。
- 拒绝：profile→scenario→env 任意覆盖。理由：各层责任不同，后者不能扩大前者授权或改测试期望。
- 拒绝：立即冻结完整失败 taxonomy、新模型接口或大量 metadata 字段。理由：真实买家/样本尚不足，先复用现有输出并验证最小报告。
- 拒绝：一律全量重跑、自动重认证、设备环境失败覆盖行为失败。理由：分别浪费反馈时间、破坏期望审计、遮蔽真实失败。

## ADR / Authority references

- ADR-0007：Development Flow 与 PERSIST/PLAN 分离。
- ADR-0026：接口由真实 buyer 验证，不从名词预造。
- ADR-0032：Host-owned Project/Test Catalog；当前仓库临时来源。
- ADR-0037 与 repository-role-boundaries-v0.1：testsets/scenarios/evidence/.dsh 等职责边界。
- Product architecture baseline §24.8；Simulation baseline v0.1 C1–C9 + v0.2 C7：同一 Runtime、独立 Host、realization 标注、C8/C9。
- 本轮未新增 ADR，不修改已接受基线；以上为继承约束，非重新裁决。

## Verification（本轮文档检查与后续实现验证分离）

### 本轮 PERSIST

```yaml
verification:
  level: CONTRACT
  method: >-
    python3 tools/gen-open-changes.py;
    校验本 state 的结构、8 场景与引用文件存在、realization/carrier 与当前 JSON 一致；
    git diff --check；核对本轮仅修改 state/index
  expected: >-
    SIM-006 为 persisted；索引含该 open change；首批场景/引用有效；
    无实现、无新鲜运行 PASS、无模型/设备或认证写入
  actual: >-
    PASS（仅 PERSIST 文档检查）：结构与 8 场景引用/realization/carrier 核对通过；
    引用文件存在；索引为 134 changes、1 open，SIM-006=persisted；
    git diff --check 通过，仅新增本 state 并再生 index；未执行实现验收、模型或设备测试
  evidence: 本 state、changes/INDEX.md 和本轮检查输出
```

### 后续实现

| level | method | expected | actual / evidence |
|---|---|---|---|
| CONTRACT | schema/引用/输入覆盖与脱敏验证；产品边界检查；配置和报告实际消费路径核对 | A1/A3/A4/A5/A11 通过，非法映射/配置拒绝 | NOT_RUN；后续填写真实输出与证据引用 |
| DETERMINISTIC | 工具聚合行为测试，包括同时失败、选择范围、跳过和未知归因；组件契约回归 | A5/A6/A7/A8；原始失败均保留，无虚构 PASS/owner | NOT_RUN |
| SCENARIO | 首批场景新鲜执行与重复 digest；按实际变更执行完整 Simulation/solution、认证检查和完整 coverage | A2/A9/A10/A11；partial coverage 不声称全绿，不自动改 golden | NOT_RUN |
| ENVIRONMENT | 若后续独立请求真实模型/设备，走已登记环境 gate 和实际任务验收 | 与前几层分开；不可用如实报告；本 Change 不要求 live PASS 才证明仿真工程化 | NOT_RUN；本轮未启动模型或设备 |

## Residual risks / 接手须先核对

- 场景 JSON status、旧认证和旧 TRX 不能证明本次 HEAD 通过；SCN-SMOKE/WIFI 共用 carrier、PERC 的 none 边界不可忽略。
- 当前 runtimeSourceHash 覆盖 Kernel/Agent；Host、工具、provider/profile/testset 的影响须在配置/报告关联补齐，不擅自扩大 golden 认证职责。
- 现有 coverage freshness 主要依据文件时间；运行复用须证明当前二进制与资产关联，不只看报告路径存在。
- Settings 测试文字中“导航零 DeliveryCompleted”的可判定性需对照 receipt 语义，不能误把安全导航当成禁止 effect。本 Change 不顺手修改 AGT-012；证伪时记录独立修订。
- 持久化期间未新增领域词或接口；接手方先从已有 ScenarioReport/config/trait seam 派生最小实现。需要改共享契约时先返回 RESOLVE 并明确 authority impact。

## Reading entry points（定位材料；不是实现文件分配或 WorkItem）

- 流程与交付：AGENTS.md、changes/README.md、docs/agents/issue-tracker.md、.agents/skills/uniflow/SKILL.md、docs/agents/work-reporting.md。
- 仿真权威：docs/architecture/simulation-baseline-v0.1.md、docs/architecture/simulation-baseline-v0.2-c7-amendment.md、CONTEXT.md。
- 场景和执行：scenarios/schema.json、首批 SCN JSON、tests/UniClaw.Simulation.Tests/ScenarioLibrary.cs、ScenarioRunner.cs、SimulationHost.cs、SeamOverrideTests.cs、ScenarioRealizationAnnotationTests.cs。
- 认证与聚合：tools/scenario_certify.py、tools/scenario-coverage.py、tools/verify-change、tools/verify-live。
- 测试目录：testsets/README.md、testsets/android-settings/manifest.json、testsets/android-settings/README.md、tools/validate-testset-manifests.py。
- Host/config：src/UniClaw.Host/HostRunner.cs、src/UniClaw.Host.Dsh/Program.cs、src/UniClaw.Host/SettingsCoverage/SettingsCoverageConfig.cs、.dsh/profiles/settings-coverage.yaml、.dsh/profiles/uniagent-prod.yaml、.dsh/model-bindings.yaml。
- 相邻 Change：SIM-002/003/004/005、AGT-012、ARCH-DOC-018/019。

## Status log

- 2026-10-05 · UNDERSTAND → RESOLVE · 用户经 grill-with-docs 确认分层验证、组件契约与闭环基线、首批 8 场景、配置快照、映射与最小反馈；接受方案审核后的收紧边界。
- 2026-10-05 · RESOLVE → PERSIST · 用户授权 to-spec，后续交 GLM-5.3 实施；按项目 issue-tracker 映射创建本 state，候选字段不冻结，后续验收未执行。
- 2026-10-05 · PERSIST（保持） · 文档结构、8 场景与引用、index 和 exact scope 检查通过；后续实现 verification 仍为 NOT_RUN，未进入 PLAN。
