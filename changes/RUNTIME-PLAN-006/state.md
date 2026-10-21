# RUNTIME-PLAN-006 — Settings一级与最深二级安全遍历尝试
lifecycle_state: closed · disposition: none · depth: standard · base: bce0ec3a

## Intent
Owner要求尝试完成设置应用一级页面、最深二级页面遍历，注意安全。首页深度0、一级1、二级2；沿用真实CommandCode Flash→正式codec→Kernel Plan/Traversal，不用脚本决策替代Agent。

## Scope / Out of Scope
范围：当前注册API35模拟器、当前账户/权限/系统状态内可观察的Settings导航；分别开展根页滚动盘点、一级遍历、最深二级遍历；必要的纯Host任务导航允许表与执行前收紧、真实验收工具及证据。
范围外：新Capability/协议/Kernel算法、第三层、任何状态修改（含WiFi）、登录/安装/账户/支付/凭据/授权/确认、外部应用继续操作、全UI或全部系统状态完备性声明、提交推送。

## Decisions
- 安全优先：只有当前Settings route中任务明确允许且当前唯一可见的菜单目标可点击；未知/未授权元素只记录排除原因。只读开关无Effect。执行前强制同一允许表，不能由Agent绕过Traverse改Act。
- 允许observe、可见列表swipe-up与当前可见Navigate up；未知package/route、popup确认、禁手词、重复/歧义目标拒绝。原策略只可收紧，不能放宽。
- 导航允许表属于任务授权，不是World事实/Kernel进度；Source只投影当前World引用，Traversal仍拥有进度及独立验证后推进/返回。
- 根页盘点只滚动不进入；据真实页面记录审核一级允许入口。二级允许表根据一级实际观察建立，逐route限定。未确认控件全部只记录，保留缺口。
- maxDepth=1/2由Kernel强制；滚动用现有Act能力，仍经Kernel同一执行/验证链。不伪造discoveryComplete。
- 公共验证缝沿用005已确认的ProjectTraversal、SettingsCoverageRunner/GuardStep/正式codec；纯Host任务policy不进Capability Registry。

## Acceptance
A1：公开缝反例证明同名不同route不能继承权限、外部route/所有switch/非白名单/desiredState/确认按钮零Effect，允许菜单与列表滚动可通过既有独立guard。
A2：真实Agent按预注册任务与预算输出Plan/Act，逐次正式codec、当前World绑定、guard、Effect回执与post-action验证；保存1级/2级所进入页面、最大实际深度、跳过/失败/未知及回根页证据。
A3：根页滚动有真实回执与视口证据；在预算内尝试所有已观察安全一级入口及最深二级允许入口。未穷尽/安全排除/环境阻塞如实报告，不能用部分覆盖冒充全部完成。
A4：相关仿真/协议/Host回归和双轴review通过；共享脏现场保留，只清理自身设备租约，无未经授权改动。

## Constraints
Leader6.1Sol/LunaWorker沿用Owner授权，根进程独占build/模型/设备调用。CommandCode Flash绑定/代理/凭据保持。入口恢复快照在evidence/runtime-plan-006/entry。

## Verification
verification:
  level: ENVIRONMENT
  method: "公开缝安全反例、预注册真实Agent与注册模拟器、轨迹/页面/回执独立审计"
  expected: "在安全边界内尝试一级及最深二级，逐页可追溯，零状态修改"
  actual: "有界尝试结束，完整覆盖未通过：16根入口中13往返；32条登记二级边中30进入、26往返；最大深度2、违规Effect=0；40安全/33仿真/16协议/27返回回归全通过，双轴最终增量0项，自有5556租约已回收。"
  evidence: "evidence/runtime-plan-006/"

## Status log
- 2026-10-20 · UNDERSTAND→PERSIST→PLAN · Owner授权扩大安全遍历；现有Source缺二级正向分类、普通tap guard过宽，先在既有Host缝收紧任务导航授权后真实执行。入口脏现场已快照。

- 2026-10-20 · IMPLEMENT · 逐route只导航Scope接线及真实harness完成；明确Actor只有5556，5558未动。公开反例先复现新边界被Allow后修正；39/39原相关Host、33/33仿真、16/16协议通过。review1发现rootBack与Source popup前提不一致、注册model可缺省及完成oracle缺口；最小修复中，当前模型/任务Effect仍0。用户已知模拟器未实际滚动，不将预检冒充遍历。

- 2026-10-20 · IMPLEMENT→REVIEW · review1五项发现已按真实公共反例修正：rootBack拒绝、Source/guard共用route+popup前提、mandatory model和全部预算、独立覆盖/深度/回根页oracle。当前Host40/40、仿真33/33、协议16/16，harness build成功。review2和census epoch001输入已冻结，census仅允许3次root scroll，模型咨询仍0。
- 2026-10-20 · REVIEW→VERIFY · review2 Standards0，Spec两处census条件已修为BackNavigation=false及精确3次；空rootTargets无菜单授权。未变的40安全/33仿真/16协议证据复用，当前harness编译通过；epoch001未调用模型，保留后以002重新冻结。现已开始真实模型/Kernel根页盘点，设备为5556。
- 2026-10-20 · VERIFY · census002两次滚动Verified，第三次相同视口被post-action-content-transition拒绝；保留失败批与16个真实根入口。一级001已完成9项进入/返回，Security & privacy跨com.android.permissioncontroller被现有host策略视为overlay而暂停。一级002因启动命令未真正清空跨包task仍在同页，零Effect停止；改用NEW_TASK+CLEAR_TASK并验明Root后一级003完成4项进入/返回，About已打开但XML缺失导致未知身份停止。一级合计13项完成返回，未伪报全部；Wallpaper安全排除。
- 2026-10-20 · VERIFY→IMPLEMENT · 二级001实际到达Internet，但返回父页失败。E4轨迹与既有seam反例确证Host将opaque receipt target id当Navigate up文本判断，父路径未pop，Kernel正确拒绝；最小修正仅Host显式解析已接受occurrence语义，原失败保留。RED1/3→相关GREEN27/27。二级Contract旧要求0页修为至少1页，原全部注册子目标/深度2/回Root验收不变。诊断见return-fix/diagnosis.md。

- 2026-10-21 · REVIEW→VERIFY · 返回识别最小修复双轴复核均0项，build成功。二级002真实复验Internet进入及回父页成功，随后SIMs采用另一UI标题结构落为unknown route，安全暂停；模型随后错误ContinuePlan被revision gate拒绝。该批4个Verified Effect，保留失败，不把它计为完成。二级003另登记残余4个根入口/13条二级边，未把SIMs缺口移除出总体结论。

- 2026-10-21 · VERIFY · 二级004完成4根入口/7条二级边并回Root，Display另一条在原始XML中为视口外倒置bounds，独立audit拒绝8/8。二级005正式通过4根入口+6二级边/22步/2次咨询；008通过网络残余3边/8步/2次咨询。007在Battery Saver因projection关联暂停后安全返回，Manager保留；006 App notifications标题结构不支持，009 Default apps跨permissioncontroller，均保留失败。当前在独立登记的010残余通知范围继续，无代码/模型/安全门放宽。

- 2026-10-21 · VERIFY→CLOSED · 本change的有界安全尝试与证据交付完成，结果明确为PARTIAL_NOT_FULL_COVERAGE，不代表Settings全覆盖验收通过。010补跑通知3条可见边返回，余1条视口外；011电池管理器、012特殊应用权限通过。最终13根入口及26/32登记二级边验证往返，30/32已进入，6条缺口逐条持久化；无开关/desiredState/越权Effect，最大深度2。下一步范围见evidence/runtime-plan-006/results.md，不借关闭尝试隐藏产品缺口。源指纹无漂移，租约回收成功，未提交推送。
