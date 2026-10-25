# Python 资产消费者前置闭包

本记录是只读验收：`tools/test_asset_archives.py` 的 `consumer_checks()` 读取 TRX 时通过 `runpy.run_path()` 加载 `tools/evolution-signal.py`，并调用其 `scan_dev(trx_paths, root, context=...)`。因此该文件是归档 51 项私有发布视图的实际 Python 消费者前置，不是产品代码缺陷。

## 实际合同

- 本地 import/reader：`tools/test_asset_archives.py:139-164`；`tools/tests/test_verify_entry.py` 也直接复制并执行该入口。
- CLI 合同：`python3 tools/evolution-signal.py dev --trx PATH --intent verify|expected-red --out-dir OUT [--composition PATH] [--known-issues JSON]`；运行诊断兼容 `run --run-dir DIR --out-dir OUT [--diagnosis PATH]`，也接受直接的 `--run-dir/--out-dir` 形式。
- reader 输出：`scan_dev()` 返回 `uniclaw.evolution.signal.v0` 信号列表；消费者随后读取 `schemas/evolution-signal.schema.json` 校验 JSON 信号及来源 hash。该工具只生成非权威 evidence，不创建 Change、不执行测试、不授予修复权。
- 相关 schema/契约：`schemas/evolution-signal.schema.json`、`changes/EVO-002/signal-contract.md`。

## 所属 closed Change

最小闭合前置为 `EVO-002`（`changes/EVO-002/state.md`，`lifecycle_state: closed`）。其 Scope 固化统一信号协议、dev-verify adapter、live-run adapter 和 INTAKE 去重；Acceptance A1–A7 记录该工具的真实入口与 schema 验证。`EVO-003` 只负责 runDir→case 重放导出，不能替代本入口。

## 发布视图结论

私有发布视图若要让 `test_asset_archives.consumer_checks` 完成 TRX 的既有消费者检查，必须纳入 `tools/evolution-signal.py` 及 `schemas/evolution-signal.schema.json`，并保留其相对路径合同。当前缺失是视图前置材料缺失；不应通过修改 `test_asset_archives.py` 绕过 reader。`test_verify_assets.py` 的 `test_verify_entry` import 问题属于执行器 `tools/tests` 的 `PYTHONPATH` 配置，不属于产品或该消费者前置。

本记录不宣称 51 项验收通过，也不修改源码、资产索引或 Change 状态。
