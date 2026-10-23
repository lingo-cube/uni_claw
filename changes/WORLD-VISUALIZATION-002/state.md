# WORLD-VISUALIZATION-002 — 修正页面比例与录制 Slice 空展示
lifecycle_state: closed · disposition: none · depth: standard · base: 5a4f485e6e5533ab880985daf10a737818af89ff

## Intent
2026-10-23 用户要求先修正已确认的页面比例和录制 Slice 空展示问题；完成后沿用既有授权提交推送 origin/uni-harness。

## Scope / Out of Scope
仅修改 tools/world-visualizer 的只读几何展示及回归检查、录制样例导出调用及其断言，并重新生成相关样例和摘要。保持产品 World/身份/关系/配准/执行权威和旧 Replay 不变；不接入原图、不做工具替换决策、不覆盖其他会话记录或同步 jojofran。

## Decisions
- 已通过实际同 Slice 对比定位根因，沿用该诊断证据：1080×2400 捕获在 Owner 页面被画成300×304；Slice本身15个有效定位，但导出传了CoordinateSpaceId而非定位的SpatialFrameId，Presentation.Elements为空。
- 显示比例按对应观察的正式设备尺寸转换，保持原归一化坐标和来源版本。缺少尺寸保持未知说明，不猜造设备比例；累计内容允许越过当前视口和负坐标。
- Slice表达使用实际定位坐标框，保留产品投影的严格过滤；不能在前端用原Slice偷偷补画以掩盖空投影。

## Acceptance
1. 同一1080×2400录制样例在Owner和实际Slice模式均绘制全部15个对象，矩形像素比例与原定位一致，切换对象仍定位正确来源。
2. 投影回归在修复前可失败、修复后通过；导出中的Presentation对象与实际Slice有效定位一致。
3. 桌面与窄窗实测有正确纵向滚动，无页面横向溢出；原有正方形受控样例、不同尺寸/历史来源、累计内容仍按对应空间显示，缺失尺寸不伪装成已知屏幕比例。
4. 实际Owner导出重打包后索引摘要正确，已有JS读取/版本检查及相关Dsh场景通过；浏览器复现入口和结果留存。
5. 只提交本次负责文件，既有6个dirty文件hash保持；origin包含最终提交。

## Verification
level: SCENARIO
method: 最小失败回归 → 修复 → 同一输入重跑；Dsh录制导出、示例包装、Node几何/数据回归、实际浏览器桌面/窄窗检查。
expected: 比例正确且Slice15个对象可见，原始定位和来源不变。
actual: 两项真实回归先失败后通过；Node7/7、Dsh导出1/1；Owner与Slice均显示15项，比例正确；390宽无横向溢出，历史/横屏/累计/尺寸缺失分支通过；十个资料摘要一致，6份原有dirty记录hash保持。独立复审无阻塞。
evidence: evidence/verify.md、review.md、browser-desktop.json、browser-narrow.json、browser-source-checks.json及红/绿回归日志。

## Status log
- 2026-10-23 UNDERSTAND→RESOLVE：复用已完成的源码/同样本诊断，根因已确定，不重复假设扫描。
- 2026-10-23 PERSIST→PLAN：保留本地6份dirty记录，经无交集核验安全快进到origin最新5a4f485e；实施范围只含两个缺陷。
- 2026-10-23 IMPLEMENT：将几何转换集中为只读pageGeometry；修正显示比例/对应capture尺寸选择；录制测试用真实SpatialFrameId导出并断言15项及坐标完整；重新生成正式源和打包摘要。
- 2026-10-23 REVIEW→VERIFY：独立Standards/Spec复审未发现阻塞；产品src及旧Replay无变更。
- 2026-10-23 VERIFY→CLOSED：相关测试、来源/比例/历史/390宽浏览器验证完成。关闭提交后串行推送并核实origin包含最终SHA；该SHA以Git交付记录为准，不同步另一个jojofran远端。
