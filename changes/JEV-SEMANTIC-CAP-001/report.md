# 语义能力与World联合交付

2026-10-21。本轮实现与组件联合验收完成；模型仍作为辅助语义来源。没有更改运行中的DSH供货配置、操作设备、提交或推送。World独立实现记录在WORLD-SEMANTICS-002。

## 做了什么

把原来的语义协议marker补成可调用的能力，提供Jev实现，并验证结果能通过证据链进入World现有Slice。调用方通过同一Registry/L1接口替换供货，World继续决定身份与关联。

## 原来的问题

`ISemanticPerception`只有声明，没有执行载荷；实验脚本的布局、前页、动作、预期和Jev回答还不能直接作为产品能力消费。World另有迟到身份回退、冲突推进身份和临时occurrence id造成重访签名变化的问题。

## 这次修改

| 打开文件 | 行为 |
|---|---|
| `src/UniClaw.Kernel/Capability/SemanticPerception.cs` | 统一当前/前页布局、动作来源、预期意图和独立观察/关系/符合性结果 |
| `src/UniClaw.Kernel/Capability/UniPerceptionCapability.Semantics.cs` | 当前关联/新鲜度/几何校验、128KiB请求界、同次尝试去重、预算/取消、结果校验；缺前页不判断关系，冲突保留独立观察 |
| `src/UniClaw.Host/Perception/JevSemanticPerceptionAdapter.cs` | 注入模型绑定/配置/HTTP；布局转Jev选择题；标题只选实际当前文字；HTTP/JSON/model/choice错误零观察；前页描述和预期明确分区 |
| `src/UniClaw.Kernel/Perception/SemanticObservationProjector.cs` | 仅观察成为ui.semantic.page，带全部父证据及叶闭包，经P2而非直写World |
| `src/UniClaw.Kernel/World/WorldModel.Semantics.cs`（World会话） | 已接受语义明确关联到当前身份观察后补入Slice；类别不作身份，迟到/错帧不贴新页，未知字段不进入事实 |
| `src/UniClaw.Kernel/World/WorldModel.cs`、`src/UniClaw.Host/SettingsPageIdentity.cs`（World会话） | 修复迟到/冲突推进与重访签名；保留原有导航/滚动/身份权威 |

调用链：`Registry.Resolve → ISemanticPerception.ObserveSemanticsAsync → UniPerception执法 → Jev供货 → SemanticObservationProjector → UniKernel.Process(P2) → World → Slice`。替换确定性供货与Jev供货使用同一消费闭包。现有Slow请求、P22、Assurance和Effect协议未加预期字段。

PER-022的两个内部Fast strategy接口已收层，公开live构造入口及trace/metrics/cache使用方式保留。公开面清单、源码归属、CONTEXT、组件基线v0.3、开发指南v0.2及Skill的过时状态说明同步；新能力协议名称/版本仍为兼容的Semantic Perception@1.0。

## 验证结果

| 验证 | 实际结果 | 证据（仓库根相对路径） |
|---|---|---|
| 契约/World联合/公开面 | 17/17（13+3+1） | evidence/jev-semantic-cap-001/green-review-input.log |
| Host全量 | 400/400 | evidence/jev-semantic-cap-001/host-reviewed-final.log |
| Simulation全量 | 250/250 | evidence/jev-semantic-cap-001/simulation-reviewed-final.log |
| DSH兼容 | 3/3 | evidence/jev-semantic-cap-001/host-dsh-compatibility.log |
| Kernel全量 | 1011/1014；3项外部既有问题 | evidence/jev-semantic-cap-001/kernel-reviewed-final.log |
| World范围专项 | 100/100；负责文件冻结12项无漂移 | changes/WORLD-SEMANTICS-002/evidence/；world-freeze-check.json |
| 场景认证 | 28文件，0违规；20个有执行载体场景刷新 | evidence/jev-semantic-cap-001/scenario-check-final.log |
| 代码评审 | 规范轴无阻塞；需求轴2项均修复并独立复查 | evidence/jev-semantic-cap-001/review-{standards,spec}.md |
| 凭据检查 | 143个本轮证据文件扫描，0处实际凭据 | evidence/jev-semantic-cap-001/credential-check.json |

Kernel剩余3项明确为：其他文件SettingsNavigationScope/ProfileIdentityRenderer的README缺登记、原复用分析缺元数据、Simulation原Agent.Dsh项目引用违反旧闭包检查。本轮补齐自己的登记，不修改这些其他变更；全量结果保留失败，不跳过后宣称全绿。组件验收据本次范围与实际调用链完成。

真实.NET Jev最终请求格式回放12例（4历史页+8构造对照）：页面用途11/12，前后关系12/12，预期符合性11/12，另1例明确Conflicted；P2入账及Slice关联12/12，身份保持/未造导航边12/12。完整输入/请求/响应/结果见`evidence/jev-semantic-cap-001/live-v2/`。初版回放为用途11/12、关系/符合性12/12，保留在上级目录；由于修复请求分区而重新验证，没有选择最好的一轮充当最终结果。

## 新发现

最终错例`p04-constructed_other`把App notifications列表认作通知总览（置信度0.76）；`p13-historical`正确认出列表，却回答不符合该列表预期（0.23），L2将符合性转为Conflicted，保留独立观察。这证明模型会混淆相近页面或自相矛盾，供货成功/入账成功并不等于判断正确。

评审反例也发现当前标签可夹带预期、只有条目数未限制总载荷；已禁止Current页面先验、将前页描述独立标注，并对完整typed请求设置128KiB界。反例先RED再GREEN，未重新裁决用户已接受的语义边界。

样本是重复使用的开发集，前页/元素类型人工标注并提供预期，每例单次响应；不作为生产准确率、跨设备泛化或实时设备遍历证据。供货健康只说明配置/线路/schema，不代表语义可信。

## 下一步

组合根可按`integration.md`替换供货使用该能力。本轮已完成可注册/可调用/可替换和World关联；如果要让运行中的自动遍历改用它，须明确选用这组执行输入和部署配置，再做实际运行验收。当前不能把Jev判断单独提升为到达成功、身份合并或操作授权；应继续扩大独立样本集，优先覆盖本次两个问题样例。
