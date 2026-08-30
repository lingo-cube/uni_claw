# Runtime Debug P1d — Base Packet Generator — Graduation Decision

> Status: GRADUATED (human-authorized) | Decision: `GRADUATE_RUNTIME-DEBUG-P1D-PACKET-GENERATOR` | Date: 2026-08-30
> Change: `openspec/changes/archive/2026-08-30-runtime-debug-p1d-packet-generator/`

## 1. Buyer and exact claim boundary

**Buyer:** Runtime Debugging Toolchain 只读确定性查询/投影面（Debug Query Core 的增量能力）。

capture bundle → 机械 Debug IR base packet：TerminalState/TargetObservation(stored)/TargetOccurrence(CANDIDATE)/evidenceIndex=CAPTURE_ASSET（AssetRef 字段）/MissingEvidence 枚举/repairGate/确定性 digest；零语义伪造。

AuthorityDelta: NONE · ArchitectureDelta: NONE · RuntimeBehaviorDelta: NONE。

## 2. Validation evidence

- AgentWorkflow 套件（uv pytest）：**199 passed + 13 subtests**（本片测试在列）。
- 契约测试覆盖：生成→存盘→summarize/occurrence/evidence 全部 OK 读回（P1a/P1b 兼容闭环）；byte 确定性；禁止字段断言（GapKind/Owner/Disposition/Good/Bad/… 不存在）；未知 seq EVIDENCE_UNAVAILABLE。
- `openspec validate runtime-debug-p1d-packet-generator` PASS；`check-consistency.sh` ALL PASS；`git diff --check` OK。

## 3. Falsifier result

语义面绝不生成（MissingEvidence 显式声明缺失）；digest 按 P0 约定；不写盘 stdout only。

## 4. Deferred scope

语义 chain 构造（需标注源）、多 run 配对 Good/Bad、FDP/Owner 自动判定。

## 5. Final conclusion

**GRADUATED.** 本切片对应能力已验证并归档；deferred 项需各自独立 gate。
