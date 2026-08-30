# Runtime Debug P4b — Replay Projection — Graduation Decision

> Status: GRADUATED (human-authorized) | Decision: `GRADUATE_RUNTIME-DEBUG-P4B-REPLAY-PROJECTION` | Date: 2026-08-30
> Change: `openspec/changes/archive/2026-08-30-runtime-debug-p4b-replay-projection/`

## 1. Buyer and exact claim boundary

**Buyer:** Runtime Debugging Toolchain replay 干跑面（P4 断言基础）。

replay fixture 的确定性干跑投影（replay-run）：有序轨迹、步骤/观测/动作计数、最后观测 seq、首个机械非 OK 步骤（resultOutcome ∉ {Dispatched,Succeeded}）；校验前置、不仿真、不最小化。

AuthorityDelta: NONE · ArchitectureDelta: NONE · RuntimeBehaviorDelta: NONE。

## 2. Validation evidence

- AgentWorkflow（uv pytest）：**245 passed + 1083 subtests**。
- AgentWorkflow 245 passed + 1083 subtests；Rejected order-3 判机械首失败、干净 fixture 无失败、缺失 fail-closed。
- `openspec validate runtime-debug-p4b-replay-projection` PASS；`check-consistency.sh` ALL PASS；`git diff --check` OK。

## 3. Falsifier result

机械失败定位纯 stored-outcome 判定（显式 note，无语义推断）；与 replay 共用读/校验路径；零设备/状态仿真。

## 4. Deferred scope

minimize（P4c）、语义充分性判定、可执行重放引擎。

## 5. Final conclusion

**GRADUATED.** P4b 已验证并归档。
