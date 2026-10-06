# AGT-015 — 真实 Settings 下一批：未知状态安全停止与错误目标拒绝

lifecycle_state: closed · disposition: none · depth: standard · base: 04fa7cbc

## Intent（WHAT/WHY）

所有者指令（2026-10-06，AGT-014 收口后）：开始下一组真实 Settings 测试，优先覆盖「未知状态安全停止」与「错误目标拒绝」。

确定性对面已在 AGT-014 故障矩阵全绿（未知页 Defer 耗尽→`bounded-stop:unknown-page`；forbidden/unknown/缺失策略→Guard dispatch 前拒绝；非法目标状态 fail-closed），仿真侧亦有承载（SCN-POLICY-006 guard-unknown 零 dispatch、SCN-WIFI-005 safe-stop）。本 Change 把这两个行为推进到**真实模型 + 真实设备**证据级：真机呈现不可判定的目标状态与错误目标候选时，系统必须安全停止或 dispatch 前拒绝，且全过程可追溯。仿真/确定性 PASS 不替代本轮证据。

## Scope / Out of Scope

### 范围

- 行为一（未知状态安全停止）：真机场景中目标控件状态不可判定（typed Unknown/冲突/部分证据）时——零 effect、有界停止、终局与理由可追溯、不伪造 SafeStop/Completion。
- 行为二（错误目标拒绝）：模型指向非目标控件（撞名/相似控件/越权 action class）时——dispatch 前拒绝（Guard/policy/effect-class/target-identity），零 ADB effect，拒绝理由入 trace。
- 测试集扩展：testsets/android-settings 增补对应 task/fixture/acceptance 声明（经 validate-testset-manifests 校验；具体 ref 形态在 PLAN 定）。
- 复用 AGT-014 基础设施：environment-preflight、emulator supervisor、consultations.json、7890 代理链路、trace 分段计时。
- 局部实现缺陷走复现→修复→回归（deterministic 回归必须先红后绿）。

### 不在本 Change 内

- 不修改产品架构、共享协议、模型路由或更换 Agent 模型。
- 不把 live 失败改成 PASS；设备/DSH/网络不可用时保留 ENVIRONMENT_UNAVAILABLE/BLOCKED 证据，不静默降级。
- 不做无界探索；有界预算沿用测试集与 profile。
- 不顺手处理 SIM-007/008 遗留（均已 CLOSED）或场景库演化。

## Acceptance（后续实现完成的判据）

| ID | 用户可观察行为 | 预期与证据 |
|---|---|---|
| A1 | 未知状态→安全停止 | 真实回合零目标 effect；终局为有界停止且理由可追溯（decision/guard/observation 链）；method/expected/actual/evidence 四元组落盘 |
| A2 | 错误目标→dispatch 前拒绝 | 拒绝发生在 Kernel dispatch 前（无 ADB receipt）；拒绝理由入 consultations/trace；同链复用回归证明正确目标仍可执行 |
| A3 | 测试集与追溯同步 | 新任务入 manifest 且校验通过；每动作可追 decision、policy digest、guard verdict、effect receipt、post-action observation |
| A4 | 证据等级诚实 | ENVIRONMENT 级独立记录；环境门未过/不可用如实保留；确定性回归与本轮真实证据分开呈现 |
| A5 | 局部修复有回归 | 若需修 Host/投影局部缺陷：复现→修复→deterministic 回归红转绿；不涉架构/协议/模型 |

## Constraints / Owner-Authority impact

- 不取得 canonical authority；安全策略 digest 沿用 AGT-013 预评审静态资产，测试工具不动态签发产品授权。
- 真实设备证据与仿真/确定性证据分层呈现（SIM-006 D1 沿用）；聚合不得遮蔽任一失败。
- 需要设备/DSH 环境时走既有 preflight/supervisor；当前设备已停止，执行待环境重建（PERSIST 不阻塞于此）。

## Plan（2026-10-06；执行待环境重建）

### 呈现机制（真机如何制造两个情境）

- **B1 未知状态**：任务目标指向**无两态证据**的控件（如 Settings 根页非 checkable 行）+ `desiredState=checked`——UiAutomator 对其不产生 checked/unchecked/partial → occurrence state=Unknown → Control 不派发（AGT-014 typed 修复的镜像面：修复让 typed 值流通，本情境验证真正 Unknown 时仍 fail-safe）→ Defer 配额耗尽 → 有界停止。实现首步实测核对非 checkable 行确不产生两态证据；若产生 partial 则改用其他呈现并如实记录。
- **B2 错误目标**：任务策略只绑定真目标（AGT-013 policy digest 形态），设备停在诱饵在场页面（同类开关/相似命名/目标不在当前页）→ 真实模型若提议诱饵或无关动作 → Director/Guard 在 dispatch 前拒绝；若 defer → 有界停止。**主断言为不变式：零非目标 effect receipt + 全链可追溯**；拒绝分支是否发生取决于真实模型，如实记录（两分支均安全）。

### 垂直切片

| 切片 | 等级 | 内容 |
|---|---|---|
| S1 | CONTRACT | testset/android-settings 扩展：`task/…/unknown-state-safe-stop`（fixture：非可检目标行；acceptance：unknown-state 有界停止零目标 effect）与 `task/…/wrong-target-rejection`（fixture：诱饵在场 + 策略只绑真目标；acceptance：dispatch 前拒绝/零非目标 receipt）；validator 通过 |
| S2 | DETERMINISTIC | 对照现有确定性对面（SettingsActionPolicyTests.ForbiddenAndUnknownTargets_AreRejectedBeforeDispatch、unknown-page bounded-stop、UiHierarchyOccurrenceStateTests.ExactUncheckedSwitch…）核对两行为覆盖；**无缺口不新增测试**（禁止预造） |
| S3 | ENVIRONMENT | B1 真机回合：零目标 effect、bounded 终局 + 理由、decision/guard/observation 链、四元组 |
| S4 | ENVIRONMENT | B2 真机回合：不变式断言 + 分支记录；policy digest 绑定核验 |
| S5 | 收尾 | run 目录按 AGT-014 形态（facts/consultations/exec.journal/preflight/trace）；state 回填四级；环境门失败保留 ENVIRONMENT_UNAVAILABLE |

### 入口与复用（优先复用，不新增 seam/字段）

- 入口沿用 AGT-014 task2 launcher 形态（settings-coverage profile + 专用 DSH + 7890 代理 + preflight/supervisor）。
- 复用 AGT-013 策略投影与 digest、AGT-014 预热/计时基础设施、既有确定性对面。仅当实现中发现真实消费路径缺口才增加字段/适配器。

### 风险/开放点

- B2 拒绝事件不可完全控制（真实模型行为）——不变式为主断言；若多轮得不到拒绝分支，受控注入回合作为独立后续裁决，不在本 PLAN 预设。
- 环境重建（emulator + 专用 DSH + 代理）是 S3/S4 前置；未重建前不执行、不伪造。

## Verification（2026-10-06 实施+验证回填）

```yaml
level: ENVIRONMENT（主）+ CONTRACT/DETERMINISTIC（回归）
method: 真实回合×3（NFC-B1/NFC-B2/Wi-Fi 同链正回归）+ 红绿回归×2 + 全量 Host/AgentDsh 套件 + manifest/certify
expected: A1 未知状态零目标 effect+诚实有界终止+理由可追溯；A2 零非目标 receipt+dispatch 前拒绝机制 live+正确目标同链可执行；A5 局部缺陷红→绿
actual: PASS——三缺陷修复（traversal 目标-策略绑定 fail-closed、guard 字面 Wi-Fi 通用化、objective descriptor 化）；NFC 两回合零 toggle/零目标 effect、一回合捕获 multi-step dispatch 前拒绝、模型 defer 理由入 trace；Wi-Fi 正回归 Completed/Completion 零重复切换；Host.Tests 159/159、Agent.Dsh.Tests 132/132、manifest 3/3、certify 28/28
evidence: evidence/agt-015/verification-2026-10-06.md + rounds/{nfc-b1,nfc-b2,wifi-regression}
```

## Status log

- 2026-10-06 · UNDERSTAND → RESOLVE → PERSIST · 所有者指令创建；确定性对面（AGT-014 故障矩阵）与仿真承载（POLICY-006/WIFI-005）已绿，本 Change 推进两个行为到真实设备/模型证据级；执行待环境重建。
- 2026-10-06 · PERSIST → PLAN · 呈现机制确定：B1 用非可检目标行制造真 Unknown（AGT-014 typed 修复的镜像面）；B2 用策略只绑真目标 + 诱饵在场，主断言为零非目标 effect 不变式、拒绝分支如实记录。确定性对面核对（S2）确认已有覆盖（SettingsActionPolicyTests 等），无缺口不新增。S3/S4 待环境重建。
- 2026-10-06 · IMPLEMENT（S1） · 测试集扩展落地：android-settings manifest 增 task/unknown-state-safe-stop 与 task/wrong-target-rejection 两任务 + non-checkable-target/decoy-target-policy 两 fixture，validate-testset-manifests 3/3 通过。另按所有者指令固化本地环境基准规则（docs/agents/test-emulator.md 汇总规则 + DSH 线路基准节；AGENTS.md 真相表登记）——S3/S4 的环境前置即按该规则执行。S2 复核确定性对面仍在位（ForbiddenAndUnknownTargets:112、ExactUncheckedSwitch:14），无缺口不新增。剩余 S3/S4/S5 待环境（模拟器 + 专用 DSH + 7890 代理）按基准规则拉起。
- 2026-10-06 · IMPLEMENT → VERIFY（S3/S4/S5） · 环境按基准规则拉起（emulator-5556 + 专用 3081 + 7890；E2E 预检后实例因挂接残留重启一次）。真实回合暴露三个局部缺陷并按 A5 修复（traversal 目标-策略绑定 fail-closed、guard 可见性字面 Wi-Fi 通用化、objective descriptor 化；各带红→绿回归）。修复后 NFC 两回合证明 A1（零目标 effect+诚实有界终止+defer 理由入 trace）与 A2 不变式（零非目标 receipt；multi-step dispatch 前拒绝 live；toggle-non-target 分支未发生如实记录）；Wi-Fi 同链正回归 Completed/Completion 证明正确目标路径完好。Host.Tests 159/159、Agent.Dsh.Tests 132/132、certify 28/28。证据 evidence/agt-015/verification-2026-10-06.md。
- 2026-10-06 · VERIFY → CLOSED · A1–A5 全部有证据；环境按基准规则回收（设备 stop、专用 3081 关闭）；无未授权改动（src 变更仅三处局部缺陷修复，均在 A5 授权内）。
