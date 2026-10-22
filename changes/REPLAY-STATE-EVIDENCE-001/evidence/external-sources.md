# 官方资料核验（2026-10-22）

本次仅查询 Android Developers 与 AOSP；没有设备调用。

| 来源 | 核验事实 | 不能推出的结论 |
|---|---|---|
| [AccessibilityNodeInfo.getChecked](https://developer.android.com/reference/android/view/accessibility/AccessibilityNodeInfo#getChecked())，官方页 getChecked/isChecked 小节 | getChecked API36 起提供三态；isChecked 为布尔且API36弃用；checked语义须checkable为真 | API级别不能证明某实际acquirer调用getChecked，不能证明旧XML有三态 |
| [AOSP Dumper](https://android.googlesource.com/platform/frameworks/testing/+/refs/heads/main/uiautomator/library/core-src/com/android/uiautomator/core/AccessibilityNodeInfoDumper.java)，查询时blob 59616c8ab655c39da0dad4c8b2120fce888b7cf2，dumpNodeRec | 序列化checked调用isChecked并转布尔字符串 | 不确认原设备运行此blob；不提供Stay awake二态域证明 |

这些资料支持候选采集路线和语义限制，不替代录制 provenance、实际运行版本与校准证据。
