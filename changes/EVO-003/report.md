# 运行产物现在可以封存并实际重放（EVO-003）

## 做了什么

新增`tools/evolution-case.py`的`export → verify → replay`调用链。首个适配消费已有Settings运行记录，将指定动作之后的XML交给既有`SettingsPopupClassifier.Classify`，用独立整理的标签比较结果，输出实际vstest TRX。

## 原来的问题

EVO-002能从runDir生成失败信号，但信号尚不能形成独立保存、可重复执行的组件案例。直接引用原run会受文件移动影响；从trace的popup结论推导expected，则会把被测输出当作答案。

## 这次修改

- `tools/evolution-case.py`：核对capture、trace及DeliveryCompleted回执的唯一关联；复制facts、trace、steps、相邻XML、触发XML和显式外部Oracle，记录来源与sha256，最后封存manifest。拒绝覆盖、输入损坏和错关联。
- `tools/EvolutionReplay/ExportedCaseTests.cs`及独立工具测试项目：校验封存输入，经公开分类器运行一个真实xUnit测试。replay仅运行固定载体，区分标签一致、标签不符和载体不可用，并保留TRX。
- `schemas/evolution-exported-case.schema.json`及四处导航/Skill文档：约束这类导出bundle，保留同run信号/诊断的引用，固定工具与配置实现hash。产品分类器记录baseline但不锁住后续候选修改。

本轮产品src和旧case/Oracle均无改动；未执行记录中的动作。完整Runtime重放、模型/设备调用和分类修复均不在本片范围内。

## 验证结果

真实Default apps记录导出后，同一manifest与inputs连续两次产生相同的标签不符：Oracle期望无弹窗，当前分类器仍返回弹窗。两次均有真实TRX Failed，因此证明原问题可重复，未宣称它已修复。真实SIM弹窗正对照得到TRX Passed。

工具测试44/44、既有Host RecordedPopup测试8/8通过；Standards与Spec独立评审均0问题。27份入场保护文件及11份评审输入hash一致，原始输入与复制件hash一致，tool registry和`git diff --check`通过。NuGet漏洞缓存访问出现NU1900警告，构建及测试正常完成。未提交或推送。

完整证据及可复现命令见[evidence/verification.json](evidence/verification.json)，负例及正对照分别见[evidence/cases/default-apps/case.json](evidence/cases/default-apps/case.json)与[evidence/cases/sim-dialog/case.json](evidence/cases/sim-dialog/case.json)。重跑时使用新的输出目录，保留已有结果。

## 新发现

相邻的录制观察无法证明它就是独立的动作前捕获；既有资料也没有补齐清障动作、逐帧时间与原始窗口栈事实。外部Oracle的来源描述和哈希能被校验，其独立认识来源仍需要评审。因此该结果是组件分类反例，不能直接批准分类策略变更。可选信号与诊断只保存外部引用，引用文件仍需保留。

## 下一步

补齐SIM-017要求的窗口证据与分类语义，确定跨包页面和真实弹窗的判定依据，再对`SettingsPopupClassifier.cs`提出最小候选修复。保留本轮封存案例作为同输入的RED基准；当前语义gate仍为required。
