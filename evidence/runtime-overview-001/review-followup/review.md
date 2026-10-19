# ADR-0052 关闭后的独立复核与处置

2026-10-18，Owner要求继续并限制最多一个Luna Worker。复用同一个指定GPT-6 Luna Worker，顺序进行Spec与Standards审查，未换模型、未派子Agent。工作区恢复快照见before/与before-hashes.json，其他dirty内容仍归原Owner。

## Spec

- P1：Host把当前branch位置当作成果，Driver每次grant重置稳定事实集合，并用自由文字NextDirection识别重复路线。A/B旧状态复现或改写措辞可重复授额。
  - 删除branch成果键；全Run累计已用于授额的稳定事实。
  - 事实Owner投影ActionableItems（稳定事项键、来源、相关稳定成果键），Agent以NextItemKey引用。Driver只核对当前投影与引用、逐事项累计已用于授额的成果；文字变化不产生身份，无关事项新成果不支持重复续额。未资助的当前事项可获一次有界尝试；同事项自身有新成果可继续。
  - 独立Worker复查确认已报P1关闭；Kernel定向20/20、Host投影3/3（当时版本）。Leader随后补部分维度续额的P2边界回归，未改P1规则。
- P2：合法threshold=1在第一次复核后已无资源纠正。
  - Goal配置拒绝初始轮次<2与显式轮次阈值<2；Driver触发阈值至少2。
  - Leader追加复现：仅续步数后余1轮，不应新开必然无法纠正的复核；现在仅remaining>=2时创建新复核，现有纠正照常消耗最后一轮。
  - 初始与部分维度路径均有失败基线及回归。

## Standards

独立Worker未发现明确违反仓库架构边界的阻塞问题。提取ReviewResponse的重复分支为建议级维护风险，未在本Change顺手重构。Owner仍是既有World/任务/Runtime；新增事项只是同源投影，无第二套任务真相，Runtime不解析自由文字替Agent判断方向。

## 失败尝试

Host红灯host-red.txt；Kernel红灯kernel-red-2.txt、kernel-items-red.txt；无关成果红灯item-relevance-red.txt；部分维度红灯partial-grant-red.txt。首次全套还暴露新字段codec allowlist及公开类型白名单漏同步，失败日志保留为agent-dsh-full.txt、kernel-full.txt，均已修正后重跑。kernel-red.txt为测试字段写错导致编译失败，不能算行为复现。
