# 旧弹窗回放数据清理

按用户2026-10-25裁决“只保留正式测试基线，历史过程证据也可删除”，本轮清理限旧弹窗回放，不扩展到其他活动Change。

## 实际删除

| 范围 | 文件 | 字节 |
|---|---:|---:|
| `evidence/sim-016/`旧过程输出 | 215 | 41,661,814 |
| `changes/REGRESSION-DIAG-001/evidence/`诊断过程数据 | 81 | 5,300,630 |
| SIM-024候选组合及current-replay/host-current-qualified结果 | 6 | 716,174 |
| 三个CurrentCandidatePopup测试载体 | 3 | 13,256 |
| 合计 | 305 | 47,691,874 |

另删除本会话创建的旧诊断临时副本（318,024,946字节），以及本次验证/对照副本（109,301,105字节）。不备份被删内容，仅留path/size/SHA元数据。SIM-016目录仅留简短RETENTION说明，诊断报告及相关Change状态追加退役说明；历史执行结论不再冒称当前证据文件仍存在。

## 保留与边界

保留`tests/UniClaw.Host.Tests/Fixtures/PopupReplay/{composition,sources,oracle}.json`、正式RecordedPopup两组测试及提取器、原始录制/截图/trace及其现存依赖，场景与产品实现不因清理改变。150份受保护正式fixture、场景、载体及引用文件逐字节不变。没有刷新pins或Oracle，没有把删除用例记作修复。

入口时共保护422个文件，421个最终未变；共享树中`src/UniClaw.Host.Dsh/RuntimeToolHost.Reports.cs`出现并行修改，单独记在`evidence/concurrent-drift.json`并原样保留。本次没有产品源码写入，不回滚他人的工作。

## 验证结果与新发现

- 当前清理后源码在私有副本中编译通过；定向24例为16通过、8失败。
- 正式六个PopupReplay用例仍全部发现，仍在原`SettingsActionPolicy.cs`源码SHA gate拒绝；行为断言尚未运行。六个候选重复用例已退役。
- 额外窗口buyer有两例在`SettingsWindowClearanceBuyerTests.LocalAct.cs:50`删除不存在的临时目录时抛`DirectoryNotFoundException`。没有修改这两个测试或产品实现，也不推断其行为断言已通过。
- 私有对照只恢复逐字节相同的三个候选载体及其执行manifest，使用相同产品源码/正式输入再构建：30例为16通过、14失败。移除六个候选重复用例后，剩余失败名称和消息一致；仅归一化由测试生成的临时目录GUID。旧过程导出不属于运行时输入，未恢复；不把此对照宣称为完整历史版本复演。
- `evidence/verify.py`核验305条删除结果、保护SHA、零交集、正式六例保留、候选六例退出和上述对照。`git diff --check`通过。无设备/模型运行、提交或推送。

清理完成；基线认证关系及窗口测试的临时目录问题仍需各自处理，本轮没有修复。权威删除清单为`evidence/deletion-manifest.json`，验证结果为`evidence/verification.json`。
