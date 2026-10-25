# Slice 4 Standards Review v1

审查的是 `slice4/entry.json` 的 8 个候选文件，以 `slice4/before/` 和
`candidate.patch` 为基点（候选 SHA 均与 `candidate.json` 一致）；未看整仓
HEAD 差异，未执行真实 release/restore。

## Findings

1. **[P1] 外部符号链接来源可绕过跨 Run 保护。**
`tools/test_asset_cleanup.py:25-35` 的 `_physical_reference` 仅以
`abspath` 比较 `localPath` 与待释放 `root`，不会解析该 `localPath` 的符号
链接。因而其他已登记 Run 的 `provenance.json` 若记录
`<外部链接>/case.trx`，且该链接指向 `<target-run>/raw`，
`reference_blockers`（:96-118）不会列 blocker，随后 :184-196 删除目标工作
副本。隔离契约复现得到 `completed=true, deleted=4, target_exists=false`。
这违反 `docs/agents/test-asset-retention.md` §11：其他 Run 的 JSON 外部来源
须核对，链接或仍被消费的来源必须保留；也违反 AGENTS 1.5 的显式缝/验收要求。
修复应以受控、存在时解析的真实路径比较 provenance 的 `localPath`（解析失败
保守 pending），并加入该外部链接引用的 RED 测试。

其余审查范围有界通过：新 verification-only opt-in、同 Run 锁、逐文件
pending/actual 回执、归档解包复验、普通物理引用/硬链/FIFO/本仓链接、归档中
其他 Run provenance（不 restore）及严格逻辑 ref 边界均有实现与覆盖。

验证：`python3 -m unittest tools/tests/test_test_asset_cleanup.py tools/tests/test_asset_archives.py tools/tests/test_asset_operations.py tools/tests/test_test_assets.py -v`，48/48 通过；该套件未覆盖上述外部符号链接变体。
