# HYG-001 — harness 卫生安全子集（优化评估落地）

lifecycle_state: closed · disposition: none · depth: standard · base: 14be825

## Intent

2026-09-22 优化评估的「足够安全」子集落地（用户主动指令，台账事件 #13）；
其余项（评审前机械事实核对、docket 事实新鲜度、C2 档正式化、skill 长尾）
留待 GATE-001 校准报告（2026-10-04 或 ≥30 事件）后再议。

## Scope

- `model-routing.yaml` 陈旧引用修复（SKILL.md §13-§14 → §B4-B5 实际编号）
- `changes/INDEX.md` 生成物 + `tools/gen-open-changes.py`
  （GATE-001 P-E′「open-gates 可生成索引」提前落地；一致性执法仍留二期）
- HOST-001 / UIW-005 头部 `lifecycle_state` 补齐 closed
  （裁决已由 ee790f7 完成且状态日志在档，本 change 仅记账一致性修复）
- GATE-001 台账：#12 回填 ruling（协议允许的人裁决后补录）；#13 追加
- `workitems/README.md` 载荷保留规则（见 D1）
- 本地构建/恢复日志删除（gitignored，无仓库影响）

## Out of Scope

- SKILL.md / ADR-0007 任何改动（GATE-001 实验期冻结面，零接触）
- 评审前机械事实核对、docket 事实新鲜度（待校准报告）
- C2 档写进流程语义、skill 长尾处置（待校准报告）
- CORE-004/005/006 closure（DECISION-HEAVY 保留人工 closure，一期零自动关闭）

## Decisions

- D1 载荷删除被证据否决：FSV-001 / WI-P* 载荷被 5 份 evidence/docs 引用
  （`docs/analysis/harness-v2-compatibility-matrix.md` 等）→ 不删，
  改为在 `workitems/README.md` 落保留规则（被引用即证据）。
- D2 HOST-001/UIW-005 仅翻头部字段 + HOST-001 补一行状态日志；
  不重写既有日志行，修复依据 = ee790f7 提交信息 + 两份状态日志 + 台账 #12。

## Acceptance

1. `model-routing.yaml` 引用的章节在 SKILL.md 中实际存在（§B4/§B5）
2. `tools/gen-open-changes.py` 可运行且幂等；INDEX.md 与各 state.md 头部一致
3. HOST-001 / UIW-005 `lifecycle_state: closed`，与 ee790f7 / 台账 #12 一致
4. 台账 #12 ruling 已回填、#13 已追加；无历史行删除或语义改写
5. 根目录 `*.log` 本地删除

## Verification

```yaml
level: DETERMINISTIC
method: python3 tools/gen-open-changes.py（两次，比对幂等）+ git diff --stat
        + grep '§B4-B5' model-routing.yaml + grep lifecycle_state
        changes/HOST-001/state.md changes/UIW-005/state.md
expected: 两次生成 INDEX.md 内容一致；引用/头部/台账按 Acceptance 1-4 成立
actual: 幂等 SHA256 一致；INDEX.md 82 changes / 4 open（CORE-004/005/006
        verified + GATE-001 implementing，与逐目录扫描一致）；§B4-B5 引用
        命中；HOST-001/UIW-005 头部 closed；台账 #12 回填 + #13 在档；
        本地 6 个 *.log 已删
evidence: 本文件 Status log + 提交 diff（SHA 见 Status log）
```

## Status log

- 2026-09-22 · created·implemented·verified·closed · 单会话完成（用户指令
  安全子集，台账 #13；Verification actual 见提交）。
