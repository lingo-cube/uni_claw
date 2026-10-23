# 可视化读取模型与交付计划

前置核心56a75fd74b5dff72db931edc96e343f0aea6d80b已通过Review/full Verify、提交且远端确认。实际功能落tools/world-visualizer，独立只读本地Web入口，Python标准静态服务；无npm依赖、无执行/回滚接口。

## 复用与独立项

- 复用产品SliceContentPresentation（原Simulation Replay纯表达提取）；示例记录实际Slice、SliceId和WorldPageView，前端只绘制既有框/文字/状态，不重新配准/融合World。
- 复用WorldOperationReplay的实际Driver/receipt/viewport输出；其旧HTML报告只提供布局/来源表达参考，原测试Oracle及程序集不被应用引用。
- 扩展测试导出只读观察快照和实际Owner视图，包装world-visualization.v1格式；不改变产品行为、原断言或独立Oracle。应用只消费静态JSON，不加载测试程序集。
- 独立实现可交互图、时间线/播放、版本比较、页面/Slice范围、来源/Agent/效果联动、资料导入及未知状态。图布局/文本差分仅呈现，不裁决身份、关系或效果。

## 最小读取形状

外层id/title/provenance(coreCommit/sourceTest/inputClass/limitations)/frames。每frame声明key/label/source revision以及实际revision、Owner knowledge/recovery/effectImpact/terminal/pages/slices。Slice wrapper携带原SliceId、Slice和产品WorldPresentation。实际时点一次采集，不能把后面PageView冒充前面revision；缺页/Slice/消费记录显示未采集。当前对历史更正解释单独带最新来源标识，不覆盖原时点认识。

纯包装adapter可把核心已发布示例封装为该格式；不重算世界、关系、变换或当前采信，不把旧scope、坐标转为执行target。前端校验明确SourceRevisionId与frame一致，输入错配拒绝呈现，不用自己修正数据。比较视图的新增/移出/值变/来源变表达相邻快照差异；不从“未再观察”推现实不存在。

## 示例与实现顺序

1. 导出context/reuse/recovery/terminal实际Owner时点，补录制、滚动、误归属纠正例子，均明确受控/录制边界。
2. 实现中文单页工作台，图/页面/版本联动，数据来源与局限可见。
3. 独立Review，实际浏览器交互与截图检查，桌面/窄屏、失败输入/未知、两版比较、历史解释、scope区别、Agent/效果信息。
4. 对MV18/RU8/CI12及早期要求给可见覆盖映射；按实际结果更新state，单独负责提交推送并验证远端。

不将任意device/detail路径自动建Container，不增加通用恢复、Goal、身份merge或世界数据库。没有真实来源的恢复/依赖/访问明确未知；UI不把最新图正确画成旧效果已恢复。
