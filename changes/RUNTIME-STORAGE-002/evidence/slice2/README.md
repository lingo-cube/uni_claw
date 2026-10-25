# RUNTIME-STORAGE-002 第二片：必交付 run-report

完成的是首个 run-report buyer 的可靠要求、固定来源/生成器、独立交付和重开后报告级重试。父 Change 继续实施；核心最新/正反增量、其他报告 buyer、快照、辅助维护与总体性能尚未完成。未提交/推送，未运行真机或产品模型。

## 代码与行为

- `src/UniClaw.Host/Runtime/RequiredReportDelivery.cs`：Accept → Prepare → Publish → Confirm，最新 head 指向已确认的小版本；固定输入/脚本和 publication receipt 逐项校验。失败保留要求与可恢复来源。Delivered 是历史事实，当前可读性另查；损坏不能伪装成功。
- `src/UniClaw.Host.Dsh/RuntimeToolHost.Reports.cs`：从 registry 找真实脚本，以 `--describe-inputs` 唯一清单固定实际材料，重试用私有字节。Runtime facts.RunId 和手工 artifact-set 目录名绑定，生成前再核验私有 Runtime 输入。Python 自持 OS 锁，Host 单杀后仍存活的生成器阻止重叠执行；明确返回 busy，不调用 Agent/Effect。
- `RuntimeHttpServer.cs`：显式 requireReport 先可靠接受再 attach/dispatch；任务先终态，之后报告结果仅经 `RuntimeRunStore.UpdateArtifacts` 更新可用性。`RuntimeBackgroundWork` 和 ToolHost 关闭分别有界等待/取消、输出未确认引用。启动调用顺序经静态独立评审；未做真实 DSH 启动或模型联调。
- `ReportDeliveryEndpoints.cs`：HTTP 查询、显式 retry 和有界核验产物读取。普通 legacy 直接工具接口保持，旧目录仅为兼容镜像；报告权威产物在 `.required-reports`。

## 精确候选和复现

`candidate-v2/manifest.json` 固定27个所属文件及 entry-to-owned diff；不是宽 HEAD diff。`build-binding-v2.json` 绑定1007项输入与44个程序集：首片原输入归档及v6测试 overlay + 本片v2 + 三个只读测试依赖。只读 registry、procedure carrier 和旧确定性退出脚本来自共享入口现状，不归本片改动。

核验封存，或在新临时目录重建并运行相同定向测试：

```sh
python3 changes/RUNTIME-STORAGE-002/evidence/slice2/verify.py
python3 changes/RUNTIME-STORAGE-002/evidence/slice2/reproduce.py
```

重建使用本地 .NET/Python 和已声明依赖，包恢复可能需要缓存或网络；它不调用产品模型或设备。临时目录、日志及TRX会输出，不覆盖用户工作区和封存。二进制指纹记录的是本次构建，不要求不同临时绝对路径的重建二进制相同。

共享工作区另有 `SettingsPageScene` README 登记，原样保留并单列 `parallel-readme-v2.patch` / `workspace-binding-v2.json`。本片生产代码与测试匹配冻结候选；其他任务的业务代码不进入纯候选。

## 验证四元组

| method | expected | actual | evidence |
|---|---|---|---|
| 隔离公开存储/终态产物更新及原消费者 | 要求/输入/失败可重开；原终态与业务结果不变；损坏拒绝、超时保留租约 | Host23/23，其中新增17例 | `pure-host-v2.trx` |
| 隔离真实脚本/HTTP/兼容工具 | 同要求固定脚本、来源匹配、交付可读、重试幂等；旧工具/诊断消费不退化 | DSH50/50，其中新增12例；诊断测试为fake transport，无产品模型 | `pure-dsh-v2c.trx` |
| 生成器真实CLI | 输入清单确定且 source-label 仅改变展示来源，旧默认不变 | 2/2；PNL006原golden字节一致 | `input-contract-final-v1.log`、`legacy-golden-final-v2.log` |
| accepted / ready / generating / published 实际进程 Kill | 新进程发现同要求，必要来源可恢复，既有immutable文件不变 | 4/4；ready后删除外部输入/脚本仍交付；第二reader不再生成 | `pure-runtime-evidence-v2/crash-tests/*/phase-summary.json`、`immutables-summary.json` |
| 只杀 Host，保留真实 Python | 遗留本体仍活时新Host不进入第二次生成，结束后可重试 | RED重叠调用2且错误成功 → GREEN调用1/busy；释放后同id交付、重复不再生成 | `orphan-tests/orphan-red-v1.trx`、`pure-runtime-evidence-v2/orphan-tests/*/observed.json` |
| 错来源与正常关闭 | 拒绝错Run而不错误绑定；关闭有界且未决可见 | owner两例RED→GREEN；关闭三例GREEN，真实Python退出、失败要求/来源仍可查 | `tool-tests/report-owner-*`、`shutdown-tests/`、`pure-runtime-evidence-v2/shutdown-tests/*/close-summary.json` |
| 独立双轴评审 | 规范/需求无剩余可行动缺陷 | v2两轴0；初始三项实际缺陷已修 | `review-spec-v2.md`、`review-standards-v2.md` |
| 封存与兼容保护 | 首片196项及本片输入/证据摘要不变 | `verify.py`逐项检查 | `verification-v2.json`、`final-artifacts-v2.json` |

Standards v1 要求另造报告批量队列的初始意见已按 ADR-0055“单条逻辑操作亦可异步提交、不能跨确认屏障凑批”复判撤回，保留 `review-standards-v1-applicability.md`。没有据此声称已证明批量收益。

## 失败尝试和边界

- 已形成的 RED、错误断言、构建/准备失败和对应日志保留；未形成持久TRX/日志的探针准备不计作行为证据。纯DSH首轮24/50失败源于未复制用于发现repo的 registry；补齐后49/50，余下一例缺旧退出脚本；加入被实际读取的readonly输入后50/50。没有为获得通过而改变产品或原断言。
- 共享较早v1全量观察：Host958通过/14失败/1跳过（12个fixture哈希门、随机OwnerId比较、一个全量内存测量失败）；DSH92/93（已知partial overlay）。内存例单独运行通过，成因未定；未改这些路径。它们是 `host-full-v1.trx`、`dsh-full-v1.trx`、`world-streaming-isolated-v1.trx` 的观察，不是最终纯v2全量通过声明。
- 正常关闭与子进程终止在本机进程范围验证，不扩大为断电/OS崩溃、跨节点或远程发送保证。输入来自已停止写入的产物集，未声明跨Owner全局快照。来源未固定且丢失时明确无法生成，不重跑任务。
- 必需来源与已交付报告没有自动回收。`report-added-cost-v2.json` 对实际生成器＋合成facts单列新增存储：含兼容镜像89475字节，6个小元数据版本；这不是压缩、批量flush、峰值内存或速度收益证明。
- 后续先做增长型核心账目的最新/版本化增量与正反历史，再按World/报告读取成本决定快照和辅助保留。新快照不授权删除旧增量，读取历史不授予继续执行许可。
