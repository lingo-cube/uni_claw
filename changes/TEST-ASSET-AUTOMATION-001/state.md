# TEST-ASSET-AUTOMATION-001 — 开发资产登记、收尾与可验证归档
lifecycle_state: closed · disposition: none · depth: standard · base: a05176f3c66e0a3206f49dde463f51df1335975c

## Intent（WHAT/WHY）
把TEST-ASSET-LIFECYCLE-001既有规范接到真实开发入口。当前verify-change在共享TestResults和仓库evidence写出，资产无统一生产/读取索引；配置改变或重复运行不能可靠关联布局和来源。

## Scope / Out of Scope
依赖TEST-ASSET-LAYOUT-001已验证的布局/绑定契约：实现test-assets.v1生产/读取/校验、来源与配置指纹、验证批次/retryOf、成功/失败/取消的消费者完成后收尾；再接重放/诊断/仿真、可验证归档恢复及任务自有清单清理。
配置选择/父根与分类解析/首次verify-change路径接线归LAYOUT，不在本Change重复实现。产品剩余存储归原聊天负责的RUNTIME-STORAGE-002。本Change由local/01a1730a-957c-7ab2-8d46-2574de5a93e9接续。
不清理存量、不修改产品运行/恢复/必交付来源、不调用设备/模型。正式baseline不纳入GC。早期“不提交推送”仅约束原委派片；当前Leader按用户后续“完成一起提交推送、仓库健康”授权精确发布本次修改，不带入其他dirty。

## Decisions
- 复用LAYOUT唯一配置解析与旧Run绑定，不另造路径优先级或独立拼接规则；分类路径与保留策略分开。
- 每次执行新Run；源码/输入/配置/执行范围一致才同批次，retryOf引用已有同批Run，不覆盖失败。清单只存逻辑根/相对路径/digest。
- 先完成TRX/coverage/信号/packet等真实消费者，再保全必要证据并收尾；处置失败pending可见，不改测试/业务结果。
- 归档须实际解包核验并证明消费者可读，再处置明确自有源。保护baseline、活动/未知归属、未决、证明与待交付来源，归属核对包括计划/WorkItem。

## Acceptance
| ID | method / expected |
|---|---|
| A1 | 基于LAYOUT的真实Run生成/读取test-assets.v1，Owner/Run/用途/源码/输入/配置digest、文件bytes/SHA256与保留规则可核验 |
| A2 | 相同输入批次、retryOf与配置/源码变更新批次正确，重复运行不覆盖失败；旧绑定可读 |
| A3 | verify-change成功/失败/取消完成消费者后产生真实kept/archived/deleted/pending回执；清理失败不改原结果，无多余资产可零删除 |
| A4 | 重放/诊断/仿真出口接同一归属和消费顺序，最小复现、独立Oracle、封存输入与规定RED/GREEN不丢失 |
| A5 | 归档解包核验路径/大小/摘要，原消费者通过；再按自有清单处置，任务级删除先解除有效依赖且独立baseline保留 |

## Dependencies / Owner
布局能力依赖TEST-ASSET-LAYOUT-001；产品数据保护仍归RUNTIME-STORAGE-002。本规范不授权整仓或存量批量删除。两个资产Change同一Leader按纵向片先后实现，已有配置工作归LAYOUT保存，不回退已做修改。

## Constraints
保留所有共享dirty；入口副本只含本片实际修改文件，不整树复制或重哈希旧封存。先真实buyer再扩入口，不以本state代替实现。目录、索引、结果均非第二生命周期。

## Verification
level: CONTRACT + DETERMINISTIC + SCENARIO
method: 定向工具契约测试、真实verify-change quick新运行、清单读取校验、独立双轴审查。
expected: 先证明A1–A3首个buyer，A4–A5按后续纵向片验证。
actual: A1–A4既有切片保持，A5的显式任务资产退役完成；最终95项工具/入口契约通过，独立双轴v3无剩余问题。真实受控子进程CLI按旧绑定完成盘点、保护和退役：删除新建自有5文件、3,644逻辑字节，7个受保护文件SHA不变；退役读取拒绝、保留读取成功、原Run事实不变。LAYOUT+AUTOMATION共14Run、246登记文件、4归档校验可用，原Run/归档/baseline条目前缀保持。自动GC、存量批量迁移按本轮范围裁定排除，不再作为关闭尾项。仅对新建受控任务实际删除；无C#构建或设备模型执行。
evidence: evidence/task-delete/README.md、candidate-v3.json/.patch、regression-v4-result.json、actual-cli-v4-result.json、verification.json、verify_final.py、review-spec-v3.md、review-standards-v3.md、finalization.json及assets.json；早期切片证据保持evidence/slice1–slice4及../TEST-ASSET-LAYOUT-001/assets.json。

## Status log
- 2026-10-25 UNDERSTAND→RESOLVE→PERSIST→PLAN：用户联合交接明确授权新产物治理，已读AGENTS/UniFlow/规范与存储state/plan；首个buyer为verify-change，先配置和消费者，独立保留产品Owner及存量删除门槛。

- 2026-10-25 Scope修订（用户要求实际分Change/合理分配）：配置/父根分类路径和首次路径接线移至TEST-ASSET-LAYOUT-001；本Change负责其上登记/消费/收尾/归档。已有工作保留并按实际范围归属，产品余项由原聊天继续，不在此接管。

- 2026-10-25 PLAN→IMPLEMENT→REVIEW→VERIFY（首片）：真实verify-change清单、来源/输入/config digest、批次/retryOf、TRX/信号消费者与取消回执已接通。布局代码归LAYOUT独立验收；本Change不关闭。正式baseline只列独立引用，新baselineRefs小改Leader复核和回归，既有真实Run不回填/重哈希。首片默认保留全部验收材料；仅实际自有支持临时文件删除，未触达共享TestResults或原始输入。下一片接重放/诊断消费者，之后才实现可验证归档与受保护清理。

- 2026-10-25 RESUME（第二片）：用户要求继续并先判断前面清理是否仍需做；入口复核首片源/资产有效，旧限定删除无需重复，保护4份恢复源与78个当前验收/失败资产。plan.md登记诊断C#共享接线文件及职责，按独立Run接重放/诊断，产品存储仍归原Owner。

- 2026-10-25 IMPLEMENT→REVIEW→IMPLEMENT→REVIEW→VERIFY（第二片）：唯一布局解析接重放与显式开发诊断，原ABI/模型路由/产品来源Owner保持；真实重放失败完整保留。修正输入实际SHA与声明混用、来源误列baseline、SIGTERM登记窗口/handler恢复与finish错误覆盖返回码，最终双轴有界通过。首片SHA不重写，用slice2/before和候选v2表达演进。旧限定清理无需重做；本片均保留必要材料，遗留回归临时fixture未登记完整归属，未按前缀盲删。A5与存量待办仍open，不关闭父Change。

- 2026-10-25 RESUME（第三片）：用户要求下一步，复验第二片当前候选、94个Run文件及旧清理/恢复保护均通过。继续plan.md的Run归档/恢复读取部分；既有来源不删，只在新建可丢弃验证副本上证明删源后恢复。通用清理/任务删除未在本片启用，产品存储Owner与共享dirty保持。

- 2026-10-25 IMPLEMENT→REVIEW→IMPLEMENT→REVIEW→VERIFY（第三片）：原Run归档、实际解包、原读取器及逐文件校验、按旧绑定完整恢复已接通；解决碰撞异常清理、外部来源pending状态传播及TRX时间刷新问题。v2保留mtime，旧v1以Epoch0保守恢复，旧包/hash不重写。真实同一6文件副本两次受控恢复（包括mtime修复）均通过，原94保留，净删源0，49项Python通过，两轴有界通过。A5剩余删除/迁移仍open；没有C#构建、模型/设备/服务或提交推送。

- 2026-10-25 RESUME→IMPLEMENT→REVIEW→IMPLEMENT→REVIEW→VERIFY（第四片）：显式创建者授权仅新verification Run，release-work在v2解包/消费者/来源及链接别名依赖核验后逐文件释放。修复仓库链接、FIFO、跨Run provenance、完整逻辑ref边界、外根alias及既有repository根清单兼容；原结果/索引事实不改，准确记录partial/retry。65项通过，双轴v3有界通过，原100保留，新6副本净移除10,630逻辑字节且仍可归档恢复。任务删除/GC/迁移继续open。正式产品World→Coverage交接已收到，完成本片后按原Owner文件契约接续，不改变本Change残余状态。

## 2026-10-26显式任务资产删除（当前授权）
用户要求连续完成：删除前盘点归属/跨任务引用→保留仍使用的证据、基线及必要来源→清理允许项并留实际回执。Leader直接完成，不再逐步请求推进；只有改变已定保护规则或范围的决策才回用户。

本轮封闭范围为开发Change的登记资产，不操作Codex聊天或产品任务数据。新增公开缝：`tools/test-assets plan-delete-task CHANGE [--out FILE]`只读盘点（显式out只写指定计划文件），`delete-task-assets CHANGE --plan FILE`按当前仍有效计划执行。公共Python缝为`plan_task_delete(repo,change)`及`delete_task_assets(repo,change,plan)`；沿原CLI/资产清单测试缝验收，既有用户授权适用，不重复询问。保留state/index和最小回执作为审计，不改原Run结果/hash；显式退役的Run由既有读取入口报告不可用，不恢复已退役材料。

A5补充验收：计划列出Run工作文件、所属归档、任务内登记材料和未登记保留项，并按旧binding拼父根/分类路径；跨任务逻辑/物理/别名/目录引用、正式baseline与原来源、活动/未决、坏或未知归属保护；同任务中被保留消费者的依赖传递保留。执行复验计划与字节/文件身份，先持久化pending，逐项unlink并记实际计数，只删空目录；部分失败可在新计划中继续且不重复计数。创建/归档/恢复/释放与任务级删除共用外层锁，退役后拒绝新Run及复活。真实受控目录证明删除/保留、原消费者/来源和旧配置不受影响；原契约定向回归及独立双轴审查通过后关闭本Change。

自动GC与存量批量迁移明确不属于本轮，不继续作为本Change关闭尾项；旧数据不被新规则追认归属。仅对本轮新建且自有的受控验证任务实际执行删除，不自行选择真实历史任务删除。原sealed资料及其他任务改动保持。

- 2026-10-26 IMPLEMENT→REVIEW→IMPLEMENT→REVIEW→VERIFY→CLOSED：新增plan-delete-task/delete-task-assets公开入口；旧binding、多配置父根/分类路径、跨任务引用及保留消费者依赖保护、共享外层锁、pending先行逐文件回执、部分失败重试及退役拒绝复活已完成。评审修复当前计划/相对引用、执行中未知材料pending和诊断路径逻辑投影；95项通过、真实受控CLI通过、双轴v3通过、原清单和全部14Run可用。证据与临时物实际处置见task-delete/finalization.json。按用户连续推进指令关闭有限范围，既有产品存储2703244f不重开，Leader继续精确提交推送。
