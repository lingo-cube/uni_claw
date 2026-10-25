# 重放与显式开发诊断的资产出口

本片先复核清理，再接通重放和诊断的真实资产调用链。归档、恢复后删源、任务级删除和存量迁移仍未实现；父Change保持开放。产品存储继续由原聊天负责。

## 清理前置结论

`cleanup-precheck.json`记录：旧限定清理的40文件仍不存在，上轮3个自有临时文件仍不存在；曾恢复的4份来源文件SHA256一致。两次首片验证的78文件仍供验收和失败排查，继续保留。因此无需重复旧清理，也不把清空旧目录作为本片前置。此前未实施的归档、迁移和自动清理没有取消，需等保护和消费者验收。

## 调用链与修改

- `tools/test-assets`将begin/scope/finish/path-ref/replay接到`test_asset_operations.py`。路径仍只有原load_layout/immutable binding一个来源。一般源码/输入只列Run protectedRefs，不自动成为正式baselineRefs。
- 重放调用原`evolution-case.py replay --out-dir <本Run/raw/replay>`，不改封存脚本/REALIZATION/Oracle。保存实际输入SHA与声明SHA，来源/配置改变拒绝跨批retry。TRX计数和失败消息、原replay.json先消费后finish；CLI退出码与内部carrier退出码分列。
- `RuntimeToolHost.DevelopmentAssets.cs`只处理显式开发诊断：Python begin解析路径，原core产生packet及信号，finally路径收尾。结果使用`test-assets:CHANGE/RUN/相对路径`，path-ref经原绑定及摘要校验供旧消费者读取。实际写出前再次拒绝链接重定向。
- `RuntimeHttpServer`新增可选developmentAssets字段，原无开发Owner的调用兼容。模型仍按PNL-012既有AgentDecision路由；路由拒绝发生于真实诊断执行前，不伪造模型身份或Run。此处没有让产品profile读取开发配置，也没有迁移产品report/facts/恢复来源。
- 成功、transport失败、显式取消、退出信号失败均记录；finish失败单独回报，不替换原诊断结果。所有本片运行证据保留，没有存量删除/迁移，没有真实模型、设备或服务调用。

## 真实结果与证据

两次真实组件replay均为mismatch，CLI/carrier退出码各1。最终TRX记录1个Failed：独立标签expected=False，当前分类observed=present。输入仍带原gate，不能把该组件结果升级为完整Run、窗口真值或设备证明。资产链正确保存这个失败，没有修业务判断。

旧EVO-003 case当前因SettingsCoverageConfig源码漂移而不可重放，见existing-case-precheck.txt；原case/hash保持。`case-input/`是从既有原始输入与独立Oracle新导出的当前验证case，不覆盖历史。原脚本及两份旧case的SHA复验保持。

| method | expected | actual / evidence |
|---|---|---|
| Python公开操作/布局契约 | 唯一解析、实际输入指纹、保护原来源、重试拒绝、取消收尾、读取校验 | 最终22/22，python-final-v3.txt；命令为unittest的test_asset_operations.py和test_test_assets.py |
| 既有verify入口回归 | 原失败分类、TRX复用及取消保留仍成立 | 15/15；verify-entry-regression.txt |
| DSH诊断/registry/输出相关回归 | 原ABI/输出兼容，新开发模式不覆盖原结果 | 39/39；diagnosis-regression-v3.trx/txt |
| helper诊断联调 | 真实Python CLI、fake transport、嵌套类别、packet/信号读取、失败/取消/链接/收尾错误 | 8/8；diagnosis-managed-final.trx/txt；这些8项包含于相关回归，不累计成47个不同用例 |
| 真实组件replay | 有效来源及TRX/重放结果先消费，失败不丢 | real-replay.txt、real-replay-final.txt；各8文件SHA有效，最终sourceStable=true；最后的collector自指纹和取消窗口小改由定向测试覆盖，不冒称重跑了产品全量 |
| 独立双轴审查 | 可行动缺陷解除且范围清楚 | review-standards-v1/v2及修复复核、review-spec-v1/v2；Spec最初关于路由的P1已按PNL-012原D1复判撤回，原报告保留 |
| 清理与来源保护 | 不重新删除已清理项，不改原始/封存输入 | cleanup-precheck.json；source-reference-role-correction.json；全部本片Run回执kept，deletedBytes=0 |

最初diagnosis-api-red.txt是新增API尚不存在；diagnosis-integration-first.txt仅证明并行实现时helper尚未就绪，不能作为业务缺陷RED。后续真实helper联调通过。构建使用共享工作区，其他产品变更仍保留，不声称隔离纯候选或全solution通过。assembly-observation.json仅为读取时点，不冒充执行绑定。

Standards发现的P2为Run已登记到handler安装之间的取消窗口，已在begin前安装SIGTERM/SIGINT标记并在任何退出路径恢复；测试证明取消时不启动carrier且返回130/pending。handler-finalizer-red.txt另外真实复现了异常前置漏恢复handler，最终finally及finish错误分域修复后22例通过。v3 Standards与v2 Spec均有界通过，不用新文案推翻既有模型路由权威。

第一份真实replay曾误把一般源引用列到baselineRefs；后续实现已改为protectedRefs，并以source-reference-role-correction.json记录索引角色纠正，原source/input摘要、Run结果、所有文件内容及SHA都未重写。

## 复验与下一步

本片只读复验脚本检查当前候选、首片before来源、真实Run及旧封存SHA。slice1的原verify.py直接对当时工作源码校验，后续源码演进会产生预期差异；不修改其历史摘要来假装当前仍是旧候选。slice2/before与本片patch给出明确的叠加关系，没有整树复制。

下一步是可验证归档：实际解包核对，再让原TRX/packet/case消费者读取，证明保护完整后才允许处置明确自有的多余源。旧无开发Owner的诊断调用尚未迁移，不能据此声称全仓自动清理完成。
