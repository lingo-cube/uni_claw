# WRC-003 代码审查记录

## Leader 审查（已执行，非独立评审）
审查范围为 implementation-files.json 与 scenario 认证字段；排除 entry 快照中其他 Change 的修改。检查操作与验证权、accepted evidence 来源、版本/时序/作用域、历史保留、未知处理、Replay 是否自造配准、schema/document 同步和场景期望是否篡改。

发现 R1：只按所选区域 bounds 选锚时，嵌套滚动区内部的两个移动锚可使外层错误得到 Aligned。`viewport-review-red.trx` 复现，产品增加相交 scrollable 检测并返回 OverlappingScrollRegions；`kernel-reviewed-final.trx` 通过。当前没有 scroll-parent 关系，因此不猜测嵌套区域归属。

Standards：World 独占关系/位移，Kernel 仅组合真实 receipt 和 Assurance 结果；Host 策略仍提案，Replay 只投影。不新增假买方接口；新增公共载荷有设计与 schema/白名单同步。无额外未处置的 Leader 发现。

Spec：已有导航边被消费，同页滚动建立独立关系；正反向与二维平移有例证；未知不累计；旧 capture 与 NaN 已有反例。录制帧未知、任意长页拼图、慢感知模型质量与真实设备均不声明通过。

## 独立评审（未执行）
当前 Host 缺少 `.codex/` 模型绑定；DSH 绑定不能直接映射到当前可用子代理模型。UniFlow B4：`primary 不可用 → fallback；均不可用 → 显式报告 ROUTING_UNAVAILABLE，禁止静默落回会话默认模型`。

已请求仅本 Change 的当前 Codex 模型委派授权，尚未收到回复。没有派发子代理，没有把 Leader 审查冒充独立评审，不能据此把 Change 标记 reviewed/closed。恢复入口为 changes/WRC-003/state.md、design.md、implementation-files.json、implementation.patch 与 verification.json。


## 2026-10-21 独立评审补齐
当前用户授权本会话Leader按顺序收口；Standards/Spec分别由独立子代理执行，最终均0阻塞。Spec初审一条报告空Space猜测被短路语义和真实script反证，审查者已撤回，未据误报修改产品。评审与本轮24项验证见 `evidence/eng-opt-001/wrc-independent-review.md`。上述未执行记录保留为历史。
