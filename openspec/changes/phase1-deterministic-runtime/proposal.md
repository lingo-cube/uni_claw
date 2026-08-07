# Proposal: Phase 1 — Deterministic Runtime（Normal WiFi Scenario）

| 属性 | 内容 |
|------|------|
| Change ID | `phase1-deterministic-runtime` |
| 状态 | Proposed |
| 类型 | **Vertical Slice**（宪章 §60 第一项工作：A Architecture Proposal + B Minimum Contracts） |
| 日期 | 2026-08-07 |
| 分支 | `uni-agent`（架构框架分支） |
| 根 Change | `greenfield-agent-runtime`（Phase 0 地基） |

## 动机

Phase 0（工程边界 + 机械 Guard）已完成。宪章 §60 规定第一项工作：

A. Architecture Proposal → B. Minimum Contracts → C. Fake Environment → D. Normal WiFi Scenario → E. Recovery WiFi Scenario → F. Architecture Review

按宪章 §54（Requirement → Scenario → Responsibility → Authority → State Owner → Interfaces → Implementation → Verification）与 §12 / I-12（没有 Requirement 支撑的复杂度不提前实现），先做 A + B：把 Runtime 的第一条 Vertical Slice 设计定稿并契约化，为 C + D 的实施提供审批基线。设计通过后再编码，避免"Prompt → immediately code"。

## 目标（本 change 范围）

1. **Architecture Proposal**（`design.md`）：component model / ownership table / dependency diagram / runtime state model / normal lifecycle / trap-recovery lifecycle / minimal project structure / deferred decisions
2. **Minimum Contracts**（`specs/`）：只定义第一条 vertical slice 真正需要的 contracts：
   - `run-lifecycle` — Run 生命周期 + Startup + RecoveryAnchor + World Belief 建立
   - `environment` — 观察/动作端口 + Fake Environment 确定性
   - `container-traversal` — Container 局部状态域 + Traversal 步骤 Kernel + Agent 容器管理
   - `normal-wifi-scenario` — §34 第一条必须通过的场景（端到端验收）
3. **实施清单**（`tasks.md`）：C（Fake Environment）+ D（Normal WiFi Scenario）的落地步骤，审批后按序执行

## 非目标（Deferred，本 change 不解决）

- E Recovery WiFi Scenario — 属 Phase 2（Trap & Recovery 机制）change
- F Architecture Review — 完成 A–E 后执行
- 任何真实设备能力（Phase 4）、Memory / LLM / 语义识别算法（Phase 5+）
- Scroll / Popup / Uncertain Action（Phase 3）

## 验收

- `design.md` 覆盖 §60-A 全部八项输出，无 God Object / 重复 authority / Runtime State 与 World State 混淆（§60-F 预检）
- `specs/` 只含第一条 slice 需要的契约（I-12），每条 SHALL 可被 Scenario 测试断言
- 实施后（C+D）将满足 §57 第一阶段完成标准的第 1、6、8、9、10、11、12 条：
  Normal 场景 Fake 全跑通 / Startup 建立 RecoveryAnchor / lifecycle 与 traversal 职责分离 /
  单 owner / Guard 机械保证 / Trace 可解释每步 / 无 LLM 依赖
