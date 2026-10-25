# 窗口证据与有界补采修复

本次修复 Settings 的弹窗判断及观察不足后的执行行为：跨包普通页面不再被当成弹窗；新 capture 没有合格窗口证据时，暂停清障与遍历，补采耗尽后停止。（SIM-019）

原来 `SettingsPopupClassifier.Classify` 把 XML 包名不同当作覆盖关系，Default apps 因而误报 present；只有 XML 时也无法可靠区分同包对话框。另一个问题是缺少新声明时，CoverageLedger 可能继续使用上一次 present。

现在的调用链为 `SettingsTraversalLiveFeed.Next → SettingsWindowAcquisition / ClassifyWithWindows → EvidenceLedger → WorldModel.Overlay → KernelRunDriver / SettingsCoverageDirector`。Host 在截图/XML 前后采窗口和显示原文，只有 API35 单显示、完整且稳定的窗口关系才产生 present/absent 候选。World 校验 capture/cycle/session/time，派生当前 popup；新 capture 使旧 present 失效，同 capture 矛盾保持 conflict，迟到或错关联只留审计。Ledger 只投影 World。Driver 复用既有 Control 计数与 deadline，观察期间不增加清障、遍历或重复投递动作。

补采配置独立于慢模型：缺省每 Run 累计 4 次，同一阻塞位置总计 15 秒，identity 与 overlay 共用额度；范围仍为最多 16 次、30 秒。15 秒依据此前实采每批 2.150–2.861 秒设置为可覆盖配置，并非成功保证。旧 XML-only API 和历史封存案例保留原义，既有 Evidence/World/Control/Effect 权威不迁移。

验证结果：

- 同一封存 manifest、输入、独立预期和测试载体：旧 XML 启发式受控消融 2 失败/4 通过；候选连续两次 6/6 通过。未改 expected，未重签旧 EVO case。
- 实际 `Drive` 和 World 的定向检查 14/14：旧 present 失效、错/迟到材料不恢复控制、冲突保持、身份完成不清除 overlay Need、共享预算、恢复不重发、超时和耗尽有界返回。既有取消路径回归通过。
- 八个完整测试工程共 2218/2218，通过工具测试 44/44；41 个场景认证检查通过。20 个场景只更新认证元数据，execution/expectations 与入场相同。两份 composition 只同步实现/契约来源，raw/Oracle 不变。
- 最终评审代码构建后，在注册 API35 克隆设备上重新采集：Default apps absent、SIM 对话框 present、BACK 取消后 absent，各连续两批正确。实际产品采集器/分类器/Evidence/World 消费原始材料；缺来源 unknown、错 capture 不替换状态。六批运行记录含实际 Host/Kernel 程序集 hash。截图已检查，设备已回收并确认离线。
- Standards：0 硬规范问题、0 异味；Spec：0 缺陷。评审固定的 51 个文件在最终设备验证前全部一致；关闭后仅状态验收记录变化。27 个保护文件 hash 保持一致。

本轮未发现新的阻断项。环境证明限于实际来源、分类与 World，Driver 的执行行为由确定性真实实现验证；未宣称完整 Agent/模型 live 或其他 Android/车机适配已通过。窗口前后投影一致也不等于原子采集。

本轮范围已关闭，报告和可复验材料见 `evidence/verification.json`、`evidence/packet.json`、`evidence/verified-live/summary.json`。无需新的 Owner 裁决；本地修改保留，提交、推送和基线晋升未执行。
