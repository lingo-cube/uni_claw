# SIM-006 resolved-config 快照

> 这些是从 `evidence/sim-006/runs/` 复制出的脱敏解析配置快照。`runs/` 仍保留为本机生成目录；本目录保存可跨机器审阅的选定快照，解决 D7 的证据可见性问题。

快照只包含场景与执行输入的内容摘要、仓库提交、测试组结果、证据路径和状态分类。工具按构造不读取环境变量、凭据或 secret。TRX、完整控制台输出和构建产物仍按 `evidence/README.md` 的“大体积原始产物只留摘要+指针”规则留在本机。

## 快照来源

| 快照 | 来源 | 结果 | 用途 |
|---|---|---|---|
| `SIM-006-quick-20261005T111234Z` | 145977668 | PASS | 首批 8 场景 quick 基线 |
| `SIM-006-full-20261005T111332Z` | 145977668 | RED（文档 tripwire） | 首轮失败证据 |
| `SIM-006-full-20261005T111519Z` | 145977668 | PASS | 修复后 full 重跑 |
| `SIM-006-full-20261005T111612Z` | 145977668 | PASS | full 重复性重跑 |
| `SIM-006-full-20261005T120623Z` | 3a0a82f | PASS | 选择边界与 caveat 修复后的最终 full |
| `SIM-006-full-20261006T091209Z` | 63804f7 | RED（当前工作树） | 后续模型管理改动导致认证/测试失配的重核证据 |

每个文件的 SHA-256 和执行摘要见同目录的 `manifest.json`。快照是历史执行证据，不会替代当前场景认证，也不会把当前工作树的 RED 解释成 SIM-006 基线回归。

## 重核入口

在与快照 `git.head` 相同的干净 checkout 中运行 `python3 tools/verify-change SIM-006 --scope quick` 或 `python3 tools/verify-change SIM-006 --scope full`；当前工作树若含未完成的能力改动，认证哈希失配应保留为失败证据，不能直接重认证。
