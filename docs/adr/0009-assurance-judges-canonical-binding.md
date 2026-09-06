# 0009 — Assurance 授权对象是 Canonical Binding（目标序 Bind→Judge→Gate）

C2E-002 已验证的实现序是 `Judge(intent, candidate) → Bind → Dispatch`：
Assurance 先审 candidate，CanonicalBinding 认定后不出 Effect Boundary。协议
基线 grill（PROTOCOL 会话，2026-09-09）裁决**目标协议**改为：

```text
CandidateBinding
→ Effect Boundary canonicalize
→ CanonicalBinding
→ Assurance Judge(Intent + CanonicalBinding + current state)
→ Judgment
→ Gate
→ Dispatch
```

理由：授权必须针对「最终真正要执行的那个 target」；Assurance Judgment 必须
语义绑定 exact Intent + exact CanonicalBinding + applicable WorldBelief
revision（校验机制——引用 / ID / 签名 / registry——是实现细节）。该序与
不变量 25 的链条 candidate → canonical → admissible → authorized → command
一一对应。已验证切片的 `Judge(candidate)` 序记录为 **known deviation**，
不直接升级为目标协议；迁移列入协议基线第 5 节（Recommended first
implementation/change），不在协议会话内执行。

## Consequences

- Binding validity 与 authorization 分离：judgment 拒绝不使 canonical
  binding 失效——validity 仍由 revision / freshness / consumption 派生判定
  （无 event），binding 存在 ≠ authorization。被拒 binding 是否允许重新
  judgment：deferred，等真实 buyer。
- AssuranceJudgment 协议载荷须携带 IntentId + BindingId + RevisionId
  三元组（当前实现仅 IntentId，属同一 known deviation）；三元组是
  correlation key，不是 canonical identity。
- CandidateBinding 在目标协议下只流向 Effect Boundary，不进入 Assurance
  输入。
- Uni Kernel Act pipeline 组合序需重排；C2E-002 已验证的验收事实不变，
  实现与测试语义随迁移 change 调整。
