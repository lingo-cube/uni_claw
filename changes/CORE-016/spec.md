# CORE-016 spec — 文件系统域 Core 契约 realization tracer v0.2

> 状态: REVIEWED（2026-09-20 spec 评审 CHANGES_REQUIRED 已闭合：
> S6/S8 重写、S3 收紧、ResourceVersion 条件注明；评审闭合条件满足）
> 上游: CORE-016/state.md D1–D5（grill round 1，2026-09-20 全按建议）·
> `docs/design/uiworld-realization-contract-v0.1.md`（契约形态参照）·
> `docs/design/core-extraction-qspec-v0.2.md` §4（字段边界）·
> `src/UniClaw.Core/CoreRecords.cs`（候选基线）
> 检验主张: 「Core 是跨领域核心」——第二个领域按契约实现，Core 候选
> 形状在此过程中**被证伪或被加固**；本 tracer 不修 Core。

## 1. 形态：mini-world + 投影缝（不是直接构造 Core 记录）

与 UI realization 同构、更小规模：

```text
真实临时目录（受控文件操作）
        ↓ 观察（readdir / stat）
FileSystemWorld（自持类型：目录快照、文件身份、观察历史——零 Kernel 类型）
        ↓ FileSystemCoreProjection（唯一投影缝）
UniClaw.Core 记录（Segment/Evidence/Claim/Effect/Slice/Attempt/TargetBinding）
```

直接 new Core 记录只证明「能表达」，不证明「一个领域世界能藏在契约后面
自持 identity」——后者才是 realization 契约 §1 的形态，也是本 tracer 的
架构价值所在（D2）。

## 2. 域映射表（字段级）

| Core 记录 | 文件域语义 | 字段纪律 |
|---|---|---|
| Segment | 目录子树（根路径登记） | B：Id、SemanticKind=`directory-subtree` |
| Slice | 一次列目录窗口快照 | B：Id、SegmentId、ObservationEvidenceIds（readdir evidence 集）、ObservedAt、Scope（路径前缀+模式）、ObservedRecordIds（条目相对路径）、Coverage（捕获条目集说明） |
| EvidenceRecord | readdir / stat 观察 | B：Id、SubjectId、ObservedAt、Source（`readdir`/`stat`）、Context（绝对路径）、ProcessingChain（`["raw-listing"]`/`["stat"]`） |
| Claim | 文件状态命题（exists / size-bytes / last-write） | B：Id、SubjectId、Predicate、Value、Disposition、EvidenceBasis；**不可变——更正只追加新 Claim（见 S3）** |
| Effect | fs 逻辑操作（append / delete / move） | B：Id、Operation、SubjectId |
| Attempt | 一次 fs 操作尝试 | B：Id、EffectId、BindingId、StartedAt、Delivery、DeliveryEvidenceId；**RequestSnapshotId / ExecutorId / AuthorizationBasis / ExecutionState = null（未提供，不伪造）**（D3） |
| TargetBinding | 路径定位 + **非 Slice 固定依据** | B：Id、TargetSegmentId、**BasisSliceId = null**、LocatorKind=`path`、LocatorKey（相对路径）、Disposition；**C 门开启**：BasisReferences = [BasisReference(ReferenceId→stat Evidence, Kind=`ResourceVersion`, SnapshotKey)]。**Disposition 由 realization 投影——含基于当前版本比较的 Stale 判定；Core 不读取文件版本** |
| Clause | 不使用 | 文件域无显式条款买家；不建（NO_REAL_BUYER） |

**ResourceVersion SnapshotKey** = `{Length}:{LastWriteTimeUtcTicks}` 复合键。
**它是测试侧可观察版本依据，不是通用文件版本保证**（mtime 可被显式设置、
同内容重写不必然改变键值）；其语义仅在受控 fixture 范围内成立。fixtures
用 `File.SetLastWriteTimeUtc` 强制区分版本，绕开 mtime 粒度问题——该控制
手段在测试内显式声明，不伪装成自然时间。

**identity minting**：realization 本地、确定性（内容/路径派生或场景内
顺序），CoreId 对 Core 不透明（沿 RunId/EvidenceId 先例精神）。

## 3. 场景集（S1–S8）

| # | 场景 | 检验 | 对应 UI 契约 §7 场景 |
|---|---|---|---|
| S1 | 初始列窗：readdir → Slice + Evidence 固定 | 表达保持（局部观察可表达） | 滚动后局部观察 |
| S2 | 目录增长后重列：新窗不覆盖旧窗，重叠条目两窗并存 | Slice 历史 / 新不覆盖旧 | 多 Slice 重叠历史保留 |
| S3 | stat 变化：**原 Accepted Claim 保持不变；追加**新 Conflict Claim 与新 Accepted Claim，各自携带自己的 EvidenceBasis；不得修改旧 Claim 的 disposition 或依据；更正关系只作可追溯历史关系，不覆盖 Core 历史记录 | 演化保持（更正 append-only） | Occurrence revision-local |
| S4 | 同一文件跨列窗身份保持（FileSystemWorld 自持 identity，非 Core 生成） | realization 自持 identity；Core 不反向生成 | LogicalItem 跨 revision 连续（域对应物） |
| S5 | ResourceVersion 绑定：stat → BasisReferences（非 Slice）→ Canonical | C 门开启 + HasFixedBasis | 非 Slice 固定资源版本不被伪装成 Slice |
| S6 | 当前文件版本（stat）与 Binding 固定 ResourceVersion 不一致时，**文件域的有效性检查必须拒绝该次发送**；旧 Binding 不被修改、不自动换绑；当前使用判断投影为非 `Canonical`（Stale）；**stale 判定完成后**才断言不可发送；`CoreInvariants.CanDispatch` 只负责检查投影后的 Core 约束，**不自动完成文件版本比较** | 有效性判定（freshness）归属 realization；Core 约束只作用于投影结果 | 旧 Binding 不自动换目标 |
| S7 | fs 操作 Unknown 结果（反馈缺席）保持 Unknown；迟到反馈追加不覆盖；无 Receipt/反馈不得产出 Completed/Failed 判断 | Unknown 不变成成败 | Unknown 不变成成败 |
| S8 | **双序等价（限定）**：同一固定目录状态、同一观察输入版本、同一投影规则下，仅交换 world 内部登记与投影调用的顺序；比较**必要语义**一致——world identity、历史引用、Slice/Evidence 依据、Binding 固定依据、当前可发送性、Unknown 保持 Unknown；不比较全部输出对象完全相等。中途发生文件变化 = 不同版本场景，不纳入本等价 | 演化保持（受控双序） | —（qspec §3 演化保持的直接实例化） |

## 4. Core 约束满足检查（双向矩阵第二域复刻）

- **realization → Core**：投影后 CoreInvariants 按文件域事实成立
  （HasFixedBasis / CanDispatch / IsTerminalDelivery）——前提是
  realization 已完成自身有效性判定并投影 Disposition；
- **Core → realization**：Core 不反向生成文件身份（无此类 API 调用面，
  结构性检查：投影类出向仅构造 Core 记录，无回写 world 的方法）；
- 不伪造纪律：无 Receipt/反馈不得产出 Completed/Failed Claim。

## 5. 落点与依赖执法

- 新测试程序集 `tests/UniClaw.FileSystemRealization.Tests`（xUnit）；
  csproj ProjectReference **仅** `UniClaw.Core`（D2/D4）；
- closure 测试：tracer 类型程序集 `GetReferencedAssemblies()` 中
  UniClaw.* 前缀引用的集合 == {UniClaw.Core}（GEV-004 D1 build 层强制、
  RFS-001 D23 closure 先例）；
- 真实临时目录（`Path.GetTempPath()` + 随机子目录，teardown 清理）；
  不引入抽象文件系统库。

## 6. 验证声明（level 预告，实现后按四元组落）

- 记录映射 / 不变量 / closure：DETERMINISTIC；
- S1–S8（真实临时目录、受控 mtime）：SCENARIO。

## 7. Non-goals / 不可外推

- 不修 `UniClaw.Core`（反例落 evidence + qspec 修订建议，走后续 change，D5）；
- 不建 Clause（无买家）、不接 Attempt 执行字段（无真实输入）；
- 文件域 identity 方案不是 UI LogicalItem 的泛化，也不反推 UI；
- SnapshotKey 复合键不是存储格式裁决，也不是通用文件版本保证；
- 本 tracer 通过 ≠ Core 冻结；不触发程序集升格（ADR-0024 条件不变）。

## 8. 验收映射

| Acceptance（state.md） | 承载 |
|---|---|
| 1 表达保持 | S1–S5 + 映射表字段纪律测试 |
| 2 演化保持 | S3 / S7 / S8 |
| 3 ResourceVersion + stale 拒绝 | S5 / S6（realization 有效性判定 → 投影 Stale → CoreInvariants.CanDispatch=false） |
| 4 双向矩阵复刻 | §4 检查族 |
| 5 零 Kernel 编译级 | csproj + closure 测试 |
| 6 零产品代码 / 反例处置 | git 范围 + evidence 纪律 |

## 9. v0.1 → v0.2 变更溯源（spec 评审处置）

| 评审问题 | 处置 | 落点 |
|---|---|---|
| S6 把版本比较误归 `CoreInvariants.CanDispatch`（源码核实：只检 Disposition+HasFixedBasis，不读文件版本） | 重写：有效性判定归 realization，投影 Stale 后才断言不可发送 | §3 S6、§2 TargetBinding 行、§4、§8 |
| S8 双序等价未限定比较对象 | 重写：固定状态/输入/规则三同，仅换序，比必要语义不比全对象；中途变化不入等价 | §3 S8 |
| S3 表述可诱导原地修改 Claim | 收紧：原 Claim 不可变，更正=追加新 Claim + 可追溯关系 | §3 S3、§2 Claim 行 |
| ResourceVersion 键需条件注明 | 注明：测试侧可观察版本依据，非通用文件版本保证 | §2、§7 |
