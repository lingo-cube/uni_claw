# CAP-001 — Capability Hub 与感知能力接入路线图
lifecycle_state: closed · disposition: none · depth: decision-heavy · base: working-tree

## Intent（WHAT/WHY）
建立可扩展的 Capability Hub 管理接缝，使 Product Runtime 能在不改变 Evidence、WorldModel、Control、Assurance 和 Effect Authority 的前提下注册、分类、装配和管理后续能力。感知协议与具体实现按依赖顺序接入，避免先造一个无真实买方的万能插件总线。

## Scope
- Capability Hub 管理骨架：注册域、能力描述、协议声明、版本、作用域、生命周期和健康状态。
- 感知能力分类与协议：Semantic Perception、UI Element Perception、PerceptionAssessment、来源与融合语义。
- 按分类接入 Capability Registry，区分复合能力、独立能力、实现来源和适配器。
- 具体能力纵向实现：Text Semantic Perception、Slow Visual，以及后续摄像头、语言校验和性能观测接入。

## Out of Scope
- 统一跨 Product Runtime、Runtime Integration、Development Harness 的共享注册表。
- Hub 成为 Evidence、WorldModel、Control、Assurance、Effect 或置信度权威。
- 未经真实能力协议验证的动态插件总线、任意模块自动编排和通用 opaque payload。
- 在本 Change 内完成所有后续能力实现；后续能力按路线图逐项派生 WorkItem。

## Decisions
1. Capability Hub 是 Capability Plane 的能力管理角色，不是新的 L2、Product Owner 或 Authority。
2. Product Runtime、Runtime Integration 和 Development Harness 使用独立注册域与 Composition Root，只共享 Host-neutral lifecycle fact 与 correlation 词汇。
3. 感知的外部买方接口是 Semantic Perception 与 UI Element Perception；Fast/Slow 是内部实现方式，不能直接替代外部协议。
4. XML 属于 UI Element 感知来源；YOLO/OCR 是快速感知实现或来源，不直接等同于最终 Product 能力。
5. Text Semantic Perception 是 Fast YOLO+OCR 与 Slow Text 的强绑定复合能力；Slow Visual 可独立并可同时实现两个感知接口。
6. PerceptionAssessment 暴露结构化评估，但不成为 Hub 或 Product Authority 的置信度裁决。

## Roadmap
1. Capability Hub 管理骨架。
2. 感知能力分类与协议冻结。
3. 按分类接入 Capability Registry。
4. 具体能力纵向实现：Text Semantic Perception → Slow Visual → 外部摄像头、语言校验、性能观测等。

## Acceptance
1. Hub 能在信任域内注册能力，并保留能力描述、协议、版本、作用域、依赖、生命周期和健康状态。
2. Hub 的生命周期事实在状态提交后发布，顺序单调、来源明确，不改写业务事实。
3. Product、Runtime Integration、Harness 注册域彼此隔离，不能通过共享可变注册状态越权。
4. 感知分类能区分外部买方接口、复合能力、独立能力、实现来源和适配器。
5. Text Semantic Perception 的 Fast YOLO+OCR → Slow Text 依赖、Slow Visual 的独立性及评估语义可被验证。
6. 既有 P2/P3、Evidence Ledger、WorldModel、Control、Assurance、Effect Boundary 的 Owner/Authority 不改变。

## Constraints
- 所有跨组件行为必须经显式协议、投影或 Composition Root 接入。
- 先做可验证的管理骨架，再冻结感知协议，再接入能力，最后实现具体能力。
- 不把 provider-specific model、Host transport 或 Harness session 语义写入共享层。

## Verification
level: DETERMINISTIC
method: 按 WorkItem 逐项运行契约、单元、边界和纵向场景验证
expected: 路线图依赖顺序可执行，Hub 管理骨架与既有 Owner/Authority 边界保持一致
actual: WI-CAP001-001 through WI-CAP001-009 are complete; Hub classification, Registry assembly, Text Semantic Fast YOLO+OCR → Slow Text, independent Slow Visual, shared Host-neutral integration contracts, language Inspector protocol, staged Operation Measurement pairing and external Artifact/Measurement binding are synchronized
evidence: `dotnet test tests/UniClaw.Kernel.Tests/UniClaw.Kernel.Tests.csproj --no-restore` → 792 passed; `dotnet test tests/UniClaw.Host.Tests/UniClaw.Host.Tests.csproj --no-restore` → 142 passed; `evidence/cap-001/registry/s3-verification.md`; `evidence/cap-001/perception/s4-verification.md`; `evidence/cap-001/perception/s5-verification.md`; `evidence/cap-001/integration/s6-verification.md`; `evidence/cap-001/inspection/s7-verification.md`; `evidence/cap-001/measurement/s8-verification.md`; `evidence/cap-001/external/s9-verification.md`; `git diff --check` → pass

## Status log
- 2026-10-04 · RESOLVE → PERSIST · 用户确认 Capability Hub 与感知能力接入的四步路线图。
- 2026-10-04 · PERSIST → PLAN · 生成 WI-CAP001-001..004 依赖顺序；先执行管理骨架。
- 2026-10-04 · PLAN → IMPLEMENT → REVIEW → VERIFY（S1）· 完成 WI-CAP001-001：Product Capability Registry 管理骨架、信任域隔离、生命周期事实和公共面白名单同步；737 个 Kernel 测试通过。
- 2026-10-04 · PLAN → IMPLEMENT → REVIEW → VERIFY（S2）· 完成 WI-CAP001-002：冻结 Semantic Perception / UI Element Perception、来源与字段级融合、Text Semantic / Slow Visual 关系及 PerceptionAssessment；未修改运行时链路。
- 2026-10-04 · PLAN → IMPLEMENT → REVIEW → VERIFY（S3）· 完成 WI-CAP001-003：注册级感知分类、协议角色、复合/独立关系和 fail-closed 装配校验；742 个 Kernel 测试通过。
- 2026-10-04 · PLAN → IMPLEMENT → REVIEW → VERIFY（S4）· 完成 WI-CAP001-004：同 capture/cycle 的 typed FastTextBasis 进入 Slow Text，门控区分缺失/过期/不对齐/空检测/provider 不可用，Slow Visual 保持独立，Slow provider 请求携带有界 basis；747 个 Kernel 测试和 142 个 Host 测试通过。
- 2026-10-04 · PLAN → IMPLEMENT → REVIEW → VERIFY（S5）· 完成 WI-CAP001-005：Slow Visual 改为仅依赖同周期 raw artifact，不再受 Fast YOLO/OCR availability 门控；成功、空结果、失败和晚到结果继续经既有 SlowResultProjector/P2；747 个 Kernel 测试和 142 个 Host 测试通过。
- 2026-10-04 · PLAN → IMPLEMENT → REVIEW → VERIFY（S6）· 完成 WI-CAP001-006：建立 Host-neutral Integration Envelope、correlation、Finding、MeasurementSample、ArtifactReference 和 FixtureLifecycleFact 契约；Runtime、Host monotonic、外部设备和传感器时间分离，Measurement 要求显式关联；6 个定向 Kernel 测试通过，后续全量回归为 792 个 Kernel 和 142 个 Host 测试通过。
- 2026-10-04 · PLAN → IMPLEMENT → REVIEW → VERIFY（S7）· 完成 WI-CAP001-007：建立 ObservationAccepted 只读文本投影与语言 Inspector 请求契约，分开 declared/rendered text、occurrence/source reference、规则版本、期望语言、覆盖/输入状态，并只输出既有 Finding；35 个 Capability 定向测试和 763 个 Kernel 测试通过，未接入主循环或任何 Owner 写入路径。
- 2026-10-04 · PLAN → IMPLEMENT → REVIEW → VERIFY（S8）· 完成 WI-CAP001-008：建立 Dispatch、Receipt、PostActionVerification 三阶段的 OperationStarted/OperationTerminal 配对契约，要求显式关联和终点序号晚于起点；13 个定向测试和 776 个 Kernel 测试通过，未修改 EffectBoundary、Receipt Owner 或主循环。
- 2026-10-04 · PLAN → IMPLEMENT → REVIEW → VERIFY（S9）· 完成 WI-CAP001-009：建立 ArtifactReference 与外部 Measurement 的完整 correlation 绑定，拒绝 Run/Capture/Receipt/Operation 错配，保留 Fixture 生命周期和外部设备/传感器时间；16 个定向测试、792 个 Kernel 测试和 142 个 Host 测试通过。
- 2026-10-04 · VERIFY → CLOSED · CAP-001 路线图四步及其后续协议接缝已完成；统一目录搬迁和未来 Promotion 保留为后续独立决策，不影响本 Change 的 Acceptance。
