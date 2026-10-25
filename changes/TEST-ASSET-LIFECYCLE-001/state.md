# TEST-ASSET-LIFECYCLE-001 — 诊断、重放、仿真与验证的资产收尾
lifecycle_state: closed · disposition: none · depth: standard

## Intent / Scope
承接用户已接受的任务资产目录/JSON清单、独立Run、重复测试保留及随任务删除规则，补齐最新要求：诊断、重放、仿真每次完成都清理多余资产，不只等Task删除。将可执行的收尾纪律接入本地UniFlow和诊断/重放扩展及测试规范，统一真相源。

## Decisions / Constraints
- 只影响开发测试资产；产品任务核心结果/恢复/报告来源的保留仍由产品Owner和现有契约管理，不接管RUNTIME-STORAGE-002。
- 本次落实流程指令和格式规范，不宣称现有CLI已实现自动目录迁移/归档/GC，不更改verify-change的check-only语义，不启动存量批量删除。
- 第三方diagnosing-bugs原文不改，按skill-creator的窄更新原则接到本地扩展；不创建新生命周期或Task系统，不改已有packet/case schema和封存hash。
- 清理服从实际Owner及引用，不凭closed、mtime、文件类型、未跟踪、重复hash或目录名判可删；共享并行编辑保留。已授权的任务自有临时产物按规则处理，不额外制造重复审批。

## Acceptance
A1 唯一规范覆盖诊断、重放、仿真/验证每次收尾，成功/失败/取消均需留下真实处置结果；三条入口均可发现该规范。
A2 规定任务归属、独立Run、重复测试、最小复现/封存输入保护、归档校验与引用可用、任务删除及保护依赖迁移；收尾在既有生命周期内，不另造状态。
A3 定义任务资产清单与轻量收尾回执；清理实际完成前不得写已清理，待决项和保留原因可见，不覆盖或抹除失败。
A4 第三方skill字节不变，链接/示例JSON/本地skill元数据与diff检查通过；不执行产品测试或数据清理来冒充流程文档验证。

## Verification
level: CONTRACT
method: 入口链接/规范一致性、JSON示例解析、本地skill quick_validate与第三方SHA检查、diff检查
expected: A1–A4
actual: 唯一规范及UniFlow、LOCAL诊断/演进扩展、packet/case引用说明、测试/证据/Change/验证/工具导航已更新。诊断、重放、仿真/验证每次返回前强制收尾，涵盖失败/取消，保护有效输入与引用；重复测试、任务删除与真实处置回执统一定义。53个相对链接均存在，JSON示例可解析，第三方diagnosing-bugs和skills-lock与HEAD字节相同。通用skill校验器不接受仓库既有source字段，三个仅省略该字段的临时兼容副本校验通过，原字段保留且临时副本已删除。diff检查通过。仅流程/格式规范落地，CLI自动归档/清理适配、存量迁移及产品存储回收未实施。
evidence: ../../docs/agents/test-asset-retention.md；verification.json

## Status log
- 2026-10-25 UNDERSTAND→RESOLVE→PERSIST→PLAN：已核对当前LOCAL_UNIFLOW、LOCAL诊断/演进扩展和第三方诊断原文；沿用户已接受规则执行，无额外存储平台或自动清理权限扩张。
- 2026-10-25 IMPLEMENT：新增统一规范；接通本地三Skill与packet/case、测试/验证导航。第三方诊断skill原文保持，以LOCAL扩展补返回时资产处置；UniFlow原“失败尝试保留不删”改为保全仍需证据并按策略收尾，CLOSED加入真实收尾记录。不新增第九状态、产品协议或新工具。
- 2026-10-25 REVIEW：按真实误删案例核对计划/WorkItem归属保护、出站链接与入站消费区别；封存case/未支持归档的消费者保留原引用；失败/取消仍收尾但不重判业务结果。既有check-only语义明确保留，缺自动适配时执行者须显式执行清单；不以文字更新冒充自动化已实现。
- 2026-10-25 VERIFY→CLOSED：53链接、示例JSON、本地Skill身份/来源元数据、第三方字节和diff检查完成；通用校验器的既有source扩展不兼容单列，兼容临时副本3/3通过并删除。关闭流程规范切片，未删用户数据、未运行模型/设备或修改产品代码，未提交推送。
- 2026-10-25 后续实施顺序（用户明确）：存储功能优化完成并验收后，再接入目录组织、资产清单、归档和自动清理；当前仅规范切片closed，不提前开始自动化/目录迁移。待办和现有接线边界见handoff.md，新会话先核前置，不接管RUNTIME-STORAGE-002。
