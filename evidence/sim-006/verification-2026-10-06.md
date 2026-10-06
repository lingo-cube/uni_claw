# SIM-006 后续重核与失败归因证据（2026-10-06）

> 本文件记录 SIM-006 关闭后的 D7 证据处置和一次当前工作树重核。它不改写
> 2026-10-05 的历史 PASS，也不把当前未完成能力改动重认证为场景基线。

## 做了什么

- 将 2026-10-05 的脱敏 `resolved-config` 快照和 2026-10-06 当前重核快照复制到
  `evidence/sim-006/snapshots/`，并用 `manifest.json` 固定文件 SHA-256、来源提交、
  场景范围、失败组和分类。
- 修正 `tools/verify-change` 对认证工具中文诊断“运行时源码哈希不匹配”的识别，
  保留 `BEHAVIOR_REGRESSION` 与 `CERTIFICATION_STALE_ONLY` 并存结果；新增 T22/T23
  回归自测。

## 验证声明

```yaml
CONTRACT:
  method: python3 tools/verify-change SIM-006 --self-test
  expected: T1–T23 全部通过；中文 stale marker 可单独或与行为失败并存
  actual: PASS；T1–T23 全 PASS，FINAL_STATUS=PASS
  evidence: tools/verify-change 自测输出；evidence/sim-006/snapshots/manifest.json

SCENARIO:
  method: python3 tools/verify-change SIM-006 --scope full
  expected: 当前输入、认证、仿真和 coverage 的结果分别保留；认证过期不得遮蔽行为失败
  actual: 当前工作树因 CAP-006/007/008 未收口而失败；修复后输出
    FINAL_STATUS=BEHAVIOR_REGRESSION|CERTIFICATION_STALE_ONLY。20 个认证条目报告
    runtimeSourceHash 失配，仿真同时报告 ScenarioCertification、Agent.Dsh 和 Kernel
    的当前工作树失败；未执行任何自动重认证。
  evidence: evidence/sim-006/snapshots/2026-10-06/SIM-006-full-20261006T091209Z.resolved-config.json；
    当前工作树 runtimeSourceHash=3f79adc650e22da51ee84ea4b0583a76130ce55dfa1b7c59046399486bf32525；
    场景认证块仍绑定 CAP-007 的旧 hash；tools/scenario_certify.py --check 输出

ENVIRONMENT:
  method: verify-change 未传 --live
  expected: live 明确记录为未请求，不影响本地失败归因
  actual: LIVE=SKIPPED (not requested)
  evidence: 同一 resolved-config 快照的 environment.live
```

## 结论

SIM-006 自身的失败聚合缺陷已修复：修复前真实中文 stale 输出只得到
`BEHAVIOR_REGRESSION`，修复后得到两个并存分类。当前 full RED 属于共享工作树中
尚未收口的 CAP-006/007/008 改动触发源码哈希失配和现有契约失败，不能通过重认证
掩盖；待这些能力 change 完成后，再以其致因 change 做一次 C8 认证和 SIM-006
完整重核。
