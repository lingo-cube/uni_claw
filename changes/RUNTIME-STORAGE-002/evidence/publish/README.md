# 存储收尾联合验收与发布

## 做了什么
核心资源、Control/Plan和Host投影保留确认的最新状态及可逆版本；World辅助历史采用周期基线与增量；run-report和Coverage保存必交付要求、固定输入及独立交付状态。开发资产已完成的布局、清单、归档/恢复和显式工作副本释放一并发布。

## 原来的问题与修正
Host坏snapshot曾被视为不存在而重铸幂等Run，关闭后的身份/启动写入曾只改内存；现在先确认保存再发布状态，坏核心来源明确失败。Coverage原来直接输出文件，任务返回不代表报告可靠交付；现在执行前接受要求，唯一report/steps与附件固定，仅报告重试不重跑任务。初审发现的RunId、大小写附件和任意生成器替换缺口均有RED、修复和双轴复核。

## 最终四元组
- method：固定候选公开模块、真实确定性Runner/HTTP/进程Kill与旧源对照；私有index导出的源独立构建，实际文件SHA与将提交blob逐项核对。
- expected：原任务结果与事实保持，已确认状态/历史可验证，报告跨重启可查可重试，缺必要输入明确失败，历史和幂等身份不改写。
- actual：Host411/411、Dsh42/42、Python67/67；Kernel166/166的已验代码与最终候选一致。World成本工具和既有Replay载体编译通过；机械源/结果核对811项通过。各片及集成范围最终双轴均无剩余问题。
- evidence：verification.json、final-source-map.json；host-coverage-final-v3.json、dsh-coverage-final-v4.json、kernel-closure-v3.json、python-final-v6.json；上级各片独立证据与评审。

## 依赖与共享工作区
从原HEAD建立独立index，只纳入Storage及已完成资产切片，并闭合已关闭Owner的必要接口。SIM-019三份native Overlay provider逐字节等于首片封存；后续Act/authored ExpectedEffect与独立观察恢复配置的混合hunk留在原工作区。ScenarioTests仅加partial，原断言不降；GC测量的原串行collection和真实fixture随测试保留。

配置选择为CLI、环境变量、本地profile，选一份完整配置。绝对parentRoot加相对work/archive路径、repositoryKey、Change/Run，再加相对categoryPaths形成实际目录；配置digest绑定旧Run，不随本机profile漂移改写。产品报告路径由产品显式options管理，不读取Harness资产JSON。

## 资产与边界
正式代码、测试、契约、紧凑结果/源清单及评审进入Git。原始journal、sealed源码/输入、旧失败日志、TRX、归档与中间/并行patch保留本地，不重写、不删除；历史脚本依赖本地原材料的事实明确保留。World成本工具直接编译的那一份封存v2 codec属于必要源码依赖，单独列入发布例外。具体清单见evidence-publication-plan.json，实际临时构建目录处置见finalization.json。

本轮关闭RUNTIME-STORAGE-002。开发资产父任务的任务级联删除、自动GC及存量迁移继续按原边界未启动，不纳入本次产品收尾。无设备/模型、掉电/跨节点或全产品通过声明；源码版本还原不授予执行许可。最终由Leader在授权的uni-harness执行普通提交和非强制推送，远端结果另行核对。
