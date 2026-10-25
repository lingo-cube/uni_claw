# verify-change 开发资产首片

布局由 TEST-ASSET-LAYOUT-001 验收；登记、批次和收尾属于 TEST-ASSET-AUTOMATION-001 首片。后者仍未完成其他入口与归档/清理，不关闭。产品 RUNTIME-STORAGE-002 仍由原聊天负责；本片没有改产品 src/tests、设备、模型或 .git，没有提交推送。

## 实际调用链与变化

原先 verify-change 把 TRX 写到各项目共享 TestResults，快照/信号各用固定 evidence 目录。现在 `load_layout → AssetRun → execute_verification → TRX/coverage/signals → finish` 使用同一 Run。`test-assets path/validate` 通过该 Run 当时的 namespace/configDigest 找回布局，读前验证大小和SHA256。改配置只改变新运行，旧目录未搬迁。

配置选择为 CLI > UNICLAW_TEST_ASSET_CONFIG > XDG_CONFIG_HOME/uniclaw/test-assets.json（默认 ~/.config）。只读 resolved 不创建 Run。实际本地默认配置已建立在 ~/.config/uniclaw/test-assets.json，父根 ~/.local/share/uniclaw/test-assets，repo key=uni-claw；相对 work/archives/raw/logs/results/scratch。机器绑定固定在 ~/.local/state/uniclaw/test-asset-bindings，不随配置profile改变。该目录仍须保留；丢失明确失败，不以当前根猜旧位置。

类别内多级路径合法；跨类别祖先覆盖、大小写冲突、layout.json元数据冲突、工作/归档覆盖、绝对分类、上跳和链接逃逸拒绝。登记通过 Change 级锁与原子文件替换；不同 Change 共用的 immutable binding 先完整写入/fsync，再硬链接原子发布。

资产索引保留purpose、源码/输入/配置SHA256、批次、retryOf、逐文件bytes/SHA256/retention。默认首片材料均为task验收用途。source/input指纹记录的是执行当时活跃仓库，不是完整源码封存；原始输入继续保留，不能据此删除。新登记独立baselineRefs只引用正式manifest，不取得baseline所有权。早期两个真实Run没有事后回填或重哈希。

取消时停止并等待本轮子进程组，再提取已有输出、TRX计数及pending回执。真实信号测试发现PIPE.read取消丢在途日志，已改TemporaryFile，退出后读取并自动释放。快照/信号消费异常和资产收尾错误单独记录，不改已有测试结论；未完成消费时不清理文件。

## 验证四元组

| method | expected | actual | evidence |
|---|---|---|---|
| 公共Layout/AssetRun测试 | 组合公式、隔离/拒绝、旧根/profile不漂移、重试及baseline保护 | 11/11；包含跨Change首次绑定竞争 | layout-contracts-green-final.txt |
| 原verify/coverage入口及新增资产消费者测试 | 项目只执行一次、复用TRX、失败/取消/消费异常不掩盖、真实进程退出 | 15/15，其中原入口9、新资产6；SIGTERM是真实OS信号，测试writer为替身 | entry-contracts-green-final.txt |
| 原self-test | 既有编排契约仍成立，零资产副作用 | 23/23，FINAL_STATUS=PASS | ../../../TEST-ASSET-LAYOUT-001/evidence/self-test-final.txt |
| 两次真实quick（默认/嵌套类别） | 实际TRX/日志/信号/快照全部进入各自Run，索引可校验，不覆盖 | 各39文件SHA有效；后次sourceStable=true；两次Kernel43/43、Host26/26，Simulation各1失败、认证20项stale，原结论完整保留 | ../../assets.json；TEST-ASSET-LAYOUT-001/assets.json；real-quick.txt；LAYOUT evidence/real-quick-final.txt |
| 原coverage工具读取真实外部TRX | 可读新落点，quick子集不冒充全库覆盖 | 原CLI已读取；exit=1明确缺失未请求场景及认证问题，不是覆盖率绿灯 | ../../../TEST-ASSET-LAYOUT-001/evidence/real-coverage-consumer.json 与 txt |
| 独立Standards/Spec及Leader复核 | 可行动缺陷解除，未做范围保留 | 原子发布与profile两项P1已RED→GREEN；v3两轴0。之后baselineRefs小改由Leader复核+11例，不冒称该小改再次双轴全审 | review-standards-v1/v2/v3.md；review-spec-v1/v2/v3.md；candidate.json |
| 资产收尾 | 必要证据可读，无未授权删除 | 两真实Run均全部保留、deletedBytes=0；仅3个自有/tmp支持文件核对后删除 | 每Run results/finalization.json；support-finalization.json |

真实Run：AUTOMATION `run-20261025T124731-5f80c994053341b9bb93548f8f2569e2`；LAYOUT `run-20261025T130123-3aa56049cf984806857bdb7d2071ec1b`。各Change的assets.json按logicalRoot+相对路径登记；首个Run尚无结束sourceStable测量，不能补称已测。嵌套验证临时配置已删除，完整相同配置保留在该Run的layout.json和immutable binding中，可通过test-assets path取得。

Simulation失败为 TerminalNotProven/none，原测试要求 Completed/Completion/Satisfied；本片只保留，不修改业务测试或重认证。认证stale之外不能把该行为差异解释成“仅stale”。两次quick不等于当前共享全量通过。

## 紧凑复验与恢复

`python3 changes/TEST-ASSET-AUTOMATION-001/evidence/slice1/verify.py`只读核对候选SHA、真实Run及定向输出，不调用设备或模型，也不重新跑产品测试。定向命令为`python3 -m unittest discover -s tools/tests -p test_test_assets.py`及`python3 -m unittest discover -s tools/tests -p 'test_verify*.py'`。before仅四份实际入口文件；candidate.patch包含这些基线上的实际差异与新文件，无整树副本。源码完整复现实况仍依赖保留的活跃仓库；没有把指纹冒称源归档。

最初缺模块RED只证明新增功能尚不存在；binding-race-red/profile-binding-red为真实缺陷复现。entry-contracts-green-v2/v3虽命名green，实际是失败日志，不能算通过；v4/v5及final为修复后输出。所有失败保留，不覆盖。

下一片先以不重写封存REALIZATION的调用侧适配接重放，再处理诊断调用者显式输出接线；诊断C#文件先登记共享接口归属。随后做归档解包与原消费者读取证明，才讨论清单自有清理和任务级删除。暂未实现这些能力，未扫描迁移存量，也未操作Git对象。
