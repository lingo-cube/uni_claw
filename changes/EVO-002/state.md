# EVO-002 — 诊断入链：双域失败触发 + Uniflow 挂点 + run-diagnosis 出口
lifecycle_state: closed · disposition: none · depth: decision-heavy · base: 56a75fd74b5dff72db931edc96e343f0aea6d80b

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
  run-diagnosis 升级为落 packet 文件（只读运行数据，工件落盘声明local-write，保持非权威，
  产出符合 DBG-003 packet v0）。
- **INTAKE 分诊门**：去重（同失败已有 open case / 已知问题清单）；
  预期红不立案（见 D2）。
- **Uniflow 挂点**：A3 路由具体化（bug 类 RESOLVED 前须有 diagnosis 引用）；
  state.md 模板增 `diagnosis:` 行。
- **evolution-loop skill** INTAKE 段按两 lane 重写（本 Change 为致因变更）。

## Out of Scope（禁止 / 另立）

- runDir→case 重放导出器（域 B 硬缺口，独立 change：EVO-003 候选）。
- 场景/基线晋升生命周期（G13/G14，独立 change）。
- 产品 Kernel/Runtime 状态或执行权威改动；任何自动合并/晋升。
- 2026-10-23用户明确允许仅修改现有Harness工具适配与对应测试，限定RuntimeToolHost/诊断包codec/HTTP结果投影；不扩到产品领域Owner。
- 不改 diagnosing-bugs / uniclaw-debug-evidence 的边界（DBG-003 已冻结）。
- 不建守护进程/常驻服务：扫描器是运行出口处的确定性脚本/工具调用。

## Decisions

- D1 **两域一协议**：adapter 按 lane 分，信号 schema 与 INTAKE 统一；
  信号是文件不是事件流（无新基础设施）。
- D2 **预期红/意外红分诊（本次继续实施授权采用的默认规则）**：调用方显式声明
  intent —— TDD/PROVE 阶段的红 = 预期红不立案；full/回归跑失败 = 意外红
  触发立案；hash-mismatch = 资产漂移类，直接指认漏签 manifest。
- D3 **域 A 信号必含在途变更清单**（EVO-001 2026-10-23 归因修正的教训：
  无此字段则诊断会把在途会话中途状态误判为主树既有红）。
- D4 **域 B 两步**：终态判定自动化（无模型）；深诊断人发起，产 packet
  落盘（DBG-003），两者可独立存在。
- D5 run-diagnosis 只读取运行资料；新增写诊断包到repo evidence区，不写run目录或Runtime状态。既有posture词汇中真实落盘须声明local-write，不能写文件却声称read-only；这仅描述工件副作用，不授予产品权威。
- D6 **PROVE 按需触发验证层**（用户 2026-10-23 定型）：候选修复的验证
  按影响面选起始层、按需升级（六层阶梯见 evolution-loop
  `references/verification-ladder.md`）；不盲目全量；L5 模拟器/真实模型
  仅在需要 live 证据时进入，同一循环连续多轮共享一次 boot+预热 lease
  （复用 `tools/android/start|stop-test-device.sh` 既有 supervisor 机制，
  不新建会话管理）。触发器侧信号不携带层信息——选层是 PROVE 的职责。
- D7 L4 全量回归属 REVIEW/VERIFY 或 gate 要求，不属单轮 PROVE 默认成本。

## Acceptance（本次实施验收）

| ID | method | expected | actual | evidence |
|---|---|---|---|---|
| A1 | 信号协议 + 两 adapter 单测/干跑 | dev-verify 与 live-run 各产一份合法 signal v0；域 A 信号含在途清单 | PASS：36/36通过；schema校验及malformed/缺引用hash负例通过 | evidence/verification.json A1 |
| A2 | 用 EVO-001 cycle-01 的 red.trx 作域 A 真实输入 | 解析出 failed + preliminaryClass + reproduce 候选（test filter） | PASS：首次candidate、重复duplicate，不创建Change | evidence/verification.json A2 |
| A3 | 用既有 runDir（evidence 下真实 run）作域 B 输入 | 终态判定正确产信号；hash-mismatch 类能指认 PopupReplay manifest 漏签场景（在途态复演） | PASS：TerminalNotProven→failed/pending-reproduction；受控manifest精确指向SettingsPopupClassifier.cs，原manifest不改 | evidence/verification.json A3 |
| A4 | run-diagnosis 干跑 | 产出 packet v0 文件及path+hash，落独立evidence；运行资料与Runtime状态只读，真实工件写声明local-write | PASS：Host.Dsh64/64，含导出/非法包拒绝/报告变化拒绝/实际Python退出适配；实际导出包按schema验证通过，无外部模型调用 | evidence/verification.json A4 |
| A5 | Uniflow 挂点 | A3 具体化文本 + state.md `diagnosis:` 行进入模板；bug 类 RESOLVED 门槛可检查 | PASS：Standards/Spec复审均PASS；原case逐字节等于HEAD | evidence/verification.json A5 |
| A6 | 分诊门 | 同一失败重复到达只立一次案；预期红不立案；已知问题归档 | PASS：36/36全套通过；review三项RED→GREEN和mac根路径修复失败过程保留 | evidence/verification.json A6 |
| A7 | 边界 | 仅获准Harness工具适配源码改动；无产品领域状态改动、守护进程或自动合并/晋升 | PASS：6/6保护hash一致；quick PASS/41认证0违规；Workspace103/103，格式检查通过 | evidence/verification.json A7 |

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
- model-procedure既有可选entry本期作为声明式确定性退出适配，由当前真实run-diagnosis买方接线；skillRef仍唯一模型procedure。调用约定复用--run-dir/--out-dir，附--diagnosis引用，不增加清单字段、Tool或产品Owner。验证器检查已声明entry存在。

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
  actual: A1–A7全部通过；工具36/36、Host.Dsh64/64、Workspace103/103；quick PASS/41认证0违规；独立Standards/Spec复审0遗留。无ENVIRONMENT运行。
  evidence: changes/EVO-002/evidence/verification.json
```

## Status log

- 2026-10-23 UNDERSTAND→RESOLVE：用户定型触发双域（开发验收测试 / 生产
  诊断工具）；与 DBG-003（出口）、EVO-001（循环主体）拼成完整入链。
- 2026-10-23 PERSIST→PLAN：落本 state；实施前待 Owner 确认 D2 默认分诊
  规则与 A7 边界。

- 2026-10-23 RESUME→UNDERSTAND→RESOLVE→PLAN：用户“进行下一步”，明确答复允许最小工具适配与对应测试。重新核对a05176f3与6份foreign dirty；记录保护hash。D2采用显式intent分诊；A7按新授权限缩。验证缝沿A1/A2/A3的CLI和A4既有InvokeProcedureAsync公开接口，不新增私有测试缝。计划见plans/EVO-002.md。

- 2026-10-24 IMPLEMENT→REVIEW→IMPLEMENT：两域scanner、schema、工具导出及声明式退出适配、Uniflow/模板完成。独立review指出全绿漏扫、畸形manifest丢信号、诊断引用schema缺约束，逐项公开回归RED→GREEN修正；保留首次编译失败、mac临时根路径失败及全部中间日志。
- 2026-10-24 REVIEW→VERIFY→CLOSED：两轴复审PASS；A1–A7四元组见evidence/verification.json。仅关闭本次接线范围；EVO-001候选及SIM-017裁决不变。原6份foreign记录hash一致、LOCK case不变；未提交推送。交付见report.md。
