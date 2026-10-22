# SIM-017 设计：窗口证据与未知状态生命周期

## 结论与边界

本设计只分析 SIM-016 所需的“跨包页面还是覆盖窗”证据和未知状态生命周期。现有分类保持原样：包名与 host 不同，或白名单资源 ID 出现，仍分类为 `present`；本设计不改分类、不增协议、不冻结候选公开字段。目标是让调用方能区分“有证据支持的 present/absent”与“本周期无法判断”，并防止未知期间沿用历史 present 继续自动清障。

## 当前链路与真实缺口

`SettingsTraversalLiveFeed.Next` 每周期独立截图并尝试 UiAutomator XML dump；同周期生成 `captureId`、`observationCycleId`，并通过 `PopupProposal` 将非空分类作为 `ui.overlay.popup` proposal 投入 Kernel。`SettingsPopupClassifier.Classify` 的根依据是 XML 节点 package：只要第一个 node 的 package 与 host 不同就 `present`，否则检查白名单；XML 缺失或不可解析返回 null。它没有表达窗口层级、窗口边界、焦点窗口、覆盖关系或 Activity 页面归属。

SIM-016 独立 oracle 将 `default-apps` 回放场景标为正常页面，受控身份标题为 `Default apps`；原录制支持 XML package 与 host 不同，原 route 退为 `android.settings`，不能单凭这些材料证明当时系统窗口无覆盖。该回放场景触发测试 归档 `classification-red.cs.txt` 中的 `RecordedDefaultAppsPage_IsNotProofOfPopupSolelyBecausePackageChanged` 并得到错误 `present`。因此跨包只能证明节点属于另一个 package，不能证明它是覆盖窗。反向也成立：同包覆盖窗可能只靠白名单线索才被识别。分类规则暂时不动，避免把窗口事实缺口伪装成分类修复。

未知生命周期缺口位于账本与 Director：`SettingsCoverageLedger.RecordPopupState` 只记录 present/absent，`LatestPopupState` 返回最后一条已记录声明；无 XML 时 Feed 不发 proposal，因此账本不会因缺失自动清空。Director 在 `Snapshot.PopupState == present` 时优先走清障；缺失期间仍可能把旧 present 当当前状态。另一方面 `absent` 会重置障碍计数并恢复遍历，故不能把未知折叠成 absent。

## 证据设计（候选，不冻结）

窗口关系证据的首选来源是同一采集周期的系统窗口清单/窗口管理观测，由 Host acquisition adapter 获取；需保留原始来源及采集时刻。Android hierarchy/XML、截图、包名、标题与资源 ID 是同周期辅助材料，不单独提升为“覆盖关系”事实。EvidenceLedger 负责接纳和记录；World 负责关联与状态/冲突投影；Host 负责采集和来源标注；Director 只消费经过当前性判断的状态，不自行推断窗口语义。模型输出可作待审候选，不能充当窗口事实或自动解除清障的权威。

候选字段族仅供后续契约设计讨论：采集批次/捕获 ID、采集时间、run/session 关联、窗口来源及来源版本、窗口标识/包名、层级或覆盖关系、支持/反驳/不可判定、关联的 hierarchy/screenshot evidence ID、有效性与冲突状态。命名、类型、subject、存储形态均未冻结；不得把这些候选直接当作公共 API。

关联优先级：同一个 capture 的证据在同一 observation cycle 内配对；cycle 必须属于当前 Run/session。不能仅凭时间接近、相同 package、相同 route 或相邻录制帧做强关联。若来源只提供时间戳而无共同 capture token，记录为弱相关候选，不得用于自动清障。来自旧 session/run 的证据不能更新当前 popup 生命周期。

语义：本周期证据齐备且相互一致时才产生当前 present 或 absent；关键来源缺失、采集失败、解析失败、来源过期/迟到、capture/cycle/session 不匹配时为 unknown；同周期强证据互相冲突时为 conflict（对控制而言按 unknown 处理，保留冲突两侧证据）。unknown/conflict 不等价 absent，不重置 obstacle retry，不完成清障；也不能无限沿用旧 present。旧 present 一旦没有当前周期的续证，即标记 stale 并退出“当前可清障状态”，Director 暂停普通遍历与自动 dismiss，转入有界重新观察/补采。迟到证据只进入审计历史，除非仍能证明对应 observation cycle 未关闭且与当前场景一致；本轮建议默认不回填控制状态，避免场景已变后旧证据复活。

## 未知时的停止、补采与恢复

1. 当前 cycle 得到 unknown/conflict：冻结该 cycle 的自动 popup 处置和普通目标遍历；不把旧 present 当作当前事实，不发确认侧或 dismiss 效果。保持 Run 可恢复，记清阻塞原因及证据缺口。
2. 在配置的有限窗口内重采新 cycle（截图、hierarchy、窗口来源尽量同批）；每次补采使用新的 capture/cycle 关联，不覆盖旧证据。补采次数/时限使用现有预算或由后续 Owner 裁决，不在本设计虚构数值。
3. 新 cycle 得到一致 present：恢复既有清障路径，但仍受既有安全白名单/回退键策略约束；得到一致 absent：清除 stale 状态并恢复普通遍历；仍 unknown/conflict 或预算耗尽：安全停在 bounded stop/等待人工恢复，不盲目重发上一步动作。
4. 恢复后重新观察当前场景，确认仍属于同一 Run 与新 cycle，再继续；不得重放已 dispatch 的动作。旧采集可用于解释，不可冒充新鲜证明。

此流程不改变分类器输出，只定义该输出什么时候可代表“当前状态”，以及未知时的控制后果。未知停机是 Host/Director 的安全决策，不是将未知改称 absent。

## 正反例与影响映射

| 场景 | 证据解释 | 控制结果 |
|---|---|---|
| Default apps 页面，XML 跨 package，无窗口清单证明覆盖 | 跨包只支持 package 不同；覆盖关系 unknown | 暂停 popup 自动清障，补采；不得把分类器 present 升格为确定覆盖 |
| 同周期窗口来源明确报告系统对话框覆盖 Settings，XML 同周期可配对 | 窗口关系支持 present | 既有清障策略可运行 |
| 同周期窗口来源足以证明正常系统页且无覆盖，XML 仍跨包 | 包名启发式不是独立覆盖证据；不能自动制造强证据冲突 | 能否支持 absent 依赖来源完备性裁决；不足则 unknown，不清障 |
| 白名单资源在同包层级出现，但没有窗口/覆盖证据 | 资源命中只支持既有分类，不证明窗口关系 | unknown，等待补证 |
| 同周期强证据显示 Settings 前台且无覆盖窗口，未见白名单 | 支持 absent | 清除 stale present，恢复普通遍历 |
| XML 缺失、解析失败、采集源超时 | 无否定证据 | unknown；不得清障，也不得当 absent |
| 上一 Run 的 present 晚到，当前 Run 已换屏 | session/cycle 不匹配 | 仅审计，不更新当前状态 |
| 旧 present 后连续若干周期缺失 | 缺少续证 | 标记 stale；在有界补采后停机，不能无限持续清障 |

影响映射：Feed/acquisition 提供同周期来源与关联材料；Kernel/Evidence 保持证据来源、时间与冲突可追溯；SettingsCoverageLedger 只应记录/展示派生事件；current/stale/unknown 的权威推导应复用既有Owner和新鲜度机制，不能在覆盖账本另造判定真相；Director 对 unknown/conflict 执行暂停及有界恢复；分类器与 `ProductAssociationStrategy.PopupSubject` 现有分类语义保持不变。当前 trace 中 `Popup` 字段仍只是分类输出摘要，不足以替代窗口来源证据。

## 可判定 Acceptance（后续实现用）

- 回放 `default-apps` 跨包正常页面时，结果可说明“package mismatch”但不能声称覆盖窗已被窗口证据确认；既有分类器仍原样输出 present，直至另一个已授权 Change 修改它。
- 同周期、同 Run 的窗口支持证据与 hierarchy 证据可关联；跨 cycle/session、缺失 token、旧证据均不会进入当前状态。
- 缺失、冲突、迟到均映射 unknown/conflict，并保留来源；任一都不会被折叠成 absent。
- 旧 present 在失去新鲜续证后不再驱动 Director 自动清障；unknown 有界补采，耗尽后停止且不重发动作。
- 只有新鲜且一致的 present/absent 能分别恢复清障/普通遍历；迟到结果不得复活旧状态。
- 新增公开类型或字段前需单独审查职责、消费方、兼容与状态转移；本设计中的字段均为候选。

## 尚待 Owner 裁决

1. 允许的窗口证据提供者及权限边界：系统窗口管理快照、Accessibility window 信息或其他 Host API，需结合目标 Android/API 与部署权限核实。
2. “足以证明 absent”的最小证据组合，以及来源冲突的优先级；现有材料不足以冻结阈值。
3. unknown 的补采次数/时限应复用哪一层预算，以及耗尽时是 Run bounded stop 还是显式等待人工输入。
4. Late evidence 仅审计的默认是否适合所有消费场景；若将来允许回填，必须定义 cycle 未关闭、场景未改变的可验证条件。
5. 是否需要把 stale/unknown/conflict 建模为 popup 专用生命周期，或由通用 Evidence freshness/validity 承担；不得在实现前预造第二套生命周期。

本任务到此只给出设计与源审计；候选协议、API、字段和阈值均不冻结。

后续实现应统一升级既有观察缝和状态消费，不保留两套独立的 popup 真相或旁路授权器。当前任务只冻结问题与证据要求，不冻结上述候选方案。

Root复核补充：独立oracle的正常页标签是本回归用例的预期，不是原始系统窗口栈事实。观察补证复用既有Host/driver流程，不在Effect Gate请求模型，不新增清障授权；CoverageLedger仅消费Owner派生投影。未来若升级公开契约，需在单独Change完成Owner与兼容裁决。
