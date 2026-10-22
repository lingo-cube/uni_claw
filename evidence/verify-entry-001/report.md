# VERIFY-ENTRY-001 — 验证入口修复交付

## 做了什么
修复 `tools/verify-change` full 漏跑 Host.Dsh，以及 `tools/scenario-coverage.py` 丢失带空格参数的 Theory 展示名。未修改产品源码、Simulation 测试/工程/fixture、旧场景、共享 INDEX、路由；未操作设备、未提交推送、未向其他会话发消息。

## 原来的问题
- full 的 `SIMULATION → OTHER_TEST_PROJECTS → COVERAGE` 调用链只执行七工程，与 solution 的八工程不符。回归在真实 main 的进程调用边界观察到 Host.Dsh 缺失；注入 Host.Dsh 失败也被漏过。
- `scenario_test_map → parse_trx → load/derive_status`：`^\s+(UniClaw\.\S+)$` 拒绝完整展示名中的空格。原工具对同一真实 xUnit 二进制的三个 Theory 子例发现数为零。
- 同一完整 testName 的冲突结果原来取最后一个，Failed 可被后面的 Passed 覆盖。

## 这次修改
- `tools/verify-change`：沿用既有枚举结构，补 Host.Dsh 与 ownerBasis；Simulation 和 quick 分支未改。
- `tools/scenario-coverage.py`：仅去 list-tests 行首缩进，保留整行展示名及全部参数，与 TRX testName 精确 join；冲突的同名结果抛异常拒绝，不生成通过。
- `tools/tests/test_verify_entry.py`：对实际 full 调用与 solution 清单做集合及重复检查；Host.Dsh 失败传播；quick 范围；固定参数样本逐例 join；冲突、未知、空及缺子例拒绝。
- `tools/tests/verify_entry_theory_integration.py`：独立临时 xUnit 工程，沿用仓库包版本，三个子例带空格、转义引号及三个参数；有意让第二例失败，保留真实 list-tests 与 TRX。

## 验证结果
Review 在 `review.md` 独立记录，Verify 结果如下。

| method | expected | actual | evidence |
|---|---|---|---|
| 原工具运行独立回归 | 两个缺陷确实 RED | 6 项中 4 FAIL：漏工程、漏失败传播、参数名丢失、冲突覆盖 | red.txt |
| 修复工具运行独立回归 | 原 RED 转 GREEN，未知/空/缺子例拒绝 | 扩充后 7/7 PASS | green.txt |
| verify-change --self-test | 既有契约保持 | FINAL_STATUS=PASS | self-test.txt |
| 冻结源码快照 restore 后串行 full | 八工程均执行；Simulation 一次；coverage 复用新鲜 TRX | 2038/2038；Host.Dsh 56/56；Simulation 263/263 一次；31/31 场景；20 certified + 11 descriptive；FINAL_STATUS=PASS | full.txt、module-results.json、simulation-full.trx、resolved-config.json、snapshot.json |
| 独立 xUnit build → list-tests → test → scenario_test_map → parse_trx → derive_status | 完整名逐例一致，含失败不能通过 | 原实现映射 0；修复映射 3；与真实 TRX 三个键完全一致；2 Passed + 1 Failed 派生 failing | theory/list-tests.txt、theory/execution.txt、theory/theory.trx、theory/result.json、theory/before-after.json |
| 定向 diff 检查与隔离 hash | 工具交付与被验证工具一致 | 无空白错误；四个负责工具/测试与 full 快照 hash 一致 | diff-check.txt、source-comparison.json |

full 输出的逐工程数量不含前面的 architecture 子集重复计数。coverage 仅做枚举，不额外执行 Simulation。Theory 工程的 1 个失败是有意反例，集成脚本断言它存在；它不属于产品八工程。

## 新发现与未验证项
- 首轮快照误排除 platforms 录制输入，Simulation 200 项文件读取失败；保留 `full-incomplete-snapshot.txt`、`incomplete-snapshot.trx`，只补只读输入后在同一冻结源码完整重跑。25 个历史 XML 从既有录制路径复制，未修改内容。
- 第二份快照捕获了并行源码变化：新增 `RecordedOperationReplayTests.cs` 使用不存在的 `OccurrenceFact.Space`，Simulation 增加 Host 引用又触发 Kernel composition-root 约束；其 full 未通过（`full-concurrent-source.txt`、`concurrent-source-snapshot.json`）。本任务未修改或删除这些文件。
- 最终 full 是第一份已冻结源码（31 场景）的本轮新鲜执行，不是历史 SIM-009 证据。`source-comparison.json` 明列当前工程文件差异及新增 004/005 场景等并行路径；不能把本次 2038/2038 移植成当前动态共享工作区全绿。
- 独占快照的本地 git 仅为 diff 检查初始化，未提交；其 resolved-config 中 git HEAD 不构成原仓库来源证明。原 HEAD、内容 hash 与独立快照路径以本目录 entry 状态、snapshot.json 和 Change State 为准。
- 未运行 live 设备、真实模型或其他语言工程；quick 以调用契约回归验证，未额外运行设备或 quick 实跑。没有为追绿改产品、旧场景或认证。

## 下一步
父任务可检查两个工具的 diff 与本报告，继续仿真完善；当前新增录制回放测试及 composition-root 冲突由仿真负责线处理，修复后再从当前源码创建新快照跑 full。本任务的两个工具缺陷与验收已完成，不接管该并行实现。

复现入口：`sh evidence/verify-entry-001/reproduce.sh`；它对运行时的当前源码生成新快照，因此若并行源码仍不完整，full 应如实失败。详细输入 hash 在 snapshot.json；快照与临时 Theory 项目保留供检查。
