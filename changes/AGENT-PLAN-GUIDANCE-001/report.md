# 动作字段与重新观察说明修复

## 做了什么
补全产品Agent已存在但未完整告知的动作字段和Reobserve规则，独立于SIM-037验证任务。

## 原来的问题
normal-003原始提交已把返回文字放入targetRole并漏填descriptor；parser没有丢字段，Runtime反馈后下一轮已修正。装配说明却只写targetDescriptor可选和Reobserve名称，未讲清Host要求与废弃尾部行为。

## 这次修改
product/prompt/uniagent-prod/03-payload-shapes.txt增加两段：角色与文字分别填写、通用可选性不替代当前Settings策略要求；Reobserve结束旧Plan并丢弃未执行尾部，Checkpoint才保留暂停位置。精确$candidate和通用role-only合法性保留。DSH副本同步；Prompt26→27、Profile27→28、hash和两项活跃装配断言随动。既有section/per-turn测试检查实际挂载内容。

## 验证结果
| method | expected | actual | evidence |
|---|---|---|---|
| 实际section检查旧装配 | 发现缺少字段/控制语义 | 1例RED，产品尚未修改时捕获 | evidence/red-mount.json、red-mount.txt |
| 固定源码下Node挂载/工件检查 | 两种挂载包含说明；同步/耦合/哈希与Skill边界正常 | 9/9通过，零Skip | evidence/green-node.json、green-node.txt |
| Profile公开加载与InitializationEnvelope | 新引用可加载，审计记录Profile28/Prompt27 | 19/19＋3/3通过，零Skip | evidence/agent-profile.trx、initialization.trx、dotnet-commands.json |
| 两轴独立审查 | 既有语义、版本规则和任务范围保持 | Standards0、Spec0 | evidence/review-standards.md、review-spec.md |
| 源码/执行物及共享保护 | 固定来源可定位，非拥有改动不被覆盖 | 摘要及归档核对；结果见verification/finalization | evidence/candidate.json、verification.json、archive.json、finalization.json |

Node测试使用mock DSH。输出中no-submit-decision是既有模拟咨询的预期收尾，不是产品模型请求。C#只执行明确filter；构建中的其他旧源码警告不表示那些测试被运行。没有调用模型、启动服务或设备，也没有认证整仓。

## 新发现
通用role-only匹配本来有效，不能把descriptor在全局schema改成必填。说明修复不证明它是normal-003整轮失败的唯一根因。准备暂停期间其他Owner的改动已单列保留，本轮未回滚。

## 下一步
SIM-037继续保持resolving、A1 FAIL/A2未运行。后续若授权真实模型验证，先固定包含AGENT-TRAVERSAL-001与本次说明的明确候选，并处理旧任务仍写单步Act的装配资格；不得覆盖旧结果或同输入重采追绿。本Change不部署、不重启已安装DSH、不提交或推送。
