# REPLAY-STATE-EVIDENCE-001 — checked 状态补采与证明前置设计
lifecycle_state: resolving · disposition: none · depth: decision-heavy · base: 9bc15b8974f5fb5746074e373e6c132bc4df0089

## Intent（WHAT/WHY）
保留旧 Stay awake XML 作为 Unknown 拒绝用例，核对已有 checked 契约与消费链，给出独立新语料的最小补采/证明/验收设计。设计交付已就绪，等待评审；本 Change 不代表产品契约或实现获批。

## Scope / Out of Scope
仅新目录 `changes/REPLAY-STATE-EVIDENCE-001/` 的设计、状态与证据。只读产品/既有语料。禁止修改 src/tests/旧录制/GT/scenarios/共享 INDEX/CONTEXT/ADR/模型路由；不操作设备/模型，不提交推送，不向其他会话发消息。共享工作区他人修改不回滚、不接管。

## Decisions
- 已确定：旧录制 false 无 ExactProof → Unknown；旧失败证据保留，新语料另立 provenance。
- 已确定：复用现有 typed model、projector、Runtime 与外部仿真缝；不预造通用新组件。
- 候选：受限域 BooleanExact 新录制优先；richer TriState 路线需独立校准。两者均未批准实现。
- 设计发现：proof 仅检查非空；capture 级 Exact 存在范围扩散风险；typed 消费 guard 未显式检查 checkable；XML TriState true/false 走 boolean 入口抛异常。均只记录，不修代码。

## Acceptance（本次设计交付）
| ID | method | expected | actual | evidence |
|---|---|---|---|---|
| D1 | 只读核对 raw XML、diagnostic actual、parser/mapper/projector/consumer | 说明证据不足原因且不把 OFF 期望当实际 | 已完成；两帧原始 bool=true/false，原 actual verification=false/no-checked-claim | evidence/legacy-observation.json；evidence/source-excerpts.txt |
| D2 | 逐项核对 proof/capability/typed/guard/partial/四门/ADR与消费owner | 可复核的现状及缺口，区分当前执法与候选 | 已完成；见 design.md §2–4 | evidence/source-manifest.json；design.md |
| D3 | 核对每项补采输入、oracle、版本/校准/失配、真件替换缝与四元组 | 新旧隔离、最小切片、独立正反例、明确未验证项 | 设计已完成；采集/实现全部 NOT RUN | design.md §5–8；cases.json |
| D4 | 比较保护快照与最终文件清单，检查文档材料及哈希 | 所有本任务写入在 allowlist；共享变化仅报告 | 已执行材料检查；保护文件漂移见 final-verification.json，不回滚共享变化 | evidence/ownership.json；evidence/final-verification.json |

## Constraints / Owner-Authority impact
生产 adapter 拥有采集/归一化/能力声明；语料 owner 拥有独立预期和校准材料；Ledger 入证、World belief/occurrence、Assurance 验证、Control 决策、Effect 交付职责不变。本文没有新增产品公共类型或能力注册，不冻结新接口形状。仅审阅材料，不启动新产品 Change 实现。

## Assumptions / Alternatives / ADR refs
完整分析见 design.md。替代方案：盲升级旧 XML、按 API/class/device 推 Exact、借 Wi-Fi proof 覆盖 Stay awake、由 GT 或 Runtime 输出制造 actual 均拒绝。ADR-0017/0026/0048/0049 与产品 baseline；checked 具体语义来自 PER-010/011/014 与 PER-009 D9/D12。

## Residual risks / Human decisions
首个新正例是受控 fixture 还是 Stay awake、设备/安装权限、是否提供 API36 richer 通道、两会话往返恢复的运行预算待用户裁决。当前可评审设计不依赖执行这些动作。保持 resolving 是因为后续实施选择未决，**不是设计尚未写完，也不是已授权实施**。

## Verification
level: CONTRACT
method: 只读源文件/旧材料核验 + 本目录 JSON/引用/hash/保护快照比较；没有产品测试或设备/model run
expected: 当前证据结论可追溯、补采计划及正反例完整、写入局限于本目录
actual: 设计与本地材料检查已完成；真实采集/三态端到端/guard修复均未验证
evidence: evidence/final-verification.json；evidence/legacy-observation.json；evidence/source-manifest.json

## Status log
- 2026-10-22 · entry→understanding · HEAD/branch匹配；保存 ownership、tracked patch、untracked inventory与保护哈希。
- 2026-10-22 · understanding→resolving · 当前链路核验完成；前置设计已交付待评审，实施取舍未决；未越界启动实现。
