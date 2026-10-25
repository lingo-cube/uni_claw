# Private publish integration Standards review v1

范围：只读审阅 `draft-run.json` 指向的当前私有 index、`closed-overlay-base.json`、恢复/测试先决清单与输入闭包；未运行构建/测试、未改 index/工作树，也未改旧 hunk review。

## 可操作问题（仓库规则）

0 项。SIM-019 三个封存 provider 的 index SHA-256 与清单一致：`a1f0ae91…bcd12dd`、`a5d1b6bd…2a032`、`f0483dd9…9434d`。选中的 `WorldModel.cs`=`2b519e76…f1ab08` 和 `SettingsCoverageRunner.cs`=`1bcd1609…a083c3` 恢复原生 Overlay 路由；`KernelRunDriver`、`UniKernel`、Runner 均不含 Act/authored ExpectedEffect/OverlayDismissed 新增标记。

四个 closed recovery reader 均在 index，且只读结果保持 `ContinuationSupported=false`；未带入 activation、Drive 或 continuation。`RuntimeDiagnosisPacket` 与 EVO-002 两 schema/evolution signal 同在（schema SHA：`7c96254a…fa403a`、`871ed7d5…879e7`），仍声明 non-authoritative。原 Probe executable 组、LegacyEvidenceV1、录制 XML/序列输入已选中；`FileWorldJournalStreamingTests` 保留全局 GC collection 与原 24MiB 阈值；`TraversalExecutionTests` 仅含 Storage partial、Run/Control/Policy/Journal 注入，候选/条件 sibling 未入。

## 判断性异味

0 项。此次从“全部排除 Overlay”改为按已关闭 owner 的精确封存 provider 恢复，未将其误升为 Storage Owner，也未以删除既有断言缩小闭包。

结论：integration Standards actionable findings = 0；judgement smells = 0。后续 Coverage 结论不在本审查范围。
