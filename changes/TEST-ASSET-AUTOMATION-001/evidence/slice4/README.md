# 归档工作副本的受保护释放

## 做了什么 / 原来的问题
原来archive只写包，恢复证明中的移除由Leader逐项执行。现在新增`test-assets release-work`：只处理创建时明确登记的新verification Run，复验实际v2包和原消费者、来源/引用与精确文件身份后，移除可恢复工作副本；旧Run没有这项授权。

## 这次修改
- `tools/test_asset_cleanup.py`负责引用检查及逐文件释放/回执；`test_assets.py`保存创建者声明并提供Run锁；archive/restore共用该锁。
- 引用检查包括被ignore的仓库计划、WorkItem、文档和索引、符号链接，以及其他登记Run的JSON/provenance/packet；只在归档中的依赖临时解包读取，不恢复。有效完整逻辑文件引用可继续，物理路径/未知引用或未决依赖阻止释放。
- `test_asset_operations.py`的begin显式接收理由；默认verify/replay/诊断均不自动启用。C#诊断和产品存储没有修改。
- 所有原文件清单、summary/finalization及归档保持，处置回执单独追加`assets.json.workReleases`。pending先于unlink持久化，每项实际删除后计数；重试不把alreadyAbsent重复算删除。

## 验证结果
`contracts-final-v5.txt`为50项工具契约，`verify-entry-v1.txt`为15项原验证入口契约，共65项。真实受控副本通过begin/finish/archive/release-work/validate/path-ref及原`evolution-signal.py dev` CLI：第一次移除6文件→validate只读查归档→按原根恢复6文件→源SHA/失败key/mtime一致→原读取者结束后再次释放。旧100个物理文件全部保留，新6文件最终只在归档中，净移除工作副本逻辑10,630字节，新增包6,747字节；不是磁盘物理空间测量，也不是新产品执行。

初始`cleanup-red/green`日志是测试模块同名导入问题，改名后解决，不冒称行为RED。实际行为RED在`symlink-reference-red.txt`、`nonregular-red.txt`、`cross-run-reference-red.txt`、`policy-and-reference-red.txt`，分别覆盖仓库链接依赖、FIFO、其他Run外根来源与policy/ref完整边界。Standards v1另复现外部alias符号链接绕过（`external-alias-red.txt`），已用真实路径resolve修复并通过v2复核；实仓兼容明确声明repository根的手工清单，v3复核。归档恢复真实证明先跑，随后补足上述保护并用最终定向测试与只读前置检查复核；没有为新增保护重复制造/删恢复副本。

独立规范/需求报告见`review-standards-v3.md`与`review-spec-v3.md`。最终hash和精确前片差异在`candidate.json`/`candidate.patch`；`before/`保留第三片原字节，旧封存摘要未重写。`verify.py`仅检查，不恢复或释放。

## 新发现 / 限制
外部来源protectedRefs只保存逻辑身份，物理路径在原生产者provenance，不能靠相同SHA猜归属。引用检查覆盖已登记来源及仓库引用面；仓库外未登记消费者和构建/依赖目录读写者，由创建者显式保证结束。工具拒绝已知或检测到的并行漂移，不能把任意外部进程变成遵守Run锁的参与者。

## 下一步
任务级联删除仍未实现；须先做显式任务退役/依赖解除，再按归属级联索引、过程证据、工作根和归档，保持独立baseline。存量迁移和自动临时GC也未完成，父Change继续开放。产品后续工作仍等原Owner最终契约，不提前接管、不创建监控。
