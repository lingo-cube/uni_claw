# SIM-012 验证范围
- 原始来源：runtime-plan-006/depth2/epoch-002、003、009；SHA与窗口内容锚见recent-recordings.json。
- 公开Host离线导入：3个Theory参数，冻结typed observation/candidates，原文件不改。
- 公开KernelRunDriver：3个Theory参数，缺身份、错capture、匹配恢复、无重发、owner与revision检查。
- 故障敏感度：固定expected不变，受控post signature复用旧页→3/3 owner oracle失败；非历史binary RED。
- full：8工程2070/2070，35/35场景（20 certified、15 descriptive），SCN-REPLAY-007为descriptive carrier，3个完整展示名join。
- 不证明：最近一周全量录制、真实Host身份生成/跨包popup修复、原模型决策或完整原Run、真实逐帧freshness、关闭状态Exact证明或Goal完成。
