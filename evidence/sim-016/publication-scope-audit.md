# SIM-009–017 发布范围审计

## 结论

可形成自包含提交包，但仅提交本组闭环所需文件及两个已完成前置任务；不要按整个 dirty tree 提交。SIM-009至017均有各自 state/证据，SIM-013/015是设计/协议与执法依赖，SIM-014冻结组件契约和来源pins，SIM-016承载最新Host/Runtime回放。前置 `CHECKED-GUARD-001` 与 `VERIFY-ENTRY-001` 必须纳入：SIM-011明确依赖前者修复消费guard，SIM-010/009系列的full工程枚举与参数场景发现依赖后者。SIM-012、014、016沿用这些验证入口。

## 必须纳入的未提交依赖

- `changes/SIM-009` 至 `changes/SIM-017` 的状态、设计/契约文档及对应 `evidence/sim-*`；其中每个Change声明的 `evidence/` 是验收链的一部分。
- `changes/CHECKED-GUARD-001`、`changes/VERIFY-ENTRY-001` 及 `evidence/checked-guard-001`、`evidence/verify-entry-001`：保留前置实现、RED/GREEN和入口验收的可追溯性。
- Simulation回放代码、场景定义和所有被测试直接加载的fixture；至少包括 `Fixtures/Replay`、`Fixtures/Imported`、`Fixtures/Recent`、`Fixtures/ComponentReplay` 的被引用文件，以及SCN-REPLAY-001至007对应场景。SIM-009还明确依赖历史XML输入，SIM-010/012引用录制/导入资产。
- Host回放代码及 `Fixtures/PopupReplay/{composition,sources,oracle}.json`，其来源录制资产由sources清单指向；还要包含SIM-016加入的Host/Runtime专项、场景入口、README登记与跨根coverage工具/回归。
- 产品侧只纳入这些测试实际依赖的改动：SIM-014/015的测试构成/场景执法元数据，以及SIM-016抽取的 `SettingsPopupClassifier` 和LiveFeed转发（当前 `src/UniClaw.Host/README.md`、`SettingsTraversalLiveFeed.cs`）。其余产品文件若不在这条依赖链或SIM-016精确source pins内，不应顺带提交。

## 必须排除的脏文件

当前状态中与该发布包无关的内容包括 `.tmp-protocol-artifacts/`、AGNES/JEV/DIAGNOSTIC/REPLAY-STATE/UI-TEST-REUSE 等未关联Change目录；`WORLD-OBSERVATION-*`、`WORLD-SEMANTICS-*` 等大体量设备/模型运行证据；以及 `CONTEXT.md`、RUNTIME-PLAN-002、SETTINGS-COVERAGE、TRAJECTORY、场景族 `SCN-WIFI-*`/`SCN-POLICY-*`/`SCN-BARRIER-*`、SemanticCheckedResolver/Runtime/World等并行改动。不要把这些目录或文件加入SIM提交包，除非SIM来源/依赖清单明确证明它们属于本轮。

## 来源、依赖与保留隐患

`ComponentReplay/composition.json`含大量源码/测试SHA pins，必须与SIM-014/015对应源码、测试、fixture及场景同步提交；否则新checkout会因pin失配无法复现。`PopupReplay/composition.json`同样pin源码、测试和录制资产，且引用Simulation侧的组件回放数据，故不能只提交Host目录。SIM-016的verification记录2个episode、6帧、16个原始资产、156 pins；其attempt1/attempt2失败、分类RED、变异失败和最终TRX属于重要失败边/验收证据，应保留足以支持报告结论的摘要、verification、source-provenance、protection-check、scenario发现与最终TRX。无需提交冗余完整构建输出/多份快照，前提是state中引用可由保留的证据核验。

`publication-required-assets.json`要求带入大量旧的原始XML/PNG、settings-trace、coverage步骤、facts及preregistration；其中可能含真实设备界面/应用信息或运行配置。发布前应按SIM-016审计目的只保留被选用的两个episode及其关联帧/动作回执/trace与hash、最小来源清单和必要协议/构成说明；完整周语料和无关旧run不应因清单机械纳入。若来源授权要求原始资产随仓发布，则需明确权限和脱敏状态；当前仅凭路径无法证明其公开安全。portable evidence最小集合：各Change state及核心verification JSON/report、SIM-016两个episode完整关联资产与source manifest、最终专项/全量TRX及场景发现关联、SIM-014/016构成manifest、前置guard/入口的最终验收摘要。

审计只读完成；未构建、未联网、未暂存或提交。
