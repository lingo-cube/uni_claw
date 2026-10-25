# Slice 6 Standards review v2（增量）

## 做了什么

仅审 `candidate-v1-v2.patch` 的四个变更路径及其对 v1 结论的影响。`candidate-inputs-v2.json` SHA-256 为 `bc50aa04d8c56d7951c6135f87e9583b436a5fe6572112784061fe2de1eb8a78`；清单仍为 23 路径，候选源 SHA 23/23 匹配。`concurrent-inputs-v1.json` 的三个未归属输入已单列，不计为候选改动。

## 原来的问题

同步 Host bridge 在暂停调用方消息循环时会因 await 捕获上下文而阻塞：`sync-context-red.trx` 记录该公开 `SettingsCoverageRunner.Run` 用例失败。

## 这次修改

Driver 与两个正式 Host 的嵌套 await 明确使用 `ConfigureAwait(false)`；没有 `Task.Run` 包装存储，也没有修改同步 bridge 的公开可用性。新增测试在专用 `PausedContext` 中直接调用公开同步 Runner。

## 验证结果

**硬问题：0。** 此增量只改变 continuation 位置，不越过 ADR-0055 第7、9、11行的确认/结算/关闭边界，也不改变 AGENTS.md §1.5 的显式 seam、Owner 或可替换性。`candidate-v1-v2.patch:412-423` 保留 Driver 原 await 顺序；`1172-1244`、`1245-1361` 覆盖两正式 Host 的 factory、World Owner、产物和失败写入；`sync-context-green.trx` 显示目标公开 bridge 及同批 9/9 均通过。v1 的「硬违规 0」结论不变。

**Fowler smells（判断性建议）：0。** 批量 `ConfigureAwait(false)` 是同一异步依赖规则的机械收拢，不是 Duplicated Code（同形逻辑抽取）或 Shotgun Surgery（一个语义改动散落多个职责）；三处都属于同一调用链上的 continuation policy。`PausedContext` 仅为真实 buyer 回归夹具，不是生产抽象或无 buyer 泛化。

## 新发现

无源码 Standards 阻塞；RED/GREEN 是已有证据，未在本评审执行测试。

## 下一步

按既定最终回归/封存继续；不扩展至 P24、历史读取、队列/DB/续跑或路径优化。

**计数：硬问题 0；smells 0；总计 0。**
