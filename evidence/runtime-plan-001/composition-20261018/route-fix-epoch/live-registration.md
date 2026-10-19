# 组合增量现场对照：运行前注册

本注册在Profile18/schema11eb6875…下首次模型/设备运行前写入。历史批次不混用；selected仍为DeepSeek Flash，正式3081、Owner家目录DeepSeek直连例外、同一注册模拟器、权限、观察及逐动作独立验证不变。仅mode引导不同，Prompt/Profile/二进制在整批固定。

任务：known-path进入Network & internet并返回根页；nested-scope处理Network & internet及Connected devices，按现有Owner配置每个一级页最多发现一个允许的nested入口，观察控件并逐层返回根页。MaxDepth1既有语义为一级页之后一层nested-enter。未来子页名字不预列，开关不切换。该范围不声称遍历全部Settings。

每任务Step先取得2个有效样本，最多3次尝试（仅一次失败替补）；任一任务基线不足则停止Plan。全部Step结束后且未开始任何新Plan时冻结数值门槛。每任务随后ShortPlan、ComposedPlan各固定2次，无失败替补，无结果触发追加。ShortPlan仅有限具体叶步骤；ComposedPlan首次真实提交必须实际包含If与Repeat，通过运行中Owner事实展开。全部尝试、失败模型调用/token/时延均保留。

功能有效须Completed、Owner CoverageComplete、mode形态遵从、无执行或清理异常；known恰2个verified动作，nested至少6个verified动作且至少一次非根页间已验证进入。此处测试阈值只是最小反例；另逐条审计实际动作、Owner条件、未提前列出的子入口、返回及开关观察证据，不用动作数代替真实遍历证明。

门槛公式按每任务有效Step标定：咨询上限=min(有效Step咨询)-1；实际模型调用上限=min(有效Step调用)-1；token与正式耗时上限分别为全部Step尝试总量/有效数。每个Plan模式同样以所有固定尝试总量/有效完成数摊计，必须2/2功能有效且四个收益门槛全部通过。完整session usage必须可得，货币费用不推算；预热剔出正式计时，慢感知调用计数不得漏记。未通过如实保持VERIFY。

批次由live_batch.py串行运行；固定前缀composition-r18-routefix-，原始trial路径仍在comparison下，阈值及本批汇总只写本目录。只允许已冻结同条件；artifact/provider/服务PID/启动环境指纹漂移即停止。同一模型重复2次仅为小样本工程验收，不外推普遍提速、纯网络耗时占比或第二真实设备泛化。

本独立epoch在任何新Plan前重新注册：上一epoch5份Step（2有效、nested0有效）因基线不足停止，60次实际调用、1,808,383报告token全部保留在failed-epoch-baseline-metrics.json，不混入不同Host产物的新收益分母。新产物只修正Host真实页面标题来源及每页候选上限，Kernel/wire/Prompt/Profile/模型/权限不变；仍需重建两任务的全部新Step、冻结，再固定8份Plan，不拿旧基线追过门槛。注册及计量脚本在首个新Step前完成；缺失正数token或正式耗时不得通过，Plan之前比对冻结全部相关artifact/config指纹。
