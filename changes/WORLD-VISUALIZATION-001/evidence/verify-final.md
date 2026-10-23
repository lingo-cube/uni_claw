# 验证四元组

level: SCENARIO（受控运行链 + 真实浏览器）；非 ENVIRONMENT 真机/模型结论。

| method | expected | actual | evidence |
|---|---|---|---|
| Kernel 6类相关测试，可选真实 Owner 导出 | 原断言通过，exact-W中间快照可得 | 37/37 PASS；十组素材中的Kernel来源已生成 | export-tests-final.txt |
| Host WorldOperationReplay | 实际导航/滚动/回执/配准，三种范围不重算旧Slice | 6/6 PASS；5时点×3实际Slice | scroll-export-tests.txt；scroll-world-visualization-source.json |
| Agent.Dsh录制XML通道 | 真实World/Driver/HTTP正文按W关联 | 1/1 PASS；录制输入+受控provider | recorded-export-tests.txt；recorded-world-visualization-source.json |
| JS读取/差分契约 | 拒绝跨W；保留subject键；同W恢复结果与局部投影变化可比较 | 4/4 PASS | model-tests.txt；model-red.txt；comparison-red.txt |
| 独立Standards/Spec→复审 | 无剩余阻塞，依赖/Authority不越界 | 两个P1已修，复审无P1；P3键盘已修 | review-standards.md；review-spec.md；review-followup.md |
| 真实浏览器选择/图/页面/知识/比较 | 真实资料同源联动、状态含义不失真 | 14+12+7+3项检查PASS；平移实际transform变化 | browser-checks-1.json；browser-checks-2.json；browser-checks-3.json；browser-boundaries.json；browser-layout.json |
| 导入/播放/下载 | 错配拒绝保留原视图；步进可暂停；只读下载实际资料 | 导入正/反例PASS；IAB播放/暂停PASS；下载5帧scroll资料PASS | browser-import.json；browser-checks-3.json；browser-download.json |
| 视觉/响应式 | 桌面/窄窗可读，关键状态不只依赖颜色，无页面横向溢出 | IAB实际截图检查PASS；390宽scrollWidth=375；不等于人类签字 | desktop-final.png；narrow-final.png；browser-layout.json |
| quick Verify | 架构/选定仿真/认证无回归，源产品不改变 | PASS；43+26架构、7场景、41认证0违反；src diff为空 | verify-quick.txt；protected-files.json |

所有失败尝试保留。`export-tests-with-history.txt` 的6个失败是导出器对无Assurance fixture的无条件访问；以RunView缺失时保留null修复后37/37通过。浏览器第一次scope失败修正chooseFrame；另外脚本对空下半区/历史可见项的假设不符合该实际素材，修正检查为实际范围内容与已采信配准来源，不改素材或World判断。Ego截图超时采用IAB，Ego播放暂停脚本受可见性/动作时序影响，IAB实际交互复现通过，未删除失败记录。

边界：quick并非full；本次没有产品代码变化，核心full验证仍以WORLD-UNCERTAINTY-001证据为准。覆盖账中“未采集/N/A”是已发布模型/输入材料的展示边界，不声称实现了恢复/补偿或所有负例的完整新演示。最终provider envelope/live device/model与人类视觉接受均无证明。

最终导出关闭分支也已验证：不设置UNICLAW_WORLD_VIS_EXPORT时helper直接跳过采集，37/37通过（export-disabled-tests.txt），十个样例摘要保持一致。产品src无差异。
