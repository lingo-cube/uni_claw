# 2026-10-22 语义假设失败后的修订

## R1：单步验证与导航目的不同
初版要求未变化 post 的最终状态为 VerificationFailed。实跑证明默认 tap 的当前义务只检目标存在；页面未变也可能单步 IsVerified=true，随后 NoAction 的最终状态为 TerminalNotProven。独立业务期望仍要求一条 before→after 导航边，因此错误回放必须报告 navigationEdges 首次分歧，不能把单步 verified 冒充导航成功。不改产品语义；目的地符合性仍是显式 gap。证据：recorded-diagnostic.trx、output/recorded-network-unchanged-diagnostic.json。

## R2：旧 XML 不证明关闭
初版预期旧 checked=false 产出 unchecked 并验证 off。实跑为 Unknown(partial-unrepresentable)，投影没有 checked claim，Assurance 拒绝 no-checked-claim；这是正确的证据边界。用户明确选择保留旧录制 Unknown 拒绝，另建补采/证明设计前置。修订后区分 expectedBehavior（当前证据应安全拒绝）和 desiredOutcome（关闭已证明，仍未满足）；两者独立固定，不能以实际输出抹去业务 gap。证据：recorded-diagnostic.trx、output/recorded-stay-awake-direct-diagnostic.json。前置设计：REPLAY-STATE-EVIDENCE-001，会话 01a163b7-c102-70f0-950d-137d03110b71。

## R3：独立 composition root 不可引用 Host
初版给 Simulation 工程增加 Host 引用。ProductHostClosureTests 明确拒绝第三个引用；baseline §24.8 要求两个 Host 独立组合。撤销本次引用，不改闭包断言。改为显式离线导入 reviewed immutable typed observation 及无 canonical ID 的 occurrence 候选：独立 Host 测试逐项核对原 XML 经公开 parser/projector/Host occurrence adapter 的结果与冻结输入完全一致；Simulation 通过测试侧 adapter 只在整 capture 已入账后释放导入候选，并保留全部当次 canonical evidence basis；World 铸 occurrence ID/绑 basis。Simulation 消费这些已导入 typed 输入，并继续走同一产品 projector→Ledger→World→grounding→Effect→Assurance。导入只写观测资产，永不生成 expected；原 XML 哈希、导入资产哈希和导入过程全部可复现。不会把跨测试的链表述成 Simulation 直接调用 Host parser。

## 修订 Acceptance
1. Host 侧原 XML→typed observation/候选的冻结映射由真实 parser 比对；Simulation 仅引用 Kernel+Agent，消费哈希钉扎的 reviewed immutable 输入，走完整产品所有者链。
2. 缺 post 等待且单次投递，release 后同一 Run 恢复；网络对应 post 验证并建导航边。旧开关 post 恢复后拒绝验证，并带 VerificationFailed 再咨询及 no-checked-claim 原因。
3. 未变化网络 post 虽单步验证，独立导航期望检出分歧；未变化开关 post 拒绝。关闭已证明的业务期望不降低，旧录制必须保持该期望未满足。
4. 导入输入被篡改拒绝；验证结果变异（网络 false、Unknown 开关 false→true）均被独立行为期望检出。
5. 新鲜 full 八工程、场景映射、独立 Review 与 Verify、保护已有更改；等待两个前置结果后才交付本切片。
