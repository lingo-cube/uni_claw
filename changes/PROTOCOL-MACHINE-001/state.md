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

## Acceptance

| # | 判据 |
|---|---|
| A1 | YAML 单源覆盖三机全状态/转移/边挂载；与基线 §17+RunDriveStatus+P1–P26 对齐审计留痕 |
| A2 | 三出口生成可复现（两次生成字节一致）；手改产物被漂移检测抓出 |
| A3 | 转移合法性测试就位：非法转移样例 RED（负向自证） |
| A4 | Liveness/Reachability 机械检查跑通：终止性（良基测度标注于每环）、悬挂点清单自动产出（应命中 D4 且仅此） |
| A5 | 交互模型 STEPS 由生成注入，手写副本删除 |

## Verification

```yaml
verification:
  level: DETERMINISTIC
  method: "生成器幂等运行 + 对齐审计表 + 转移合法性/漂移检测测试（运行待网络）+ 图算法检查脚本"
  expected: "A1-A5 全过；悬挂点输出={admitted-never-activated}"
  actual: "待开工"
  evidence: "待开工"
```

## Status log

- 2026-10-12 · OPENED（PERSIST） · Owner 批准开档；方向经对话裁决
  （三机切法/先派生后权威/排在 PROTOCOL-FIX 后）；实施待 -002..004 完成。
