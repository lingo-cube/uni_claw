# VERIFY-ENTRY-001 — full 工程完整性与参数化场景发现
lifecycle_state: closed · disposition: none · depth: standard · base: 9bc15b8974f5fb5746074e373e6c132bc4df0089

## Intent（WHAT/WHY）
full 漏列 Host.Dsh；场景发现正则丢弃带空格的 Theory 展示名，导致完整测试名无法与 TRX join。
## Scope / Out of Scope
负责 tools/verify-change、tools/scenario-coverage.py、tools/tests/test_verify_entry.py、本 Change 与 evidence/verify-entry-001。不得改 src、Simulation 测试/fixture/场景、共享 INDEX、路由；不操作设备，不提交推送。
## Decisions
最小补齐现有工程列表；以 solution 清单对实际调用作回归。发现保留完整展示名（含参数）与 TRX 精确 join；不截方法名。冲突重复结果拒绝。
## Acceptance
A1：回归在原实现显示缺 Host.Dsh，修复后 full 八工程各一次（Simulation 不重复执行）；quick 契约保持。
A2：固定带空格/引号/多参数的 list-tests 与 TRX 样本逐例 join；失败不会被其他子例覆盖，空/未知仍拒绝通过。
A3：工具自检通过；独占源码快照 full 实跑八工程，新鲜 Simulation TRX 供 coverage 复用；另独立临时 xUnit 工程实跑参数化发现与 TRX 比对。
## Constraints
共享工作区；写前状态与工具恢复副本在 evidence/verify-entry-001/before、entry-status.txt、entry-tracked.patch。不回退外部更改。
## Verification
level: SCENARIO
method: python3 tools/tests/test_verify_entry.py；tools/verify-change VERIFY-ENTRY-001 --self-test；隔离快照 full；独立 xUnit 参数化集成。
expected: 两缺陷 RED→GREEN；full 八工程执行、Simulation 只一次；逐例完整名一致且含失败时不会 passing。
actual: 两缺陷均 RED→GREEN；7/7 独立回归、自检 PASS；冻结源码本轮 full 八工程 2038/2038、Simulation 一次、31/31 场景；真实 Theory 3 子例与 TRX 精确 join，2 通过+1 故意失败派生 failing。当前共享工作区并行新增源码未通过，不纳入冻结快照结论（详见 report.md）。
evidence: evidence/verify-entry-001/
## Status log
2026-10-22 · UNDERSTAND→RESOLVE→PERSIST→PLAN · 当前源码与历史证据复验，HEAD 一致；根因已由输入边界确认，直接执行回归与最小修复。
2026-10-22 · PLAN→IMPLEMENT · 原实现 4 个 RED；补 Host.Dsh、保留完整 Theory 名及拒绝冲突重复结果。
2026-10-22 · IMPLEMENT→REVIEW · Leader 直接逐项评审，见 evidence/verify-entry-001/review.md；未委派。
2026-10-22 · REVIEW→VERIFY · 独立回归 7/7，自检 PASS；隔离 full 首轮输入漏拷、第二快照并行源码失败均保留；未改产品追绿。
2026-10-22 · VERIFY→CLOSED · 第一冻结源码补只读资产后完整 full PASS，8 工程 2038/2038、Simulation 一次；真实参数化集成三键精确 join 且失败保留。当前并行源码限制与下一步详见 evidence/verify-entry-001/report.md。
