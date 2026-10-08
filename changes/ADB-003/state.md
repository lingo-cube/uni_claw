# ADB-003 — D9 实战环境修复与恢复规程（快照锁 / DSH 专线纠偏）

lifecycle_state: closed · disposition: none · depth: minimal · base: 63804f7a

## Intent

CAP-012 D9 首演（真机覆盖遍历产出 capability-profiles/language-findings）所需的
环境修复，及其规程沉淀（test-emulator.md 与注册事实同权，变更留痕）。

## 修复记录（2026-10-07，全部有真实验证）

| 问题 | 根因 | 修复 |
|---|---|---|
| 模拟器启动 FATAL "snapshot operation pending" | 上次异常关停在基础 AVD（p26_pixel）残留 hardware-qemu.ini.lock / multiinstance.lock + 2.4GB 陈旧 quickboot 快照 | 清锁 + 快照目录改名备份（snapshots.stale-20261007）→ 冷启动 READY（api=35 探针全过） |
| 握手 schema-hash-mismatch（product=4a54… vs dsh-local=1fb4…） | ①uniagent-prod.yaml baseUrl 指 3080（残留旧插件实例）；②注册专线 3081 的服务跑了 1d+，早于协议更新未重载 | yaml 纠正至 3081；重启 3081 专线实例（dk-harness `dsh web --port 3081 --no-open`）重载当前插件 |
| language-findings 不产出 | D5 显式启用设计：任务块未声明 | settings-coverage.yaml 增 languageInspection 块（required/capabilityId/expectedLanguage: en/ignoreRoutes: rk1:Internet——API 35 SSID 列表所在页，D1 裁决） |

## 验证（ENVIRONMENT 级）

CoverageComplete 100%（19 步、7/7 一级、8/8 二级、deepseek-flash 真模型经 3081）；
language-findings.json：20 capture 全 Pass、忽略区在位；capability-profiles.json
真数据落盘。run 证据：src/UniClaw.Host.Dsh/runs-d9/run-20261007-135120-724
（生成物不提交，按 test-emulator.md 证据归属规则）。

## Status log

- 2026-10-07 · UNDERSTAND → IMPLEMENT → VERIFY → CLOSED · 三修复 + 规程入
  test-emulator.md；DSH 3080 旧实例未动（非本线管辖，留所有者处置）。
