# TEST-ASSET-AUTOMATION-001 slice1 — Spec review

审查固定点为 `evidence/slice1/before` 的 `tools/verify-change`、
`test_verify_entry.py`；只核对新增资产工具、入口及其定向测试。LAYOUT 的
配置选择/路径职责和 AUTOMATION 的首个登记、批次、消费者、取消收尾均已按
Change 分工审查；归档、诊断、重放尚非本片缺项。定向脚本测试
`test_test_assets.py`、`test_verify_assets.py`、`test_verify_entry.py` 均通过。

## [P1] 旧 Run 绑定没有随工作根保存，配置 profile 变化即无法读取

规范要求旧 Run 使用“当时布局版本/根绑定”，配置改变不得让旧引用漂移
（`docs/agents/test-asset-retention.md:18-20,53-55,96-103`）；LAYOUT 也明确要求
“旧Run保存当时配置digest与可定位绑定”（`changes/TEST-ASSET-LAYOUT-001/state.md:15,24`）。
但 `Layout.bind()` 只把绑定写在**当前** `XDG_CONFIG_HOME` 下
（`tools/test_assets.py:115-123`），`locate_run()` 也只从当前 profile 取该文件
（`tools/test_assets.py:165-176`），尽管 Run 根内已有 `layout.json`
（`tools/test_assets.py:207-211`），却完全不读取它。

可复现：以 profile-A 创建并完成 Run，随后仅将 `XDG_CONFIG_HOME` 改为
profile-B，再执行 `locate_run(repo, change, runId)`；得到 profile-B 中绑定文件
的 `FileNotFoundError`，而原 `runRoot/layout.json` 仍存在。也就是说，变更本地
profile（或清理 profile 缓存）会让完整、未覆盖的旧 Run 失联，违反 A2 的旧绑定
可读和 LAYOUT A4。将不可变绑定与 Run 工作根一同持久化，并让读取端先验证其
digest/namespace 后定位；若该绑定确实缺失，再明确报错。现有“改 parentRoot”
测试没有覆盖这一 profile/绑定漂移路径。
