# EVO-002 — 诊断入链：双域失败触发 + Uniflow 挂点 + run-diagnosis 出口
lifecycle_state: planned · disposition: none · depth: decision-heavy · base: 56a75fd74b5dff72db931edc96e343f0aea6d80b

## Intent（WHAT/WHY）

诊断当前是旁路工具（拉模式、产物无标准出口、流程无挂点），无法融入主流程。
DBG-003 已标准化诊断出口（packet v0）；本 Change 接**入口与挂点**，使失败
信号自动进入演进循环。用户 2026-10-23 定型：**触发分两域** ——
① 开发流程验收测试时出现；② 生产使用诊断工具时出现。两域 adapter 不同，
信号协议与下游 INTAKE 统一。

## Scope（IN）

- **统一信号协议** `uniclaw.evolution.signal.v0`（JSON，落盘约定）：
  `lane(dev-verify|live-run)` / `source(trx|runDir)` / `verdict(failed|
  hash-mismatch|unknown)` / `context(在途变更清单|run 终态)` /
  `preliminaryClass(A-F)` / `ts`。
- **域 A adapter**：测试出口扫描器（TRX + scenario-coverage 输出解析；
  失败时自动附 `git status --porcelain` 在途清单 —— EVO-001 归因教训内建；
  hash-mismatch 单列并自动指认 manifest 与致因路径）。
- **域 B adapter**：run 出口扫描器（facts.json 终态判定 → 信号）；
  run-diagnosis 升级为落 packet 文件（保持 read-only / 非权威姿态，
  产出符合 DBG-003 packet v0）。
- **INTAKE 分诊门**：去重（同失败已有 open case / 已知问题清单）；
  预期红不立案（见 D2）。
- **Uniflow 挂点**：A3 路由具体化（bug 类 RESOLVED 前须有 diagnosis 引用）；
  state.md 模板增 `diagnosis:` 行。
- **evolution-loop skill** INTAKE 段按两 lane 重写（本 Change 为致因变更）。

## Out of Scope（禁止 / 另立）

- runDir→case 重放导出器（域 B 硬缺口，独立 change：EVO-003 候选）。
- 场景/基线晋升生命周期（G13/G14，独立 change）。
- 任何产品 Runtime src 改动；任何自动合并/晋升。
- 不改 diagnosing-bugs / uniclaw-debug-evidence 的边界（DBG-003 已冻结）。
- 不建守护进程/常驻服务：扫描器是运行出口处的确定性脚本/工具调用。

## Decisions

- D1 **两域一协议**：adapter 按 lane 分，信号 schema 与 INTAKE 统一；
  信号是文件不是事件流（无新基础设施）。
- D2 **预期红/意外红分诊（默认规则，待 Owner 确认）**：调用方显式声明
  intent —— TDD/PROVE 阶段的红 = 预期红不立案；full/回归跑失败 = 意外红
  触发立案；hash-mismatch = 资产漂移类，直接指认漏签 manifest。
- D3 **域 A 信号必含在途变更清单**（EVO-001 2026-10-23 归因修正的教训：
  无此字段则诊断会把在途会话中途状态误判为主树既有红）。
- D4 **域 B 两步**：终态判定自动化（无模型）；深诊断人发起，产 packet
  落盘（DBG-003），两者可独立存在。
- D5 run-diagnosis 保持 read-only posture；只新增"写 packet 文件到
  evidence 区"，不写 run 目录、不改 Runtime 状态。
- D6 **PROVE 按需触发验证层**（用户 2026-10-23 定型）：候选修复的验证
  按影响面选起始层、按需升级（六层阶梯见 evolution-loop
  `references/verification-ladder.md`）；不盲目全量；L5 模拟器/真实模型
  仅在需要 live 证据时进入，同一循环连续多轮共享一次 boot+预热 lease
  （复用 `tools/android/start|stop-test-device.sh` 既有 supervisor 机制，
  不新建会话管理）。触发器侧信号不携带层信息——选层是 PROVE 的职责。
- D7 L4 全量回归属 REVIEW/VERIFY 或 gate 要求，不属单轮 PROVE 默认成本。

## Acceptance（草案，IMPLEMENT 前细化）

| ID | method | expected | actual | evidence |
|---|---|---|---|---|
| A1 | 信号协议 + 两 adapter 单测/干跑 | dev-verify 与 live-run 各产一份合法 signal v0；域 A 信号含在途清单 | TBD | — |
| A2 | 用 EVO-001 cycle-01 的 red.trx 作域 A 真实输入 | 解析出 failed + preliminaryClass + reproduce 候选（test filter） | TBD | — |
| A3 | 用既有 runDir（evidence 下真实 run）作域 B 输入 | 终态判定正确产信号；hash-mismatch 类能指认 PopupReplay manifest 漏签场景（在途态复演） | TBD | — |
| A4 | run-diagnosis 干跑 | 产出 packet v0 文件落 evidence；read-only 姿态不变 | TBD | — |
| A5 | Uniflow 挂点 | A3 具体化文本 + state.md `diagnosis:` 行进入模板；bug 类 RESOLVED 门槛可检查 | TBD | — |
| A6 | 分诊门 | 同一失败重复到达只立一次案；预期红不立案；已知问题归档 | TBD | — |
| A7 | 边界 | 无产品 src 改动；无守护进程；无自动合并/晋升 | TBD | — |

## Constraints

- 扫描器为确定性脚本（无模型调用）；分诊去重用文件对照，不建数据库。
- 与在途 WORLD-VISUALIZATION-001 会话工作区隔离：只读其状态做在途清单，
  不接管其变更。
- 全部运行命令遵守 `-m:1 -nr:false`（DSH 沙箱约束，摩擦台账 #3 相关）。

## Assumptions / Alternatives

- 假设 TRX 与 facts.json 的失败面足以判定 verdict；不解析 console 自由文本。
- 被拒：常驻 watcher/事件总线（无真实 buyer，违反不预造）。
- 被拒：把分诊交给模型（确定性可判的不消耗模型）。
- 被拒：域 B 强制全自动化深诊断（模型成本与非权威边界）。

## Owner / Authority impact

- 信号与分诊不裁决"现实为真"：只决定"是否值得立案"。
- 立案仍走 Uniflow PERSIST；修复走 IMPLEMENT；合并/晋升仍 Human Gate。
- run-diagnosis 产物 authority 恒 non-authoritative（DBG-003）。

## Residual risks

- intent 声明依赖调用方自觉；误报（TDD 红被立案）需去重兜底。
- 域 B 无导出器期间，live 信号只能立案为"待复现"case（reproduce 空）。
- 在途清单只含本仓 git 状态；跨仓/跨会话干扰仍可能漏。

## Verification

```yaml
verification:
  level: SCENARIO
  method: 两域各一次真实输入干跑（EVO-001 red.trx + 既有 runDir）+ 分诊门单测 + 挂点核对
  expected: A1–A7
  actual: TBD（planned，未实施）
  evidence: changes/EVO-002/evidence/
```

## Status log

- 2026-10-23 UNDERSTAND→RESOLVE：用户定型触发双域（开发验收测试 / 生产
  诊断工具）；与 DBG-003（出口）、EVO-001（循环主体）拼成完整入链。
- 2026-10-23 PERSIST→PLAN：落本 state；实施前待 Owner 确认 D2 默认分诊
  规则与 A7 边界。
