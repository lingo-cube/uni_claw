# ABG-001 — Minimal Bundle v0 Asset Governance & Baseline Promotion（Phase 3 治理半边）

lifecycle_state: closed · disposition: none · depth: decision-heavy · base: 40f190c6af6a3143b48e576a8bf4051e1edc2e26 · pin: 40f190c6af6a3143b48e576a8bf4051e1edc2e26 (2026-09-13) · Human closure 2026-09-19（docket: evidence/2026-09-16-abg001-uap001-human-review-docket.md）

## Intent（WHAT/WHY）

**WHAT**：用 Phase 1 已有 Minimal Scenario Bundle v0 资产，建一条最小资产治理
tracer：concrete filesystem store + 四类责任分离（Capture Producer / Asset
Governance Authority / Scenario Assertion Owner / Human Promotion Authority）
+ identity/lineage/关键 metadata 可检索 + fail-closed 完整性门 + Baseline
显式晋升与版本不可覆盖。

**WHY**：roadmap §7.2.1/§7.3（H7 已 Human-selected）：Capture→Register→
Replay-Eligible→Baseline-Promoted 的最小闭环未落地；当前 bundle 校验
（RFS-001 MinimalScenarioBundle.Verify）证明了 per-bundle integrity，但没有
治理主体分离、没有跨 bundle 的 identity/lineage 检索、没有 Baseline 晋升
纪律。Verification Lane 缺这半边就无法把「一次 replay green」升级为「可治理
的可重放基线」。

## 上游已锁语义（不重锁，只执行）

- roadmap §7.2.1：Content Identity 只由 bytes 计算；Run/Environment metadata
  描述产生语境；Compatibility Judgment 独立给出 Exact/Compatible/Incompatible/
  Unknown；关键字段缺失 → Unknown，不得自动推断兼容。
- roadmap §7.2.1 责任四分：Capture Producer 只拥有 bytes 与原始 provenance；
  Asset Governance Authority 拥有 identity/manifest/lineage/lifecycle records；
  Scenario Assertion Owner 拥有 GT/expected/compatibility claims；Human
  Promotion Authority 决定 Baseline。早期可共用物理存储或工具，但 authority
  records 不合并。
- roadmap §7.3：晋升条件——hash/lineage 完整、scenario 不逐 cycle 指挥、
  assertions 经 Human 审阅、同版本 replay 两遍 digest 一致、Trace 三臂一致、
  fault/timeout/duplicate/stale 有明确预期、版本可追踪。Baseline 更新不覆盖：
  断言变化必须新版本（v2），旧版保留。
- 协议 P26 / baseline §24.9：sealed Trace 只能经 ScenarioImporter 派生
  ScenarioStimulus；Trace Event 永不是 command。
- H8：完整 Matrix/compatibility/migration/tombstone/retention/redaction/
  registry 延后，不阻塞本 tracer。

## Scope

- `tests/UniClaw.Simulation.Tests/` 新增治理层（全部 internal/test-side，
  产品程序集零接触）：
  - concrete filesystem `FileSystemGovernanceStore`：capture 注册（bytes + 原始
    provenance）→ 内容寻址 identity；同内容的不同环境/lineage 为独立 occurrence
    registry records，rename 同语境幂等；bundle 注册保存完整序列化内容而非
    digest 文本；按目标 UI system 版本 /
    app build / Runtime artifact hash / lineage parent 检索
  - 四责任角色最小面：CaptureProducer / AssetGovernanceAuthority /
    ScenarioAssertionOwner / HumanPromotionAuthority（能力分离可断言）
  - `BaselineRegistry`：显式 Human 晋升记录；assertion 修改 → 新版本 v2；
    v1 不可覆盖、仍可解析
  - fail-closed 门：缺 bytes / 错 hash / 断 lineage / 错 schema
  - sealed Trace 仅经 ScenarioImporter 变成 ScenarioStimulus；Quarantined Trace
    可作为 opaque 诊断 bytes 保管，但不得获得 Stimulus/Baseline 语义
- 测试：改名不变性（path ≠ identity）、关键 metadata 检索、四类 fail-closed、
  晋升纪律、v2 演进、authority 越权拒绝、（复用既有）两遍 digest/三臂一致性
  作为晋升前置证据输入
- 更新本 state、status log、verification 四元组

## Out of Scope

- 在线 registry、兼容矩阵（Exact/Range/Matrix 维度展开）、自动 migration、
  tombstone、retention/redaction、在线治理（H8 延后项）
- 任何产品程序集修改；任何新公共 Interface（含 Ixxx）
- 把治理层升格为产品 authority 或改 Owner/Authority 语义
- Trace Writer 修改（TRW-001 已 closed，五项 follow-ups 另立）
- 提交 commit；未经 Human Review 不得自封 CLOSED

## Decisions

| # | 决策 | 状态 |
|---|---|---|
| D1 | 治理层全部 test-side（tests/UniClaw.Simulation.Tests），concrete filesystem store，路径注入；产品闭包零接触（closure 测试继续执法） | implemented |
| D2 | Content Identity = 实际 bytes SHA-256；bundle 保存完整序列化 JSON bytes（非 digest 文本）；同 bytes 同语境/lineage 幂等，跨环境/lineage 形成新 occurrence（identity 不变）；path 只是 hint | implemented |
| D3 | UI system 版本 / app build / Runtime artifact / lineage 可按 occurrence 索引；三个版本字段缺失统一显式 Unknown 查询，Bundle index 必须与 Bundle 内嵌 target/runtime identity 一致 | implemented |
| D4 | 四责任为 test-side 能力面：CaptureProducer 交 bytes+provenance；Governance 管 identity/manifest/lineage；AssertionOwner 管 claims；HumanPromotionAuthority 显式晋升。组合根不暴露 Store，私有 baseline 写入仅由嵌套 promoter 可调用；此为测试侧能力约束，不声称现实 Human 身份认证 | implemented |
| D5 | 晋升门：bundle bytes/digest、scenario、claim/expected/reviewer、完整祖先 lineage 与同源真实两跑 digest 均核对；PerCycleZero/TraceArmsEquivalent 仍是已披露的声明式证据位；缺失/失配拒绝 | implemented |
| D6 | 断言实际变化必须新 Bundle version（v2）+ 新 claim/supersession + 新 promotion record；旧 v1 保留且读侧校验 seal，失败写入不得暴露未持久化版本 | implemented |
| D7 | fail-closed 统一 typed reason（缺 bytes/错 hash/断 lineage/错 schema 各自首因），不降级、不猜测 | implemented |
| D8 | P26 的唯一派生入口是 ScenarioImporter.Derive/DeriveFromPersisted；opaque Trace bytes 可作诊断 capture 保管，不得作为 Stimulus 或 Bundle/Claim 晋升输入；治理 store 不消费 Trace Event | implemented |

## Alternatives

1. **治理层入产品程序集**：拒绝——治理是 Verification/Harness 责任；产品闭包
   禁 Simulation/Importer 功能（baseline §24.8）。
2. **数据库/在线 registry**：拒绝——H8 明确延后；filesystem concrete store
   即最小 buyer。
3. **单一大 store 类型合并四责任**：拒绝——roadmap §7.2.1 明示 authority
   records 不合并；合并会让「谁可以晋升」不可断言。
4. **推广义 metadata 进 content hash**：拒绝——Content Identity 只由 bytes
   计算（7.2.1 铁律），metadata 是 registry 维度。

## Owner-Authority impact

- 零产品 Authority 变更；治理四责任全部 test-side/Harness 层。
- Bundle/Trace/Importer 既有 owner 不变；治理层只消费与记录，不写 Runtime。

## Acceptance

1. 改名不变性：同一 bytes/语境以不同文件名/路径注册 → 同一 identity，二次
   注册幂等；不同环境/lineage 是独立 occurrence、仍同一 content identity。
2. 关键字段可检索：按 UI system 版本 / app build / Runtime artifact hash /
   lineage parent 各自可查；缺失字段返回显式 Unknown 而非推断。
3. fail-closed 四臂：缺 bytes / 错 hash（篡改）/ 断 lineage（parent 未注册）/
   错 schema（未知 schemaVersion）→ 各自 typed 首因拒绝；Bundle 后续 asset
   缺失或 registry 发布失败也不得留下部分 registry identity。
4. Baseline 只能显式晋升：无 Human 晋升调用 → 无 baseline 记录；晋升前置
   证据缺失、scenario/bundle/claim/digest 来源失配、祖先 lineage 不完整 → 拒绝。
5. 断言实际修改 → 新 Bundle version + v2：promotion 记录链 v1→v2；v1 仍
   可解析且 seal 正确；覆盖式更新与失败写入导致的假 v2 均被拒绝。
6. authority 越权拒绝：CaptureProducer/AssertionOwner/Governance 任一方直接
   晋升 baseline → 无路径或拒绝。
7. sealed Trace 只经 Importer 派生 Stimulus：未封存/Quarantined Trace 不能
   导入或消费为 Stimulus；opaque 诊断 bytes 允许内容寻址保管，但不得挂 claim
   或进入 Baseline。
8. 治理层零产品接触：ProductHostClosureTests 继续 GREEN；无新公共类型。
9. 全量回归 GREEN + git diff --check + 精确路径 status；完成后提交 Human
   Review，不自封 CLOSED。

## Constraints

- TDD（RED→GREEN）；本轮 Human Review 缺陷由 Leader 直接修复。
- 复用 Phase 1 bundle/资产（golden-run 家族）为治理对象，不造新资产域。
- 不改并行 dirty；不提交 commit。

## Residual risks

- **治理层为单机 filesystem realization**：并发写、多进程锁、网络分区等
  不在模型内；H8 全部延后项（在线 registry/矩阵/迁移/tombstone/retention/
  redaction）仍未做。
- **Compatibility Judgment 尚未成独立面**：检索返回记录集合，未输出
  Exact/Compatible/Incompatible/Unknown 判定对象（7.2.1 的判定半边留待
  真实兼容矩阵 buyer）。
- **部分晋升证据仍为自报字段**：两遍 digest 已由晋升侧同源重跑核对，但
  per-cycle-zero/trace-arms 标志仍是 boolean 自报；fault/timeout/duplicate/
  stale 的预期与 runner schema/兼容范围完整绑定仍待后续 evidence artifact。
- **单机文件系统提交边界**：Bundle registry 单文件发布，发布失败回滚内存
  identity/occurrence；之前写出的 blob 可成为不在 registry 的孤儿文件。
  claim/index 多文件及 fsync 级 crash durability 尚未建立，不宣称事务原子。
- **重驱动环境依赖**：已证明 Bundle 可从治理库重载并在当前仓库 fixture 可用时
  得到同一 semantic digest；现有 ScenarioRunner 的资产读取仍指向 Phase 1
  golden 路径，尚未证明脱离该 fixture 的独立移植式 replay。
- **Human 代理边界**：test-side 显式 `HumanPromotionAuthority` 调用代表 Human
  决策，不是身份认证或生产授权机制。
- **claim/baseline id 顺序分配**：单 store 内确定性，跨 store 合并语义未定义。
- **并行 dirty（Phase 5 痕迹：PostActionEffectVerification 等）非本 change**，
  未触碰；治层与 MinimalScenarioBundle 的 AppBuild sentinel 语义对齐依赖
  RFS-001 既有决定。

## Verification

```yaml
verification:
  level: CONTRACT + DETERMINISTIC + SCENARIO
  method: >
    三测试项目全量回归 + ABG-001 二十项专项测试（原七项 + 修复回归十三项）+
    git diff --check + 三个未跟踪 ABG 文件逐一 git diff --no-index --check
    /dev/null <path> + 精确路径 status
  expected: >
    Acceptance 1–9：内容 identity 与 occurrence 分离；完整 bundle bytes 可读取；
    关键 metadata 可检索且 Unknown 不推断；输入与 registry 发布失败不留下
    部分注册 identity；Baseline 仅显式晋升、同源重跑与 claim/scenario/lineage
    对齐；真实断言变化形成新 Bundle/v2、旧版仍可读且 seal 正确；opaque Trace
    不获得 Stimulus/Baseline 语义；产品闭包零接触；全绿
  actual: >
    2026-09-14 第一手：Kernel.Tests 382/382、Agent.Tests 17/17、
    Simulation.Tests 94/94（合计 493/493；ABG 20/20）；新增反例覆盖
    late-missing-asset、registry publish failure、full bundle bytes、
    cross-environment occurrence、Unknown OS/runtime、wrong indexed version、
    scenario/digest spoof、transitive lineage、真实 v2 assertion、failed v2
    publish、sealed baseline tamper read、failed claim index publish、
    corrupted-blob idempotent re-registration、persisted bundle reload/re-drive；
    git diff --check 与 ABG 未跟踪文件 no-index whitespace check 均无告警；
    ProductHostClosureTests 所在 Kernel 套件 GREEN；无产品文件改动
  evidence: >
    可复现命令：dotnet test tests/UniClaw.Kernel.Tests --no-restore；
    dotnet test tests/UniClaw.Agent.Tests --no-restore；dotnet test
    tests/UniClaw.Simulation.Tests --no-restore；git diff --check；
    git diff --no-index --check /dev/null <ABG-file>（每个未跟踪文件）。
    RED→GREEN 的失败/通过输出本轮已观测；NU1900 为 NuGet 漏洞缓存读权限
    warning，三个套件退出码均为 0
```

## Status log

- 2026-09-13 · enter→resolving · Human 授权：TRW-001 closed（APPROVED_WITH_
  FOLLOWUPS）后开启 Phase 3 治理半边 ABG-001；目标/边界如上（四责任分离、
  改名不变、可检索、四臂 fail-closed、显式晋升、v2 不可覆盖、Importer 唯一
  入口、concrete filesystem store、无新公共接口、H8 项全部 out of scope）；
  完成后全量测试 + Human Review，不得自封 CLOSED。D1–D8 候选决策与 4 项被拒
  替代落档。Verification Lane 在本 change 后闭合到可用程度；下一站 Phase 4
  （统一异步 Perception：observation/capture correlation、迟到 Slow 隔离、
  失败语义），不跳 Phase 6 接口冻结
- 2026-09-13 · implementing·delegated · Agent-7 完成治理层（Leader REVIEW 通过）：tests/UniClaw.Simulation.Tests/AssetGovernance.cs（四 authority + BaselineRegistry + FileSystemGovernanceStore：内容寻址 blob、registry/claims/baselines JSON、schemaVersion 门、temp+move 原子写、固定虚拟时钟）+ AssetGovernanceTests 七项。抽查确认：晋升门 digest 双跑核对（digest-run1/run2/inequality 首因）、v1 seal 不变量（supersededBy 链指针不入 seal、AssertNoOverlay 复核、baseline-version-exists 覆盖拒绝）。deviation 采纳：ScenarioId 链键必要；Unknown 查询语义显式
- 2026-09-13 · review·SUBMITTED-TO-HUMAN · Leader 自验：382/382 + 17/17 + 81/81（共 480）第一手复跑；git diff --check 干净；验收 1–9 全有具名证据。按 Human 指示提交 Review、不自封 CLOSED；residual risks（compatibility judgment 半边、自报证据字段、H8 延后项）已落档
- 2026-09-14 · review→implement · Human Review 结论 REQUEST_CHANGES：状态字段不合规范、Scope 类型名不一致；Bundle 批注册部分写、仅存 digest 文本、晋升证据不关联对象、同内容跨环境 metadata 丢失、Trace opaque 边界表述冲突、Store 暴露未校验 baseline 写入路径。Human 指示直接修复；依 UniFlow 失败边回 IMPLEMENT，不自封 CLOSED
- 2026-09-14 · implemented·SUBMITTED-FOR-HUMAN-RE-REVIEW · 十三项修复回归先 RED 后 GREEN；Bundle 先全量校验后单次 registry 发布（故障回滚内存），完整 bundle JSON 内容寻址并重载/re-drive；同内容多 occurrence；晋升核对 SourceBundle/claim/scenario/祖先 lineage 并同源重跑两遍；断言实际改变配新 Bundle version；私有 baseline 写入、组合根不暴露 Store；v2/claim 失败发布不暴露未持久记录；Opaque Trace 边界澄清。三套件 382+17+94=493 全绿；仅三条 ABG 路径修改，未提交 commit，等待 Human re-review
- 2026-09-19 · closing · Human 经 `evidence/2026-09-16-abg001-uap001-human-review-docket.md` 裁决批准关闭；三套件复跑 511/511（Kernel 382 + Agent 17 + Simulation 112）无回归；residual（H8 延后项、Compatibility Judgment 半边、自报证据字段、双实现合一）按 Out of Scope 归后续 change
