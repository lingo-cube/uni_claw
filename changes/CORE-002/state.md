# CORE-002 — Core 独立语义协议第一条 tracer
lifecycle_state: closed · disposition: none · depth: decision-heavy · base: working-tree
triage_label: ready-for-agent

## Intent（WHAT/WHY）

在已锁定的 CORE-001 to-spec 约束下，建立一个零 ProjectReference 的 `UniClaw.Core` 独立项目，并用领域无关的最小内存语义路径证明手机滚动—点击所需的 Segment、Slice、Evidence、Claim、Effect、Attempt 和 TargetBinding 可以独立表达。

## Scope

阶段 1（已关闭）：

- 新建独立 Core 项目和纯语义测试项目。
- 实现第一条 tracer 所需的候选记录和纯不变量。
- 验证多 Slice 历史不覆盖、绑定依据不漂移、Attempt 时间独立、Unknown 不升级为成功。
- 暂不接入当前 Kernel，不迁移旧类，不改变现有 solution 的并行修改。

阶段 2（2026-09-19 用户指示续行：现有手机/UI 滚动—点击 realization 接到 Core seam）：

- 先以独立投影缝验证 Kernel realization 的滚动—点击路径产物可投影为 Core 候选记录；
  优先对照 RealAssetEntityModelTests、FastPerceptionSliceTests、
  ControlReferencePolicyTests、DispatchSeamSpecificationTests 的已验证语义。
- 只提取 Evidence、Segment、Slice、Claim、Effect、Attempt、TargetBinding 所需的
  跨领域语义；UI occurrence、DOM、截图、ADB、浏览器驱动、Runtime、Trace、Harness
  和设备 SDK 留在 realization/adapter。
- Core 候选形状随 realization 接触精化（依据列表化、Attempt↔Binding 方向），不冻结。

## Out of Scope

- 不搬迁 `UniClaw.Kernel` 类型或 UI/设备实现。
- 不实现 Browser、ADB、Robot SDK、OCR、YOLO、Runtime loop、Trace 或持久化。
- 不建立独立 Event 类型，不冻结最终字段、继承树、包名或存储布局。
- 不宣称跨领域公共 API 或生产就绪。

## Decisions

- Core 项目保持无 ProjectReference；领域 payload 和 locator 只以领域无关值进入候选协议。
- Slice 通过不可变记录携带 Segment、观察依据和局部范围；后续 Slice 不修改旧 Slice。
- TargetBinding 固定引用建立绑定时的 Slice 依据；当前 World 改变不自动重写绑定。
- Effect、Attempt、TargetBinding 分开；Attempt 记录实际尝试时间和投递状态。
- Delivery Unknown 只表达投递未知，不产生 World 成功 Claim。
- 本 Change 是 tracer，不是 CORE-001 锁定基线的修订；如发现边界反例，另建 Change。

阶段 3 补充决策（2026-09-19，用户明确要求去掉 Adapter）：

- D8 投影缝归属 `UniClaw.Kernel.Core` realization seam；Kernel 只直接引用
  `UniClaw.Core`，Core 仍保持零 ProjectReference。此处是本 Change 对 RFS-001
  “Kernel 零引用”约束的窄幅依赖闭包修订，不改变 CORE-001 的语义锁定。
- D9 Core 候选精化两处：Slice 观察依据列表化（realization 依据是集合，无单一诚实锚）；
  Attempt↔Binding 方向改为 Attempt.BindingId（与 receipt↔BindingId 方向一致——阶段 1
  review ② 的 realization 证据）。均为候选演化，不构成 CORE-001 锁定边界修改。
- D10 Bind 四态拒绝不产生 canonical binding → 投影无 Core TargetBinding 可产
  （fail-closed）；防重发执法留在 realization EB，Core 候选不重复拥有。
- D11 `CoreSemanticProjection` 只保留一份，迁入 Kernel 的 Core seam；删除
  `UniClaw.CoreAdapter` 项目、源码和解决方案条目。此举是投影 Owner 收口，不是
  旧 UI/设备类迁移，也不冻结最终字段、继承树、包名或存储结构。

## Acceptance

阶段 1（1–8，已验证关闭）：

1. `UniClaw.Core` 在无 UI、设备 SDK、Harness 和 Kernel 引用时独立构建。
2. 测试能表达手机滚动前后同一 Segment 的多个 Slice 和重叠元素引用。
3. 新 Slice 不改变旧 Slice 的历史内容。
4. TargetBinding 固定到建立绑定时的 Slice 依据，不跟随最新 Slice。
5. Attempt 时间与 Slice 观察时间分开。
6. Unknown、Stale、Ambiguous、Unauthorized、Unverified 不互相升级。
7. Receipt/Delivery 状态不直接产生 World Result。
8. 测试只证明行为，不证明最终 API、字段或跨领域生产就绪。

阶段 2（9–15，realization 接线）：

9. 现有手机/UI 滚动—点击 realization（真实 corpus 管线）的 Evidence、Segment、
   Slice、Claim、Effect、Attempt、TargetBinding 可经投影缝表达为 Core 候选记录。
10. 真实滚动—点击 tracer：同一 Segment 多 Slice、Claim Revise 保留旧依据、
    binding basis 固定、Attempt 时间严格独立、点击后新 Evidence 再评价。
11. UnknownOutcome 投影为 Unknown 且不产生 World Result、不改写 belief。
12. canonical binding 随 revision 推进投影为 Stale；gate 授权拒绝投影为
    Unauthorized；四态拒绝无 Core binding 可产。
13. Core 程序集零 realization 依赖，候选记录只携带领域无关值（结构证明）。
14. Kernel 仅直接依赖 Core；Agent 仍仅依赖 Kernel；不存在 CoreAdapter 或第二套投影。
15. 仍不冻结最终字段、继承树、包名、存储结构；不做全量 Kernel/UI 源码迁移。

## Verification

```yaml
verification_phase1:
  level: DETERMINISTIC
  method: dotnet test tests/UniClaw.Core.Tests/UniClaw.Core.Tests.csproj --no-restore
  expected: Core tests all pass; Core project has no ProjectReference
  actual: UniClaw.Core.Tests 3/3 passed; Core project compiled independently; NU1900 vulnerability-cache permission warning did not affect execution.
  evidence: dotnet test tests/UniClaw.Core.Tests/UniClaw.Core.Tests.csproj --no-restore --logger 'console;verbosity=minimal'

verification_phase2:
  level: DETERMINISTIC
  method: |
    dotnet test tests/UniClaw.Core.Tests/UniClaw.Core.Tests.csproj --logger 'console;verbosity=minimal'
    dotnet test tests/UniClaw.Kernel.Tests/UniClaw.Kernel.Tests.csproj --filter "FullyQualifiedName~CoreProjectionSeamTests" --logger 'console;verbosity=minimal'
    dotnet test tests/UniClaw.Kernel.Tests/UniClaw.Kernel.Tests.csproj --filter "FullyQualifiedName~RealAssetEntityModelTests|FullyQualifiedName~FastPerceptionSliceTests|FullyQualifiedName~ControlReferencePolicyTests|FullyQualifiedName~DispatchSeamSpecificationTests" --logger 'console;verbosity=minimal'
    dotnet test tests/UniClaw.Kernel.Tests/UniClaw.Kernel.Tests.csproj --logger 'console;verbosity=minimal'
    dotnet test UniClaw.Kernel.slnx --logger 'console;verbosity=minimal'
  expected: |
    Core.Tests 3/3（候选形状精化后不变量仍成立）；CoreProjectionSeamTests 6/6；
    四个指定 Kernel 行为套件不回归；Kernel 全量不回归；solution 全量
    （基线 514 + 新增 6 = 520）通过。
  actual: |
    Core.Tests 3/3；CoreProjectionSeamTests 6/6；四套件 30/30；Kernel 全量
    388/388；solution 520/520（Agent 17 + Core 3 + Simulation 112 + Kernel 388）。
    Core/Kernel csproj 零 ProjectReference 均未破坏（后者两处命中为注释；
    ProductHostClosureTests 在全量中通过）。剩余缺口见 evidence §剩余缺口。
  evidence: evidence/2026-09-19-core-002-realization-seam.md

verification_phase3:
  level: DETERMINISTIC
  method: |
    dotnet test tests/UniClaw.Core.Tests/UniClaw.Core.Tests.csproj --no-restore --logger 'console;verbosity=minimal'
    dotnet test tests/UniClaw.Kernel.Tests/UniClaw.Kernel.Tests.csproj --no-restore --filter "FullyQualifiedName~CoreProjectionSeamTests|FullyQualifiedName~ProductHostClosureTests" --logger 'console;verbosity=minimal'
    dotnet test UniClaw.Kernel.slnx --no-restore --logger 'console;verbosity=minimal'
  expected: |
    Core 独立测试通过；Kernel realization seam 与依赖闭包通过；全 solution 保持绿灯；
    CoreAdapter 不再出现在解决方案、项目引用或测试引用中。
  actual: |
    Core.Tests 3/3；CoreProjectionSeamTests 6/6；ProductHostClosureTests 3/3；
    solution 520/520 通过（Core 3、Agent 17、Kernel 388、Simulation 112）。Kernel 仅引用 Core，Core 零引用，
    Agent 仍仅引用 Kernel。NU1900 为漏洞缓存目录权限警告，不影响执行。
  evidence: evidence/2026-09-19-core-002-direct-core-path.md
```

## Status log

2026-09-19 · enter→implementing · CORE-001 已锁定；开始独立 Core 第一条 tracer。
2026-09-19 · implementing→verified · Core project and tests created; Core tests 3/3 passed; all non-Canonical binding dispositions fail closed; solution entries added without adding Kernel→Core migration reference.
2026-09-19 · verified→closed · 独立复验（DSH 会话，按 A1 不信记录本身）：UniClaw.Core.Tests 3/3 重跑通过；UniClaw.Core.csproj 零 ProjectReference 属实；slnx 新增条目后全 solution 514/514 绿（期间修复 CORE-001 遗留的 DocsMetadataTests 两处违规，见 CORE-001 state log）。Review 留痕（非阻塞，tracer 级）：①`CoreInvariants.ProducesWorldResult` 为常量谓词（恒 false、参数未用）——文档式不变量不可证伪，下阶段应换结构性证明（无 Attempt→Claim 通路）；②`TargetBinding.AttemptId` 为绑定→尝试方向，与 Kernel 已验证模型的 receipt↔BindingId 方向相反，归入 CORE-001 qspec §9 旧模型映射待验证项；③多 Slice 历史不被改写目前由 record 不可变性承载，未经真实 reconciliation 证明——建议后续 Change 在 Kernel seams 上补端到端 tracer bullet。按本 Change 定义（独立协议 tracer）关闭。
2026-09-19 · closed→implementing（阶段 2 重开）· 用户指示续行：现有手机/UI 滚动—点击 realization 接到 Core seam。范围扩展如 Scope 阶段 2；CORE-001 锁定基线不动（无反例，无需新建变更记录）。
2026-09-19 · implementing→verified（阶段 2）· 新增 src/UniClaw.CoreAdapter（投影缝，Kernel+Core 双引用；遵守 RFS-001 对 Kernel 零 ProjectReference 的执法而不改写并行 Change 边界，故落独立程序集）+ CoreProjectionSeamTests 6 项。Core 候选精化：Slice 依据列表化、Attempt↔Binding 方向对齐 receipt↔BindingId（解决阶段 1 review ②，留痕 D9）。review ① 补了 realization 侧行为证据（reflux 无 revision / 无 attempt.* World claim），Core 侧结构性证明仍欠；review ③ 补了真实 corpus 行为断言（痕迹链 + RevisionHistory），完整回放仍欠。验证：Core 3/3、seam 6/6、四指定套件 30/30、Kernel 388/388、solution 520/520。剩余缺口记录于 evidence/2026-09-19-core-002-realization-seam.md。关闭待 review。
2026-09-19 · verified→implementing（阶段 3）· 用户明确要求最终去掉 CoreAdapter；将唯一投影实现移入 `UniClaw.Kernel.Core`，Kernel 改为仅引用 Core，并同步收窄 ProductHostClosureTests 的允许依赖集合。
2026-09-19 · implementing→verified · 阶段 3 增量与全量验证通过：Core 3/3、CoreProjectionSeamTests 6/6、ProductHostClosureTests 3/3、solution 520/520；无产品/测试/解决方案 CoreAdapter 引用；NU1900 仅为缓存权限警告。
2026-09-19 · verified→closed · 完成本 Change 的直接 Core 路径收口。关闭不代表 UI realization 全量迁移、第二种 realization 已实现，或 Core 最终字段/API 已冻结；这些仍按后续场景反驳与独立 Change 处理。
