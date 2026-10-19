# ADR-0053直连验收批次（模型运行前固定）

使用Owner已修改的DSH家目录NO_PROXY/no_proxy，Agent不改代理或凭据；仅重载已拥有的uniclaw-product 3081专线应用启动快照。正式默认Flash，Prompt/Profile17及产品实现保持。实际加载Provider产物及transport.json摘要进入每份experiment；每次trial前校验专线PID和启动环境文件hash未变。先检查真实运行时socket直连，再解释性能。

已知路径各模式计划3个有效样本，候选集合各模式2个有效样本；所有Plan固定尝试数，不因未通过追加。Step目标known3/candidates2，若有模型无效仅各允许一次替补，最多known4/candidates3，失败照常进入分子；不足有效基线则停止，不冻结伪门槛。初始Step编号known11/12/13（必要替补14）、候选11/12（必要替补13）。

所有Step完成并采集usage后，保存baseline-flash-direct-metrics.json，再运行freeze_thresholds.py --name flash-direct --known-samples 3 --candidate-samples 2，显式pin provider/model/provider产物；使用默认严格pre-Plan guard，不启用late lock。门槛按既定公式：咨询和模型调用不高于最小有效Step减1，token和正式时延按全尝试/有效完成数分别不回退。

只有冻结成功且确认未发生Plan才启动：known ShortPlan9/10/11、RulePlan8/9/10；候选ShortPlan9/10、RulePlan7/8。按Short/Rule交错运行，全部10次完成后汇总，不中途改门槛或提前停。每个组的失败、拒绝、纠正、重规划及实际模型调用均计成本，usage缺失保持未知；账单金额不可得，不推算货币节省。

同一目标/权限/逐动作guard/必要观察与独立验证，不为提速关闭安全链。新批次不与代理epoch或Pro混算收益，旧49次尝试全部保留。仅覆盖已知2动作和当前2候选/4动作，不宣称全树、现场新增C、第二真实设备或统计显著。旧动态候选/受阻/原位续行/跨结构仿真及协议回归，仅在当前哈希与独立审查产物一致时复用；不把日志状态当当前事实。

只作验收及计量元数据补充，不修改产品行为/Prompt，不提交或推送。最终仅在全部已定验收均有证据时CLOSED，否则保留未满足项。
