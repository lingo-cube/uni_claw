# PNL-011 — P2 挂接：finalize 产物三件套（metadata 投影 + runtime 事件导出 + 自动报告）

lifecycle_state: implement · disposition: none · depth: standard · base: working-tree

## Intent（WHAT/WHY）

PNL-006 记录的 P2 缺口：报告靠手动 CLI 生成、需求原文不落盘、Host 生命周期
事件进不了报告（host 分区恒降级）。本 Change 在 RuntimeHttpServer finalize
路径一次补齐三件套，使每个 HTTP 发起的新 run 自动携带自包含 evidence 与
全链路报告。

## Scope

- `RunBackground` finalize 附带产物（try/catch 包裹，失败不改 run 终态，落
  `report-generation.log`）：
  1. **metadata 投影**（`metadata.json`，`uniclaw.host-metadata.v1`）：runId/
     productSessionId/launch 关联/dshSessionId/requirement/title/taskSet/
     device/model/dshEndpoint/status/outcome/delivered/receipts。需求原文自
     taskRef（web launch 流约定 `taskRef.label` = requirement）提取。
  2. **runtime 事件导出**（`runtime-run-events.json`）：分页读全
     RuntimeRunStore 事件（store 仍为 authority，导出是 run 目录投影副本）。
  3. **自动报告**：`ToolHost().InvokeAsync("run-report", ...)`，产出路径遵循
     PNL-010 配置；finalize Transition 的 artifacts 追加 report 引用（用实际
     runDir 目录名，比既有 runId 形条目更准确）。
- `gen-run-report.py`：输入集 +`runtime-run-events.json`；需求解析链
  metadata.requirement → --requirement → 未采集；host 分区 lifecycle 表
  （seq/eventType/source/authority/summary）与 coverage 升级为 present。
- schema：hostLayer 新增 `lifecycle` 数组；artifact 名单含 runtimeRunEvents。
- goldens 重生成（input.artifacts 增一节）；schema 校验器 13 全绿。

## Out of Scope

- Program.cs CLI 直跑路径的自动报告（dev 手动流，工具面板已可覆盖；HTTP 面
  是产品真实入口）。
- runId→runDir 的 store 映射字段（report 条目已用目录名；映射留待真实 buyer）。
- androidApi/wmSize 进 metadata（旧采集流程的 adb 字段；服务器侧现不掌握，
  报告按未采集降级，如实）。

## Decisions

1. metadata.json 由 adapter 层（RuntimeHttpServer）写，不进 HostRunner：
    设备/模型/endpoint 是 realization 关切（历史 fixture 的 metadata 正是
    采集流程手写的投影）；产品执行器保持无 DSH/环境知识。
2. 事件导出优先于生成器读 store：run 目录成为自包含 evidence，python 不
   耦合 `.runtime-runs` 存储布局，绕开 runId→目录映射缺口。
3. 报告生成失败不阻塞 finalize：报告是派生产物，run 终态由执行事实决定；
   失败落 run 目录日志 + artifacts 中 report 标 partial。
4. 需求只认 taskRef（web launch 流把 requirement 放 taskRef.label 并同时
   传 metadata 数组——LaunchRequest 无 metadata 字段，taskRef 是唯一稳定
   载体）。

## Alternatives rejected

- 生成器直读 `.runtime-runs`：耦合存储布局 + 需 runId 映射。
- HostRunner 写 metadata：产品代码获得 realization 知识，边界倒退。
- 报告失败标 run failed：派生产物故障不该污染执行终态。

## Acceptance

1. 旧 fixture golden 稳定（无 runtime 事件/需求时输出语义不变，仅
   input.artifacts 增一节）。
   - method: --check
   - actual: CHECK-PASS（20562B json / 8250B md）
   - evidence: verification 段 1
2. 合成 P2 run（metadata.requirement + runtime-run-events.json）：host
   coverage=present、lifecycle 3 事件入表、①需求区 present/observed/
   metadata.json、schema 0 errors。
   - method: 合成 run 目录 + 生成 + Draft202012 校验
   - actual: 全部符合
   - evidence: verification 段 2
3. C# 编译零错误、既有 19 测试不回归。
   - actual: 已成功生成；19/19
   - evidence: verification 段 3
4. 仓库 schema 校验器收录 hostLayer.lifecycle 后全绿。
   - actual: PASSED 13
   - evidence: verification 段 4

## verification（2026-10-07，DETERMINISTIC/SCENARIO 级）

```text
1. fixture golden 重生成 + --check → CHECK-PASS
2. 合成 P2 run（/tmp 拷贝 fixture + requirement + 3 事件）：
   host coverage=present；lifecycle 3；requirement present/observed/metadata.json；
   schema errors=0；md 呈现 lifecycle 表与①需求原文
3. dotnet build → 0 error；dotnet test UniClaw.Host.Dsh.Tests → 19/19
4. python3 tools/validate-workspace-schemas.py → PASSED 13
```

## Status

- 2026-10-07 IMPLEMENT：finalize 三件套 + 生成器/ schema 升级 + 回归判据
  （见 evidence/pnl-006/README.md）。待 REVIEW。
