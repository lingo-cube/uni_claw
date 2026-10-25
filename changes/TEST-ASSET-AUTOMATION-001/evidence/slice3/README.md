# Run归档与恢复读取

归档/恢复这一片完成；通用删源、受保护清理、任务级联删除和存量迁移仍未启用，父Change继续开放。产品存储与并行源码保持原Owner，本片没有C#构建、真实模型/设备/服务调用、提交推送或Git维护。

## 实际修改

`tools/test_asset_archives.py`接通`archive → 实际解包 → 原消费者/引用校验 → archives索引追加`，以及`restore → 仅补缺失文件 → 原路径读取`。共享索引仍是test-assets.v1，归档位置沿原Run的不可变布局解析，当前配置变化不移动旧Run。包内文件名、完整集合、大小/SHA256、Run/来源/布局绑定均校验；不接受错包、重复成员、上跳、链接或未登记源文件。

`test_assets.py`抽出原binding loader供归档复用。`path`和`path-ref`对归档Run缺失的文件先完整恢复；不覆盖现有不同内容或未知文件/链接。归档Run的路径和validate统一核对字节及已知消费者。validate只临时解包，不写回工作根；工作目录完整时检查工作数据，不据此冒称备份包健康，备份另用verify-archive检查。

外部来源丢失时仍可restore原字节，但返回bytesValid=true、consumerStatus=pending和非0退出；validate不通过、path/ref不返回成功。原Owner修复输入后才重新有效，不伪造来源已修复。原业务结果、文件清单和摘要不改。

新归档格式v2保存每文件mtimeNs，实际发布恢复文件时保留原时间，防止旧TRX因恢复被看成新执行。早期v1包继续可读；缺少精确时间时采用Epoch 0并明确提示保守恢复，不改旧包。ZIP自身DOS时间字段允许截断，真正的纳秒时间以v2 manifest为准，所以保守恢复后的旧资料仍可重包。

## 四元组与真实恢复

| method | expected | actual / evidence |
|---|---|---|
| 公开Run归档/恢复契约 | 解包、旧配置、跨引用、错包/碰撞/链接/ZIP遍历/partial restore、外部未决、时间兼容 | 12/12；archive-final-v2.txt；归档+原操作/布局34/34见contracts-final-v3.txt |
| 原verify入口回归 | 原TRX复用、结果分类、取消仍成立 | 15/15；verify-entry-final-v3.txt；与上列共49个不同Python用例 |
| 原重放Run归档 | 真实TRX/replay.json读取，原源保留 | archive-0f4c9b465c5440349a71db074a087b93；real-existing-run-archive.json；8源文件仍保持 |
| 新建可丢弃输入副本：核验归档→逐项删除→path-ref恢复 | 原目录、全部摘要与信号引用一致；validate不偷偷恢复 | real-restore-proof.json；同一自有副本6文件已恢复，原94文件未动，净删源0 |
| 原evolution-signal.py dev CLI读回 | failureKey及源SHA与恢复前一致，不创建修复任务 | real-restore-proof.json；CLI退出0；输入仍是原已发生失败的复制品，不是新产品执行 |
| 原scenario-coverage.freshness_violation及真实文件时间 | 恢复不使历史结果变新鲜；v1不伪造时间 | mtime-red.txt→archive-mtime-green.txt；mtime-restore-proof.json显示SHA/mtime全同，真实仓库watch范围before/after均仍stale；旧包仍可读 |
| 独立双轴审查 | 可行动问题已修，边界清晰 | Standards v1/v2/v3有界通过；Spec v1两项P1已修，v2/v3有界通过；原报告全部保留 |

mtime证明复用同一组6个验证副本，没有新增一套产品运行。仅该可丢弃副本被按精确清单移除后恢复（各次实际回执见controlled-source-removal-receipt.json、mtime-removal-receipt.json）；原94个文件一直保留，当前合计100个Run文件可校验。旧重放的mismatch保持，不把归档验证说成业务通过。

真实恢复覆盖了原有读取器及引用。受控packet样本验证schema和外部输入保护，没有调用真实模型；不把该样本当作生产诊断证据。未知格式仅保全字节并说明未验证领域消费者。

## 发现与修复

- 包名碰撞时异常清理可能误删未由本次创建的包，collision-red.txt复现；现仅清理本次创建且inode匹配的未登记文件，并复核登记情况，既有包和来源不删。
- Spec发现只读校验和路径入口可能忽略外部依赖pending，现已统一状态传播和退出码。字节恢复与消费者有效分别报告。
- 原覆盖率工具还看mtime，单保留SHA不够；mtime-red.txt复现恢复刷新时间。v2和v1保守兼容已修此现有消费者契约，不绕开陈旧检查。

最初api-red.txt仅为新接口不存在；archive-first/second日志含实现联调或fixture缺少完整triage时的失败，不冒称产品业务RED。早期10例/32例记录是当时结果，最终为12例/34例。已有v1包/Run摘要/源SHA不重写，新v2包独立追加。

## 交接

只读复验使用本目录verify.py，不触发构建、模型或删除。candidate.json与candidate.patch以slice3/before为固定点；旧切片候选通过before叠加关系复核，不把新源码重哈希回旧封存。源/输入与必要差异继续保留，没有整树源码副本，也不声称完整源码封存或产品执行恢复。

后续若实施删源/任务删除，还需逐项核对来源、真实消费者、plan/WorkItem归属、活动/未决/证明/待交付与baseline保护；本片归档成功不是删除许可。前次限定清理不重复，旧fixture归属未登记的未决也未按目录名盲删。
