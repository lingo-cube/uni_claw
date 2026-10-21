# 索引补齐验证记录（docs/analysis + docs/design README）

> Date: 2026-10-22
> Executor: GLM doc-index worker（DSH 会话）
> 关联盘点：`inventory.md`（同目录）

## 1. 验证方法

一次性内联脚本（未在仓库新建任何索引生成器/工具）：解析两个 README 的表格首列路径 →
(a) 以 README 所在目录解析相对路径验存在性；(b) 全表查重；(c) 目录内全部
.md（排除 README.md）与 .html 反查是否均已索引。

## 2. 链接检查结果（整理后）

```text
== docs/analysis ==  索引行数: 36
重复行: 无
失效路径: 无
目录正文文件数: 36  未索引: 无
索引中不属于本目录的行: 无

== docs/design ==  索引行数: 38
重复行: 无
失效路径: 无
目录正文文件数: 36  未索引: 无
外部引用行(指向目录外): ['../../plans/2026-09-19-core-010-reliable-execution-record.md',
                        '../capability-hub/customization-integration-development-protocol-v0.1.md']
索引中不属于本目录的行: 无
```

design 38 行 = 36 个目录内文件（33 md + 3 html）+ 2 个外部交叉引用（均存在）。

## 3. 实际 diff（git diff HEAD --stat）

```text
 docs/analysis/README.md | 23 ++++++++++++++++++++++-
 docs/design/README.md   | 15 +++++++++++++--
 2 files changed, 35 insertions(+), 3 deletions(-)
```

构成：analysis +14 新行、1 行状态修正、+8/-1 行上下文计为他人先前未提交的 7 行（详见 inventory.md §5，其 diff 计入本 stat 因为对比基点是 HEAD）；design +11 新行、2 行单元格修正（路径/状态）。他人行未删改。

## 4. 仍需人工判断的条目（未修改，仅登记）

| # | 条目 | 现象 | 待裁决 |
|---|---|---|---|
| 1 | `docs/analysis/dsh-tool-scope-restricted-sessions.md` | 索引状态 `COMPLETE / EVIDENCE-BACKED`，头部仅 `COMPLETE` | 索引附加段是否有据；保留或对齐头部 |
| 2 | `docs/analysis/fast-perception-strategy-seam-fate.md` | 同上（索引多 `/ EVIDENCE-BACKED`） | 同上 |
| 3 | `docs/design/uniagent-dual-realization-architecture-v0.1.md` | 头部状态共 4 段，索引只列前 2 段（缺 `UAR-001_CLOSED / R1_NOT_AUTHORIZED`） | 是否补全后两段 |
| 4 | `docs/design/memory-system-component-declaration-v0.1.md` | 索引 `CANDIDATE / DECLARATION`，头部仅 `CANDIDATE` | 附加段保留或对齐 |
| 5 | `docs/capability-hub/customization-integration-development-protocol-v0.1.md` | 文件名/标题 v0.1，Status 头 v0.2；且该文件正被并行修改（工作树 M） | 版本命名与头部不一致由 owner 裁决；本次索引已按头部 v0.2 |
| 6 | `docs/design/state-machine-viz-v0.1.html` | HTML 无状态头，索引状态沿用本目录 html 先例（DRAFT / INTERACTIVE-VIZ） | 是否需要正式状态体例 |

## 5. 结论

验收判据逐项：全部正文可从相应索引定位 ✓（36/36、33md+3html/33md+3html）；无重复行 ✓；
无失效路径 ✓（原 1 处失效路径已修正）；无错误状态 ✓（原 2 处过期状态已按文档头部修正，
4 处「索引附加段」不足以判错，登记待人工）。未重跑产品测试（索引为文档面，DocsMetadataTests
执法对象为各文档头部，README 豁免）。未提交、未推送。
