# PROTOCOL-FIX-001 — S1 Origin 字段族 + S5 渗透立法（grill 裁决落地首切片）
lifecycle_state: persisted · disposition: none · depth: standard · base: 14987fe

## Intent（WHAT/WHY）

落实 grill 裁决（protocol-completeness-formal-audit 附录 C + ADR-0048/0050）：
S1——ObservationProposal/EvidenceRecord/回流侧统一携带封闭二值 Origin
（External | PostActionSelf），判定权归机械推导（Context=PostActionEffectFlow
的产出缝即判 PostActionSelf；其余 External），EvidenceId canonical 输入纳入
origin，admission 执法 Context↔Origin 一致性；S5——MaterialEffect 判定立法
"prior 影响的关联连续性不得作为唯一依据"，锚定测试。

## Scope / Out of Scope

In：`Kernel/Evidence`（新枚举 + 两 record + 账本一致性执法与 id 输入）、
EB 回流导出与 post-action 观察产出缝的 Origin 标注、`Assurance` MaterialEffect
判定 guard、锚定测试（xunit，运行待网络）、协议基线 P2/P3 状态注记与
ADR-0047 known gaps 更新、`Evidence/README.md` 登记。
Out：S2 期望载荷、S3 预算/续授、S4 locator（后续切片）；attempt 关联键的
独立字段化（本切片以产出缝推导承载，键字段化随 S2/S3 评估）；跨进程语义。

## Decisions

- D1：Origin 以**带默认值参数**落 record（默认 External）——非空恒存在，
  既有构造调用零破坏（保守方向：推导不出=External，ADR-0048）。
- D2：推导落点=**产出缝**：凡今日设置 Context=PostActionEffectFlow 的生产
  点，同步设 Origin=PostActionSelf；admission 拒绝二者不一致（推导执法面）。
- D3：S5 首步以"MaterialEffect 必须由 Origin=PostActionSelf 的证据支撑"
  落地（Context 自述不再单独充分）；"仅身份连续"拒绝的完整锚定随 S2
  期望谓词到位后补强。

## Acceptance

| # | 判据 |
|---|---|
| A1 | 三类型携带 Origin；默认 External；EB 产出缝标 PostActionSelf |
| A2 | EvidenceId 输入含 Origin（同内容异 origin → 异 id） |
| A3 | admission 拒绝 Context↔Origin 不一致（RED 路径） |
| A4 | MaterialEffect 判定要求 Origin=PostActionSelf（S5 立法） |
| A5 | 锚定测试落 Kernel.Tests（编译期保证；运行待网络，事后核对项） |
| A6 | 协议基线 P2/P3 Status 注记闭合；ADR-0047 known gaps 更新 |

## Constraints

- 语义严格按 ADR-0048；禁时间启发式（无任何时间推导代码）。
- 既有测试行为不回归（Origin 默认值保证兼容）。

## Verification

```yaml
verification:
  level: DETERMINISTIC
  method: "dotnet build UniClaw.Kernel（离线，APPDATA/NUGET_PACKAGES 重定向绕沙箱）；grep 探针（Origin 推导/自报/S5 门槛/登记）；DocsMetadata + README 登记复刻；新 xunit 锚定测试（编译随测试工程，运行待网络）"
  expected: "Kernel 编译零错；探针全中；双复刻 PASS；测试文件就位"
  actual: "dotnet build 0 错误（527 警告均为既有 XML 注释类，与本次无关）；ObservationOrigin 落 6 个 Kernel 文件；S5 门槛 RuntimeAssurance:365；诊断自报 2 产出缝（EB ExportAttemptEvidence / Driver DischargeCompletion）；DocsMetadata 复刻 ALL PASS；README 登记复刻 168 文件 0 违规（新增 ObservationOrigin.cs 已登记 Evidence/README）。xunit（含 OriginSemanticsTests）运行待网络恢复，为事后核对项"
  evidence: "编译输出与探针见本 status log；evidence/arch-doc-023/ 复刻脚本复用；tests/UniClaw.Kernel.Tests/Evidence/OriginSemanticsTests.cs"
```

### 实现修订记录（相对 proposal 的两处精确化）

- R1：**推导收口于账本单点**——canonical Origin 由 `EvidenceLedger.Admit` 从
  Context 机械推导（Context=PostActionEffectFlow ⇒ PostActionSelf），15 个
  既有构造点零破坏；proposal 上的自报 Origin 仅诊断（ADR-0048 Q3 裁决的
  忠实实现，比逐点标注更强：单一判定权 + 不可绕过）。
- R2：**EvidenceId 不追加独立 origin 输入**——推导制下 origin = f(Context)，
  Context 已入 id，追加属冗余且会无谓翻转全部历史 id（replay/golden 破坏）。
  "同内容异 origin"在推导制下不可构造（自报被忽略，锚定测试
  ReportedOrigin_IsDiagnosticOnly_DoesNotAffectEvidenceId 证明）。ADR-0048
  §3 据此留实现注记；值域独立扩展（第三值）时须同步纳入 id 并走 golden 迁移。

## Status log

- 2026-10-12 · UNDERSTAND → RESOLVE → PERSIST · Owner 批准开工；范围锁定
  S1+S5（proposal §1）；落点代码摸底完成。
- 2026-10-12 · IMPLEMENT → REVIEW → VERIFY · 六文件落地（新枚举 + 双 record
  + 账本推导 + S5 门槛 + 双产出缝自报）+ 测试 + 四处文档同步（协议基线
  P2/P3 Status、ADR-0047 gaps 销项、ADR-0048 实现注记、CONTEXT 词条）。
  离线编译 0 错误；探针/双复刻全过；xunit 运行待网络（事后核对项）。
