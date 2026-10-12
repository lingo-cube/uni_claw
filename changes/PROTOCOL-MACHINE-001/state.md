# PROTOCOL-MACHINE-001 — 协议状态机：三机一源三出口（YAML 单源 + 生成执法渲染）
lifecycle_state: persisted · disposition: none · depth: decision-heavy · base: 14987fe

## Intent（WHAT/WHY）

协议维护升级为状态机维护：把 Run 生命周期、对象生命周期与协议边挂载从
散文/手工同步（形式审计手工建模、27 步交互模型手写数据副本、lockdown
快照互相脱节）收拢为**机器可读单源**，经生成器产出三个出口——文档片段、
转移合法性执法测试、交互模型渲染。形式审计的 Liveness/Reachability 检查
（终止性=每环消耗良基测度、可达性=DFS、悬挂点=出度 0 报警）从人工分析
变为机械检查。

## Scope / Out of Scope

In：`schemas/protocol/state-machine.yaml`（单源）；生成器
`tools/gen-protocol-machine.py`（三出口：协议基线 Protocol Map 节片段 /
xunit 转移合法性测试源或数据文件 / `protocol-workflow-model` HTML 的 STEPS
数据）；三台机器的完整定义（见 Decisions）；一次性人工对齐审计（YAML vs
基线 §17/RunDriveStatus/协议边）。
Out：不夺文档权威（基线仍为 canonical；YAML 为派生/执法/渲染源——升格
权威需 narrow amendment）；不做模型检查器引入（TLA+ 等超范围）；PROTOCOL-FIX
-002/-003/-004 的实施内容。

## Decisions（方向已定，细节随实施裁决）

- D1：**三台机器，非单一巨 FSM**——
  ① Run 生命周期机（全局一台：created→admitted→[P24]→activated→cycle
  {observing⇄deciding⇄acting⇄verifying}→waiting/recovering→terminal(4)→
  [emit]→closed 吸收态；每转移挂：触发协议边/guard/载荷引用/协议状态）；
  ② 对象生命周期机（binding: candidate→canonical→consumed/derived-invalid；
  judgment 冻结；attempt: CommittedPending→Dispatched→Completed/UnknownOutcome；
  contract view: admitted→[D4 悬挂]/activated/frozen）；
  ③ 协议边 = 机间消息（event+payload 声明，P1–P26 挂载）。
- D2：单源 YAML，生成**单向**（文档片段/测试/模型皆产物，手改产物=漂移
  可检测）；交互模型 STEPS 数据改为构建期注入。
- D3：执法面——转移合法性测试（非法转移=RED）+ lockdown 快照互引（载荷
  引用指向快照文件，双源漂移=RED）。
- D4：排期在 PROTOCOL-FIX-002..004 之后（lockdown P2.5 位）：修完语义再
  铸机，避免铸完即改版；S3 续授转移（budget-exhausted→renewal-window→
  续授/终局）是第一块试金石。
- D5（2026-10-12 设计对话裁决）：**YAML 只承载结构，不承载语义**——状态/
  转移/边挂载/测度标注进 YAML；约束语义留基线+ADR（升格权威需 narrow
  amendment）。进程内载荷不造 JSON，边表直引 C# 类型名（`type: record`，
  细节与执法同文件，零漂移）；wire schema 是双方契约，留中立共享地
  schemas/workspace/，不搬组件目录。
- D6：**引用协议三类 ref**——payload 语义 → schema `$id`；散文约束 →
  基线标题锚（`### P25` 级）；决策 → ADR 编号。`--check` 解析全部三类，
  断锚即 RED。
- D7：**归属表 = 边表副产品**——"哪个 schema 对应哪个组件"由边记录
  from/to + payload_ref 一次 join 回答。三条执法：schemas/workspace 每个
  `$id` 被 ≥1 条边引用（无主 schema = RED）；payload_ref 可解析（record
  反射存在 / schema 文件存在且 $id 匹配）；from/to 出自封闭组件词汇。
- D8：**每域协议卡片第四出口**——边表按组件过滤生成各域 README 的协议
  卡片（参与边/载荷/guard/基线锚），派生视图手改即被 `--check` 抓；权威
  不搬家，归属目录只收获可发现性。
- D9：**机器节点与 RunDriveStatus 分栏**——生命周期主干（created→admitted
  →[P24]→activated→cycle→terminal→closed）是节点；RunDriveStatus 15 值
  是转移边上 emit 的观察投影（枚举自注"观察聚合，不新增 canonical
  state"）。两栏分离，防 YAML 变第二状态权威。
- D10（2026-10-12 对抗复核后定稿）：**Run 机终版形状**——terminal 四值
  实证 = `TerminalClassification {Completion, Failure, SafeStop,
  Escalation}`（RunObligation.cs:68）；`WaitingForInput` 建模为 observing
  自环 + emit 标记（不设独立节点）。环分两类（liveness 规则二分）：消耗
  良基测度的**进展环**，与显式标 `await: external` 的**静默环**（合法等待
  外部输入，白名单放行、不得伪装进展、不得携带测度声明）。
- D11：**边登记词汇封闭**——id 命名空间 `P##`（协议边）/`O##`（wire·宿主
  边，沿用 HTML 既有 O1/O2）/`G##`（known gap=缺边的显式登记）；每条目必
  带 `kind: edge|gap`，gap 只进渲染与审计、永不进转移合法性表。`status`
  封闭三值 `keep|fix|track` + `fix_ref` 必须解析到存在的 change id（断链
  RED）——编辑层止步于此，防 D5 破口。前奏步骤（任务定义/发起/Instance/
  Session/契约 authoring）全进表。
- D12：**执法牙齿分层**（与仓库先例对齐：牙在 C#、写手在 python）——
  ①C# 牙读 `protocol-machine.generated.json`：枚举同步（RunDriveStatus
  成员 ⊆ emits 栏）、转移合法性（含已知非法对负向自证）、payload
  `type: record` 反射存在性——环境无关，进 dotnet test；②python 牙
  `--check`：YAML→产物再生成 byte-diff（新鲜度），进验证流；③可选 shell
  版 xunit 缺 python3 时 fail-closed 报"环境缺"而非糊测试失败。写手模式
  检测到 YAML 语义 diff 时强制 `--change <致因 id>`（C8 同款），纯再生成
  免。YAML 头部 `version: 1`；生成器自带元校验（封闭词汇/必填键，PyYAML
  6.0.3，先例 validate-model-bindings.py）。产物不落时间戳（A2 字节稳定）。

## Acceptance

| # | 判据 |
|---|---|
| A1 | YAML 单源覆盖三机全状态/转移/边挂载；与基线 §17+RunDriveStatus+P1–P26 对齐审计留痕 |
| A2 | 三出口生成可复现（两次生成字节一致）；手改产物被漂移检测抓出 |
| A3 | 转移合法性测试就位：非法转移样例 RED（负向自证） |
| A4 | Liveness/Reachability 机械检查跑通：终止性（进展环良基测度标注；`await: external` 静默环白名单放行且不许伪装进展）、悬挂点清单自动产出——**每机一份白名单**：run 机 = {}（每状态有出路或是终态）、contract-view 机 = {admitted}（Deferred ⑰，即原"D4 悬挂"，归属修正见修订记录 R1）、其余任何悬挂 = RED |
| A5 | 交互模型 STEPS 由生成注入，手写副本删除 |
| A6 | 归属执法三条全绿：无主 schema = RED、悬空 ref = RED、组件词汇封闭（拼错 = RED） |
| A7 | 每域协议卡片就位（≥Kernel 六域），手改卡片被漂移检测抓出（负向自证） |

## Verification

```yaml
verification:
  level: DETERMINISTIC
  method: "生成器幂等运行 + 对齐审计表 + 转移合法性/漂移检测测试（运行待网络）+ 图算法检查脚本"
  expected: "A1-A7 全过；每机悬挂白名单精确匹配：run={}; contract-view={admitted}（Deferred ⑰）"
  actual: "Slice 3 完成：--check 升级为「新鲜度+图审计」，审计为纯函数（不写文件、无时间戳）。①可达性 DFS：每机从 initial（∪ hang_whitelist 接缝入口）出发，白名单为空的机不可达 = RED；非空白名单机 = seam-pending，不可达下游进报告待接缝闭合（语义裁决见 R3）。②悬挂白名单每机精确匹配：期望表 run={} / binding={} / judgment={} / attempt={} / contract-view={admitted}（Deferred ⑰ 唯一），声明≠期望 = RED、白名单条目实际不悬挂（失真）= RED、白名单外悬挂 = RED，双向执法。③终止性环二分（D10）：初等环全枚举，进展环必带测度（词汇封闭）否则 RED；await:external 静默环携测度（伪装进展）= RED。run 机实证 5 进展环（defer-rounds/steps/rounds/cap-remaining×2）+ 1 静默环（R-T10）。④断锚按命名空间分流：P## 逐字 grep 协议基线（26/26）、G## 逐字/锚头命中交互模型 HTML（5/5）、O## 锚头 slug 逐字等于 payload schema $id（D5/D7：wire 边 canonical 锚=双方契约）——发现 O1/O2/O10 三条 YAML 锚漂移，最小修正为 $id 逐字形并 --change 重生成。--audit-report 产出 evidence-friendly 审计报告（锚解析汇总/每机可达性/悬挂清单/环分类表/测度标注汇总）。Slice 1+2 完成项不重复；Slice 4 未动。"
  evidence: "① python3 tools/gen-protocol-machine.py --check → 退出 0（5 机/28 转移/44 边，全审计过）；② --audit-report：anchors 44/44、run 机 reachability 10/10、环分类 5 progress + 1 silent 全部带测度标注；③ 负向自证五条各 RED 一次（exit 1）并还原恢复（YAML 字节一致、--check 恢复 0）：不可达非悬挂节点 ghost → 'unreachable nodes from initial (DFS): [ghost]'；删 contract-view 白名单 → admitted 白名单外悬挂 RED；白名单加 activated → 精确匹配失配 + '白名单失真 [activated]' 双 FAIL；删 R-T09 measure:rounds → 'progress cycle without a well-founded measure: R-T04/R-T05/R-T07/R-T09'；改 P1 锚 → 'title_anchor does not resolve in inter-component-protocol-baseline-l1-l3.md'；④ dotnet build UniClaw.Kernel.slnx → 0 错误；⑤ dotnet test tests/UniClaw.Kernel.Tests → 882 通过 / 0 失败（基线保持）；⑥ 语义修正后 --change PROTOCOL-MACHINE-001 重生成 + 连续 --check 通过（产物无时间戳，两次生成字节一致）。"
```

## Status log

- 2026-10-12 · OPENED（PERSIST） · Owner 批准开档；方向经对话裁决
  （三机切法/先派生后权威/排在 PROTOCOL-FIX 后）；实施待 -002..004 完成。
- 2026-10-12 · RESOLVE 续行（Leader 设计对话） · 与 Owner 敲定 D5-D9：
  YAML=结构不语义、三类 ref 引用协议、归属表=边表副产品+三执法、
  每域协议卡片第四出口、节点/枚举值分栏。验收补 A6/A7。剩余待裁：
  Run 机节点集终版、边 id 命名空间（P/O/缺口）、缺口表示法、执法入口、
  STEPS 注入机制、每机悬挂白名单、YAML 版本化与搭乘规则、对象机范围。
- 2026-10-12 · RESOLVE 收口（Leader 对抗复核 + Owner 批准） · D10-D12 落定。
  修订两处：R1 悬挂归属纠错——admitted-never-activated 本属 contract-view
  机（D1 原文即此），此前讨论一度错记到 run 机并双记；正解 run={},
  contract-view={admitted}。R2 evidence admission 不入 v1 的理由更正——
  非"边太少"（其 admission 检查链/拒绝原因/lineage DAG 校验是台真机器），
  而是组件内逻辑非跨组件协议缝；挂 EvidenceLedger 域卡片，跨组件证据流
  出现真实 buyer 再升格。规格自洽，切 Slice 1（YAML 骨架 + Run 机全量 +
  生成器出口 b + C# 牙三件 + python --check）。
- 2026-10-12 · IMPLEMENT Slice 1（worker=glm-5.3-flash） · 落地单源骨架三件：
  schemas/protocol/state-machine.yaml（version 1，run 机 10 节点 / 17 转移 /
  7 边；D9 分栏——RunDriveStatus 只在 emits；D10 环二分——R-T08/T09/T13
  progress measure、R-T10 observing 自环 await: external）；tools/
  gen-protocol-machine.py（元校验封闭词汇 + 确定性生成 + --check 漂移 +
  写手 --change 搭乘，产物无时间戳）；tests/UniClaw.Kernel.Tests/Protocol/
  （C# 牙三件：枚举双向同步、转移合法性含 closed→acting 与 admitted 未经
  P24 入 cycle 负向自证、payload record 反射；生成产物
  protocol-machine.generated.json）。发现：RunDriveStatus 实际 13 成员
  （非任务书 15），C# 双向测试按实证枚举钉扎。A6：纯测试面，无新公共
  生产类型，白名单无需动。
- 2026-10-12 · REVIEW→VERIFY（Leader，Slice 1） · 独立复核：锚点 7/7 全中
  （D6 title_anchor 逐条 grep 基线）；--check 篡改检测独立复证（改
  generated.json → exit 1 报 stale，恢复 → OK）；dotnet test 877/877 全绿
  零回归。复核修正一处语义摆放：TerminalNotProven 原挂 R-T16
  （verifying→terminal）emits——枚举语义为"保持非终态"，进 terminal 的边
  上不应携带；移至 R-T09（verifying→observing 回环，评估不足继续循环），
  --change 重生成后全绿。RunDriveStatus 13 成员（WorkItem 误书 15）按
  实证钉扎。Slice 1 验收闭合。
- 2026-10-12 · IMPLEMENT Slice 2（worker=glm-5.3-flash） · object_machines 四台
  + 边登记全量 + 归属执法三件：YAML 增 binding/judgment/attempt/contract-view
  四台（D9 分栏——拒绝原因/派生失效是边属性不设状态；contract-view 悬挂
  admitted 按 R1 入 hang_whitelist）；edges 7→44（P1-P26 逐条按基线标题锚、
  placeholder 边 P5/P19/P21 标 track、O1-O13 payload_ref type: schema 直指
  schemas/workspace 全部 $id、G1-G5 缺边含 D5 观察指向）；生成器元校验扩展
  （D7 ①无主 schema RED ②schema $id 可解析/record 形状 ③词汇封闭已有 +
  object 机形状：terminal_nodes 必填/吸收态无出边/非白名单节点必须有出路 +
  fix_ref 解析到 changes/<id>/state.md 或 ADR + gap 不进转移合法性）；C# 牙③
  改造为 record|schema 双类解析（纯函数化，负向自证内存变异不动磁盘产物）
  + 牙④三件（object 机形状/code facts 钉扎/gap 纪律）。裁决记录：P26 组件
  词汇新增 simulation-host（测试/治理侧，非产品内边）；P19-P21 realization
  在 Kernel 测试面之外或 Deferred ⑩，payload_ref 缺席仅允许 status: track。
- 2026-10-12 · IMPLEMENT Slice 3（worker=glm-5.3-flash） · 图审计落地：--check
  升级为「新鲜度+图审计」，新增 --audit-report（evidence-friendly 报告：锚
  解析汇总/每机可达性/悬挂清单/环分类表/测度标注汇总）。审计器四件：①可达
  DFS（roots = initial ∪ hang_whitelist 接缝入口；空白名单机不可达 = RED，
  非空白名单机 seam-pending 只报告）；②悬挂白名单每机精确匹配（期望表内置
  生成器：run/binding/judgment/attempt={}、contract-view={admitted}；声明≠
  期望 / 白名单失真（条目不悬挂）/ 白名单外悬挂三向 RED）；③终止性初等环
  全枚举二分（进展环无测度 = RED、静默环携测度 = 伪装进展 RED；run 机实证
  5 progress + 1 silent）；④断锚按命名空间分流：P## 基线逐字、G## HTML
  逐字/锚头、O## 锚头 slug == payload schema $id。YAML 缺陷修正两处语义：
  R3 可达性 seam-pending 裁决（contract-view 的 activated/frozen 因 Deferred
  ⑰ 接缝未闭合而结构性不可达——严 DFS 与悬挂白名单互斥，裁决空白名单机
  严执法、非空白名单机报告不 RED，提请 Leader 复核）；O1/O2/O10 锚漂移
  最小修正（'task-launch wire'→'task-launch-request'、'task-launch ack /
  Runtime 铸造'→'task-launch-ack'、'capability envelope'→'capability'），
  --change 重生成。验证：--check 0；负向自证五条各 RED 后还原全恢复；
  build 0 错误；dotnet test 882/882 绿。Slice 4 未动；未 commit。
- 2026-10-12 · REVIEW→VERIFY（Leader，Slice 2+3） · 独立复核：882/882 全绿、
  --check 图审计过（44 锚/悬挂白名单精确匹配/环二分）、13 schema↔13 引用
  对拍、事故后完整性验尸通过（726 行/44 边/无残留）。裁决 R4（批准 worker
  R3）：contract-view 不画 admitted→activated 出边——代码事实是
  RunModel/ExecutionContractView 无 activated/frozen 状态跟踪（激活闩在
  run 机 driver；P17 冻结由 record 不可变性执法），D1 ② 的 activated/frozen
  属协议已声明、view 工件未实现；非空白名单机的 seam-pending 容忍合法，
  审计报告持续可见，⑰ 闭合时收紧。D6 增补：O 系 wire 边 title_anchor =
  payload schema $id 逐字形（双方契约即 canonical 锚）。A1 措辞按"与代码
  事实一致"理解，contract-view 部分实现状态在审计报告可见。
