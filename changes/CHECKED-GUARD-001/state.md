# CHECKED-GUARD-001 — checked 消费有效性门
lifecycle_state: closed · disposition: none · depth: standard · base: 9bc15b8974f5fb5746074e373e6c132bc4df0089

## Intent / Scope
修复公开 KernelRunDriver 链把 checkable=false/missing 的 unchecked 误判为状态验证通过。仅在 SemanticCheckedResolver 消费同 capture+node 的 checkable Observed(true)，raw checked 继续保留；RuntimeAssurance 只修必要注释。不按类名或其他节点/旧 capture 借 guard，不改变 capability 缺席、Unknown、Partial、时序与身份拒绝边界。

## Decisions / Constraints
- 依据 PER-009 D3 与 PER-010 Checked validity guard；不新增接口/authority，不扩展 World 协议。
- SIM-011 原 11 例输入、测试和独立 expected 冻结；SIM-010 原 XML/录制不改、不升级 Exact。
- 允许逐项修复因 guard 暴露的既有 synthetic fixture 缺 checkable 真观察，并追加公开产品缝回归；false/missing 真实负例保持不变。
- 20 个 golden 场景仅经既有 certify 更新本 Change 致因的认证元数据；旧输入/期望与 expectationsDigest/executionDigest 必须不变。
- 不碰共享 INDEX/CONTEXT、工具/路由、设备/真实模型；不提交推送、不消息其他聊天。保护快照见 evidence/checked-guard-001/entry/。

## Acceptance
1. 同一 11 例由 9 PASS/2 FAIL 变为 11 PASS；false/missing 明确 VerificationFailed，raw checked 保留。
2. 公开缝反例证明 malformed guard、同 capture 其他 node true、旧 capture true 不授权目标；true 正例与既有 Unsupported/Unknown/Partial 等拒绝边界保留。
3. 旧 SIM010 原 XML 仍 Unknown 拒绝；相关回归、真实 full 八工程、34 场景发现/认证通过；独立 Review 与 Verify 分开。
4. 源码/输入 hash、20 个认证块差异及全部旧输入/期望不变有证据；失败尝试保留；无未授权修改。

## Plan
隔离 /tmp 快照复现 → 同节点 guard → 专项及必要 fixture 修正 → metadata-only 认证 → 独立评审 → full/coverage/hash 验收。

## Verification
level: SCENARIO
method: 公开运行链原 11 例 + 公开产品缝反例 + full 八工程 + 场景认证/coverage + hash
expected: Acceptance 1-4
actual: original 11/11 PASS; public guard 7/7 PASS; Kernel 1044/1044; full eight projects 2064/2064; certification/coverage 34/34; independent review two axes zero blockers; read-only evidence verifier PASS
evidence: evidence/checked-guard-001/

## Status log
- 2026-10-22 UNDERSTAND→RESOLVE→PERSIST→PLAN→IMPLEMENT：复验 SIM-011 canonical post 诊断与既有契约；不重复已有诊断实验。独占快照带 platforms golden 与 SIM009 25 个历史 XML；原 11 例已复现 9 PASS/2 FAIL（controlled-red.trx），两失败 Expected False/Actual True。
- 2026-10-22 IMPLEMENT→REVIEW：原11例 GREEN；公开guard回归先1 PASS/6 FAIL后7 PASS，raw保留。30个旧Kernel失败逐项补synthetic guard，1044/1044；首轮full失败留档，metadata-only认证后真实full八工程2064/2064，34场景PASS。独立单worker审查进行中，Verify文件与保护hash核对分开。
- 2026-10-22 REVIEW：首个独立Review因/tmp build目录已更新、不能充当before-image而未签结论；保留过程说明。通过原Git对象重建30个owned文件，每个SHA256严格等于写前快照，存entry/before/；同一独立worker复审后Standards/Spec均零阻塞（review.md）。
- 2026-10-22 REVIEW→VERIFY→CLOSED：Verify独立记录四元组（verification.json）；verify-evidence.py与最终认证只读检查PASS。原11输入/expected不变，公开guard7例通过，full八工程2064/2064，34场景认证/发现通过。源/输入887项与实际构建快照一致，507项冻结输入不变；19838项保护文件无缺失，仅30个owned既有文件改动，其他3个World文档并行变化单列不回退。不启设备/模型、不提交推送，交父线独立验收。
