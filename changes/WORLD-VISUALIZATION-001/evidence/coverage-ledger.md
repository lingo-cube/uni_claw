# World Visualizer 展示覆盖账

状态按当前十个 `world-visualization.v1` 示例与可操作界面记录；核心测试通过不等于界面覆盖。界面动作：时点/比较=选择或推进帧、前后比较；图=节点/边、搜索、过滤、缩放；页面=Container、正式 scope/Slice、当前/更正后解释/累计内容；详情=来源、知识、咨询、回执、验证、纠正和影响。

## MV01–MV18（`WORLD-UNCERTAINTY-001/multi-visit-completeness.md`）

| ID | 可见资料与操作 | 状态 / 限制 |
|---|---|---|
| MV01 A→B→A→C→A 导航环 | 当前图显示有依据的关系边；无访问顺序帧 | 未采集导航访问序列；关系边不等于访问记录 |
| MV02 同页重复访问、相同画面 | `scroll` 五个 capture 帧可按时点看各自 PageView/Slice | 部分；未有同画面重复访问的专门对照 |
| MV03 导航失败、重试、重复确认 | 无相应失败/重试帧 | 未采集；不能用图推断幂等行为 |
| MV04 同轮局部观察/滚动 | `scroll` 的五帧各有 exact-W PageView 与三个正式 Slice；页面可选 scope，查看累计内容 | 部分；展示受控记录中的多轮累计，不覆盖不兼容配准/失败反例全貌 |
| MV05 第一轮开、第二轮关 | 无明确状态值反转帧 | 未采集；不从文本变化推断开关状态 |
| MV06 问题→修复→再次出现 | 无三轮动态状态帧 | 未采集 |
| MV07 旧 capture 的迟到解释 | `late-result/late-c1-interpretation`；当前帧仍是 C2，详情可查看历史解释及来源 | 部分；接受的迟到结果与当前保护可见，不证明 live provider 延迟 |
| MV08 纠正旧观察、保留当前独立观察 | `correction` 的 `verified-before-correction`→`corrected-and-blocked`；页面/Slice按各自 W 配对 | 部分；实际效果影响可见，但没有独立当前动作的并列受影响/未受影响闭包图 |
| MV09 到达 B 后回纠 A | `correction` 显示真实中间消费、纠正和停止后续动作帧 | 部分；没有完整导航访问序列 |
| MV10 相同标签、不同 Owner | correction 最终帧显示多个真实 PageView 和纠正记录；可选图/页面/详情 | 部分；这组受控例成立，不泛化为所有共同标签冲突 |
| MV11 共同依据被推翻、消费影响复核 | correction 的最终帧显示撤销依据、实际 receipt/verification 与 impact | 部分；展示本例影响记录，不声称全局依赖闭包或所有分支已复核 |
| MV12 旧图重处理：相同/不同结果 | 无候选、拒绝、重处理并列帧 | 未采集 |
| MV13 不同轮上下区域内容不同 | `scroll` 五个 capture 帧可切换正式 Slice/区域 | 部分；未展示所有冲突布局及拒绝拼接组合 |
| MV14 关系依据解决后新 capture 不继承 | `context/contains-confirmed`→`new-capture-no-inherited-relation`，图/缺口/页面可切换 | 覆盖受控例；只展示关系依据不自动继承，不代表所有 gap 同时再现 |
| MV15 新旧解释冲突、目标不明 | `correction-conflict/rejected-signature-correction`显示错误签名被拒、原归属保留 | 部分；没有多种冲突目标/候选并列展示 |
| MV16 更正再次被推翻并回看多时点 | 无二次更正链 | 未采集 |
| MV17 确认重新访问同一 Container | `scroll` 有多 capture；没有独立导航确认事件 | 未采集访问确认；不把 capture 当访问 |
| MV18 同 route 跨设备/实例/父上下文 | `context` 可见多个 Container，`recorded` 明示单一录制输入 | 未采集跨设备负例；不能推断跨设备身份安全 |

## RU01–RU08（`WORLD-UNCERTAINTY-001/trusted-element-reuse-design.md`）

| ID | 可见资料与操作 | 状态 / 限制 |
|---|---|---|
| RU01 慢结果未返回时历史先可读 | `reuse/c2-before-anchor` 显示历史 reference，当前 Slice 不带旧语义 claim | 部分；是 Owner 状态，不是真实慢模型等待过程 |
| RU02 当前对应成立后沿用稳定语义 | `reuse/c2-reused` 显示 verification 与当前 correspondence | 覆盖受控正例；不声称模型性能提升 |
| RU03 历史语义与当前动态内容不同 | `reuse-negative` 从 previous Wi-Fi 到 current Bluetooth；页面和 Slice 可切换 | 部分；无明确开/关动态状态对照 |
| RU04 位置改变/不可见时不沿用旧 locator | `reuse-negative/current-capture-unmatched` 使用移动后的 bounds，且无当前 anchor | 部分；显示此例当前定位未被补继承，不覆盖全部执行拒绝路径 |
| RU05 同 route/文本但 device/context 不匹配 | 无专门跨设备/错 profile 可视帧 | 未采集；Kernel 负例不冒充 UI 例子 |
| RU06 撤销旧解释并重判消费影响 | `correction` 实际纠正帧；`correction-conflict` 拒绝帧 | 部分；本例消费影响可见，不展示多分支传播闭包 |
| RU07 局部慢结果、乱序/重复冲突 | 无此类帧 | 未采集 |
| RU08 仅 revision/config 变化时保留来源 | 无纯版本/配置变化帧 | 未采集；现有 reuse 帧另有真实内容/correspondence变化 |

## CI01–CI12（`WORLD-UNCERTAINTY-001/correction-impact-design.md`）

| ID | 可见资料与操作 | 状态 / 限制 |
|---|---|---|
| CI01 多轮重复沿用的第一版错误 | `correction` 可看实际纠正和影响帧 | 部分；没有多轮重复沿用传播链 |
| CI02 目标和验证共享错误依据 | `correction/delivered-awaiting-verify`、`verified-before-correction`、`corrected-and-blocked`可查看消费/验证/纠正 | 部分；只陈列本例记录，不证明所有共同标签都可判别 |
| CI03 已计划未 dispatch 时依据撤回 | 无 Plan-only 帧 | 未采集；没有实际未 dispatch 消费对象可展示 |
| CI04 已 dispatch、未核验时纠正 | `correction/delivered-awaiting-verify`保留实际 receipt，后续纠正影响可见 | 部分；只读投影，不执行对账或恢复 |
| CI05 可恢复前态下成功恢复 | 无恢复/补偿 receipt 或恢复后的 Owner 输出 | N/A（当前只读可视化输入没有恢复动作 Owner 输出；不以设计性 UI 合成成功） |
| CI06 前态未知或期间被用户改变 | 无恢复尝试/外部前态记录 | 未采集；当前例子不足以判定恢复安全条件 |
| CI07 不可逆、无权或结果不明操作 | impact 详情可显示本例未决状态；无上述分类的真实动作样本 | 部分；未采集分类反例，不由一般 Unresolved 推断 |
| CI08 terminal 后才发现错误 | `terminal/terminal-recorded`→`terminal-correction-review`，显示原终局和追加影响复核 | 覆盖只读状态；不证明后续获授权新动作 |
| CI09 多分支中保留独立证据 | correction 影响记录保留具体 references | 部分；没有独立与受影响分支的整套并排视图 |
| CI10 消费血缘缺失、影响范围未知 | correction/terminal 的 impact 展示 dependency coverage 与未决影响 | 部分；未单独展示多种 legacy 缺血缘分支 |
| CI11 补偿动作失败/超时/重复结果 | recovery 显示执行与结果版本；没有补偿动作自身的 owner record | N/A（本 change 的只读样本没有真实补偿执行 Owner 输出；recovery 结果不能充当它） |
| CI12 局部正确、另一效果仍未复核 | terminal impact 显示未决项；effects 详情可展开 | 部分；无多个 effect branch 的收尾闭合画面 |

## 四类缺口、尝试/提醒、scope 与 Agent 输入

| 主题 | 可见资料与操作 | 状态 / 限制 |
|---|---|---|
| Identity | `context/child-unidentified`→`child-identified`显示 gap 与后续知识 | 覆盖受控状态变化，不是 live perception |
| Attribution | 同上；多 Container 图与正式页面归属 | 覆盖本例，不泛化其它归属类型 |
| Relation basis | `child-identified`→`contains-confirmed`，随后新 capture 不继承旧关系 | 覆盖受控 relation proposal 与边界 |
| Coverage | context 帧显示 Partial/coverage gap；页面 unknown 不等于空 | 覆盖本例；未宣称整页完整 |
| 补证尝试/提醒 | `recovery/review-1`→`review-2` 同 W；差异比较显示结果版本变化、执行计数不变 | 覆盖该实际记录；不证明模型是否真实注意提醒 |
| 同 W / 同 root 的多个 Slice | `scroll` 每个 frame 含三个正式 Slice，范围下拉和差异可见 | 覆盖导出scope；不穷举所有 collision 组合 |
| Agent 实际输入 | `recorded/recorded-current` 配对 rev-2 WorldKnowledge 与实际 DSH HTTP body | 受控 HTTP provider double；最终外部 provider envelope、live model 和质量未证 |
| 未知/未导出 | correction 有真实的五个边界帧；缺省投影仍留空，context/reuse 有 gap 记录 | 覆盖“未知保持未知”；不可将空列表解释成现实不存在 |
| 比较与来源定位 | 比较revision、PageView、Slice、knowledge、recovery、effects、receipt、verification；点击差异转到实际来源帧并匹配scope | 模型单测 4/4；未完成浏览器视觉与桌面/窄屏验收 |

以上“覆盖”仅指这些真实导出的 Owner 投影在界面可见。Python 打包/JSON exact-W 校验、模型测试和 Kernel 导出测试分别证明材料结构、模型行为和受控导出链；它们不等于浏览器视觉验收、live-device/model 证据或 change 最终验收。

## 最终浏览器确认
实际桌面1440×1100及窄窗390×844已经运行并检查截图。关键联动及边界例见browser-checks-1/2/3.json、browser-boundaries.json；导入拒绝/成功见browser-import.json，图平移与无横向溢出见browser-layout.json，下载见browser-download.json。旧表中的未采集/N/A仍保留，不因浏览器通过提升未记录场景的证据等级。
