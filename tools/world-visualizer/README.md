# World 认识过程观察台

在仓库根目录运行 `python3 tools/world-visualizer/serve.py`，打开 <http://127.0.0.1:8765>。仅需 Python 3.10+ 与现代浏览器；预置真实核心导出随代码提供，打开界面无需运行 .NET 测试。端口冲突时用 `--port 8766`。服务仅绑定本机，Ctrl+C 结束。

选择资料与时间点，图和页面自动跟随实际当前页。可选择上下文 Container、实际 Slice 范围、对象与关系；支持搜索、关系过滤、缩放、拖动平移、聚焦及复位。版本变化比较两个实际时点的图、页面、Slice、知识、恢复结果和效果核查；点击变化定位其来源版本、对象及可得 Slice。前后、滑条、修订菜单与播放均为展示步进，不重新执行任务。

“当时认识”读取所选 W；“更正后解释”保留原图，单独显示最新 Owner 对该旧 capture 的解释并标出双方版本；“累计内容”只绘制 World 已采信的配准结果，历史项用虚线标记。动态未知、访问未证、整页覆盖未证始终明确呈现。没有完整 Ledger/消费记录时不补画因果，没有模型正文时不声称模型已看到。

“查看来源”列出来源测试、核心版本与限制；实际 HTTP 正文仅证明受控通道内容，最终外部 provider envelope、live model/device 均未证。导入支持 `world-visualization.v1` 本地 JSON（上限 25 MB），拒绝混用 World/PageView/当前 Slice 的版本；本地摘要只校验预置文件一致性，不认证第三方数据。下载是只读导出的当前整段资料。应用没有执行、恢复或回滚接口。

## 代码路径与权威

`index.html/style.css` 是中文工作台；`app.mjs` 管交互与 DOM；`model.mjs` 做字段归一、来源校验与快照差分；`serve.py` 提供静态本地入口；`package_examples.py` 包装正式输出并生成核心与边界示例的摘要索引。`examples/` 来自已发布核心 `56a75fd7` 的实际 Owner 输出，数据细节见各自 provenance。产品源码不依赖此工具或测试程序集。

页面/Slice 布局复用 `SliceContentPresentation.Project` 的只读产品输出；滚动配准、身份、Contains、Navigation、知识沿用、纠正与效果判断均取自原 Owner。JS 不再实现一套 World。旧 Replay 的受控 Driver 场景用于采集导航、回执、验证及滚动来源，原测试 Oracle 不进入应用。

## 复现导出

下面的命令在仓库根目录串行执行，不需要真机或外部模型。正常使用无需重导出。每次更新后应重新打包摘要，避免浏览器拒绝混合新旧资料。

```sh
UNICLAW_WORLD_VIS_EXPORT="$PWD/tools/world-visualizer/examples" dotnet test tests/UniClaw.Kernel.Tests/UniClaw.Kernel.Tests.csproj --filter 'FullyQualifiedName~WorldSemanticReuseTests|FullyQualifiedName~WorldKnowledgeGapTests|FullyQualifiedName~WorldRecoveryReviewTests|FullyQualifiedName~WorldCorrectionImpactTests|FullyQualifiedName~WorldPageCorrectionTests|FullyQualifiedName~WorldPageHistoryTests'
cp tools/world-visualizer/examples/correction.json changes/WORLD-VISUALIZATION-001/evidence/correction-world-visualization-source.json
UNICLAW_WORLD_VIS_SCROLL_EXPORT="$PWD/changes/WORLD-VISUALIZATION-001/evidence/scroll-world-visualization-source.json" dotnet test tests/UniClaw.Host.Tests/UniClaw.Host.Tests.csproj --filter FullyQualifiedName~WorldOperationReplayTests
UNICLAW_WORLD_VIS_RECORDED_EXPORT="$PWD/changes/WORLD-VISUALIZATION-001/evidence/recorded-world-visualization-source.json" dotnet test tests/UniClaw.Agent.Dsh.Tests/UniClaw.Agent.Dsh.Tests.csproj --filter FullyQualifiedName~RecordedSettingsLayout_FlowsThroughWorldDriverAndDshConsults_AndExportsFormalViewsWhenRequested
python3 tools/world-visualizer/package_examples.py
node --test tools/world-visualizer/model.test.mjs
```

验收与边界主题映射见 `changes/WORLD-VISUALIZATION-001/evidence/coverage-ledger.md`，实际浏览器结果及截图见同目录。不把受控来源升级为实况证据，也不承诺未实现的通用补偿、不可逆动作恢复或 Goal 评价。
