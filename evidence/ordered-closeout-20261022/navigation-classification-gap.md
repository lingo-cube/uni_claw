# 当前真实遍历的导航分类缺口（2026-10-22）

## 观察到的事实

固定运行：bounded-live-01，原preregistration保留，3次真实咨询、3份receipt、2次post-action verified。第三个目标是Internet页的“Fix connectivity”。输入XML中它是`android.widget.Button`、clickable/enabled=true，位于顶栏；Director将其纳入nested-enter，Host guard记录ALLOW。receipt只证明一次tap投递完成；页面路由仍为Internet，后续page-identity-observation-exhausted，未验证为成功进入。没有证据证明该tap的所有现实副作用，也不能宣称设备发生网络重置。

## 可定位调用链

- `src/UniClaw.Host/PageIdentityRecovery.cs:Capture`：XML clickable节点被统一投影为ui.element，原class/checkable事实没有进入此capture items的类型表达。
- `src/UniClaw.Host/SettingsCoverage/SettingsCoverageLedger.cs:DynamicNestedCandidates`：泛化ui.element可进入动态候选，框架按钮仅有部分native rid排除；该按钮无rid。
- `src/UniClaw.Host/SettingsCoverage/SettingsActionPolicy.cs:SettingsActionGuard.Evaluate`：排除若干禁词/开关后，普通tap/click直接归navigate，并非根据目标的导航依据分类。
- 当前约束明确`unknownTargetDisposition=reject`、只允许已授权安全动作。可点击本身不足以证明导航；这条事实链必须在下一次完整动态遍历之前修复或用已有明确任务导航范围约束。

## 与已完成修复的区别

SETTINGS-COVERAGE-007只修同capture补证后的路由/覆盖记账；它不修改该分类链、不扩大许可、不把本次停止改为成功。原真实失败保留；007新旧输入场景通过确定性回归，不声称修复后已经再次实机通过。

## 下一步可判定工作

1. 在既有观察→World只读投影→任务guard链上保留必要的控件事实，或消费已有明确的SettingsNavigationScope；不能靠模型解释把未知控件改成安全导航。
2. 确认菜单、按钮、开关、返回、弹窗处置及未知项各自的分类依据与合法例外；未知仍拒绝。先用本次原始capture构成公开调用链反例，再最小实施。
3. 保持当前Owner：Perception供观察，World接纳/归属，Host任务guard仅限制，Assurance验证，Effect唯一投递。若确需变更已冻结载荷，先完成协议与buyer裁决，不临时发明第二真相源。
4. 复用原有已知导航、返回和目标Wi-Fi开关正例，并加入本次按钮及模型伪报role的负例；有界验证完成前不继续完整动态树点击。

本记录是定位与后续输入，不是已接受的新能力协议、动作白名单或完整安全证明。
