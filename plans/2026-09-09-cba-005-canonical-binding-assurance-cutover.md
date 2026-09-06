# Plan — CBA-005 Canonical-Binding Assurance Cutover

> PlanType: cba-005-canonical-binding-assurance-cutover / Status: ADOPTED /
> References: changes/CBA-005/state.md · ADR-0009 ·
> docs/architecture/protocols/inter-component-protocol-baseline-l1-l3.md（§5、§4-13）

## 垂直切片：一条 invariant 贯穿三个 Authority

```text
Effect Boundary 决定「操作哪个目标」
        ↓  Bind(intent, candidate, current)            （sole Canonical Binding Authority）
CanonicalBinding（拒绝四态：stale / ambiguous / unknown-target / no-candidate）
        ↓  Assurance.Judge(intent, canonical, view, current)   （sole Runtime Assurance Judgment Authority）
AssuranceJudgment（三元组 IntentId + BindingId + RevisionId=Binding.RevisionId）
        ↓  Effect Gate → Dispatch                       （只执法既有 judgment）
judgment 拒绝 → Gate 执法并拒绝（not-authorized）；binding validity 不变
```

## Before / After

### Before（C2E-002 realization，ADR-0009 known deviation）

- `Judge(ControlIntent, CandidateBinding?, View, Current)` —— candidate 在
  Assurance 输入签名内；`binding-sufficient` 检查 candidate 非 null
- `AssuranceJudgment(IntentId, IsAdmissible, Checks, RejectionReason)` —— 单键
- Act 组合序：Judge → Bind → Dispatch；judgment 拒绝即跳过 Bind/Gate
- `Bind` 对 null candidate 抛 ArgumentNullException

### After

| 文件 | 变化 | Owner |
|---|---|---|
| `Assurance/AssuranceJudgment.cs` | record 增 `BindingId` + `RevisionId`（位于 IntentId 后） | Assurance |
| `Assurance/RuntimeAssurance.cs` | `Judge` 第二参 `CandidateBinding?` → `CanonicalBinding`；删 `binding-sufficient`；增 `binding-intent-correlation`（binding.IntentId == intent.IntentId）与 `binding-revision-currentness`（binding.RevisionId == current.RevisionId）；judgment 携带三元组 | Assurance |
| `Effects/TargetBinding.cs` | `BindingRejectionReason` 增 `NoCandidate` | Effect Boundary |
| `Effects/EffectBoundary.cs` | `Bind` 接受 null candidate → `BindingDecision(null, NoCandidate)`（不再抛）；`Dispatch` 在 `judgment-binding-mismatch`（IntentId）后增 `judgment-binding-id-mismatch`（BindingId）与 `judgment-revision-mismatch`（RevisionId） | Effect Boundary |
| `UniKernel.cs` | `Act` 重排：Bind → (拒绝即短路：Judgment/Gate/Receipt/Reflux 全 null) → Judge → **无条件进 Gate**（judgment 拒绝也执法并拒绝）→ receipt 回流不变；`ActResult.Judgment` 改可空 | Uni Kernel |
| `tests/.../ControlToEffectTests.cs` | Accepted2/3/4/10 迁移 + Accepted7 增反射负向 + 新增 PressureScenario13 | —— |
| `tests/.../TerminalOutcomeTests.cs` | L442/L451 AssuranceJudgment ctor 适配 ×2（行为断言不变） | —— |

## 关键语义（固化，防漂移）

1. **三元组 = "审的是这个 binding"**：RevisionId 记 Binding.RevisionId；
   与 current 相符由 `binding-revision-currentness` check 验证（D4）。
2. **validity ≠ authorization**：judgment 拒绝不使 binding 失效；binding
   存在不构成授权（ADR-0009）。Gate 对 rejected judgment 执行 enforcement
   并拒绝（D6）。
3. **短路语义**：Bind 四态拒绝 → 后续阶段零副作用（无 judgment、无
   gate、无 dispatch）；judgment 阶段之后的拒绝由 Gate 执法表达，effect
   副作用为零，decision 留痕非副作用（D1）。
4. **null binding = API contract violation**：Judge 对 null canonical 抛
   ArgumentNullException（不产生 judgment），不设恒真检查（D3）。

## TDD 次序（RED → GREEN → REVIEW → VERIFY）

1. RED：目标签名就位（AssuranceJudgment 三元组、Judge 新签名、Bind
   no-candidate、Gate 两检查——新路径抛 NotImplementedException / 未实
   现检查）；迁移 Accepted2/3/4/10 + OUT ×2 + 新增 PressureScenario13
   （两 case）+ Accepted7 反射负向；运行可见关键场景失败
2. GREEN：按 After 表最小实现；E2B 冻结面零触碰
3. REVIEW：fresh SubAgent——轴：三元组 invariant（产者侧 + Gate 侧）/
   短路语义（Bind 四态）／C2E 上层 invariants 逐条／E2B 冻结 diff /
   意外改动扫描
4. VERIFY：全量 `dotnet test`；`git diff src/UniClaw.Kernel/Evidence
   src/UniClaw.Kernel/World tests/.../EvidenceToBeliefTests.cs` EMPTY；
   迁移台账核对；evidence 落盘

## 验收 ↔ 用例映射

| 验收 | 用例 |
|---|---|
| 1 语义阶段序 | Accepted2（迁移：四 log 留痕 + 次序）+ Bind 拒绝短路断言 |
| 2 Candidate 退出 | Accepted7（增反射负向：Judge 参数图无 CandidateBinding） |
| 3 三元组 + 产者侧 correlation | PressureScenario13 前置 + Judge 直调用例（binding.IntentId ≠ intent → 拒 `binding-intent-correlation`） |
| 4 Gate 两 case | PressureScenario13_WrongBindingId / _WrongRevisionId（独立两 Fact） |
| 5 Bind 四态短路 | Accepted3（迁移：Judgment==null）+ no-candidate 新断言 |
| 6 Gate 只执法 + validity 不变 | Accepted2（迁移：Gate 执法拒绝 + IsBindingValid 保持）+ Accepted4 |
| 7 E2B 冻结 | EvidenceToBeliefTests 零改动 + VERIFY diff |
| 8 OUT/GEV 回归 | TerminalOutcomeTests ctor 适配后 18 GREEN；Agent 17 零改动 |
| 9 no-blind-retry + currentness | Accepted10（迁移：Bind 后 Judge 直调，拒 `no-blind-retry`） |
| 10 全量 + 台账 | dotnet test 全 GREEN；迁移台账（Accepted2/3/4/10、OUT×2、新增 ×3）入 state.md |

## 迁移台账（验收 10 留痕，CLOSED 时核对）

```text
迁移：C2E Accepted2（拒绝路径重写：binding 存在 + Gate 执法拒绝 + validity 不变）
迁移：C2E Accepted3（stale：Judgment==null；"judgment 先过"注释删除）
迁移：C2E Accepted4（ctor ×3 适配）
迁移：C2E Accepted7（增反射负向断言）
迁移：C2E Accepted10（assurance.Judge 直调改 Bind→Judge）
适配：OUT L442/L451（ctor ×2，行为断言不变）
新增：PressureScenario13_WrongBindingIdRejected
新增：PressureScenario13_WrongRevisionIdRejected
新增：no-candidate Bind 拒绝断言（Accepted3 内或独立）
零改动：E2B 8 · GEV 17
```
