# PNL-010 — 工具产出路径可配置 + 本地 profile 落盘

lifecycle_state: implement · disposition: none · depth: minimal · base: working-tree

## Intent（WHAT/WHY）

PNL-008 的工具调用把产出硬编码为 `<runDir>/report`；owner 要求产出路径可配置
且本地落成配置。本 Change 引入 Runtime Host 工具执行配置（`.dsh/profiles/
tool-runtime.yaml`，与 uniagent-prod / settings-coverage profile 同款发现与
覆盖规则），并保持 PNL-008 的产物引用纪律（runs 根相对路径、不泄露宿主
绝对路径）。

## Scope

- `.dsh/profiles/tool-runtime.yaml`（本地默认配置，仓库落盘）：`output.base`
  （run-dir | runs-root）与 `output.subdir`（单段目录名）。
- `RuntimeToolConfig`（src/UniClaw.Host.Dsh/RuntimeToolConfig.cs）：env 覆盖
  `UNICLAW_TOOL_RUNTIME_CONFIG`、装配目录向上发现、受限 YAML 子集解析（含
  引号剥离）、存在但非法 fail-closed；文件缺失 → 默认 run-dir/report。
- `RuntimeToolHost`：`Load(registryPath, output?)` 接收配置；InvokeAsync 按
  base 计算产出目录（run-dir → `<runDir>/<subdir>`；runs-root →
  `<runsRoot>/<subdir>/<runDir>`），解析后越出 runs 根即 fail-closed；
  `Output` 只读投影。
- `GET /api/uniclaw-runtime/tools` 响应新增 `output: {base, subdir}` 透出
  （Workbench 可显示产出落点；additive，不改既有字段）。
- 测试 +7：合法解析、仓库 profile 加载（run-dir/report）、非法值四例
  fail-closed（含引号空串）、runs-root 模式真调用（产出集中在
  `<runsRoot>/reports/<runDir>` 且不污染原 run 目录）。

## Out of Scope

- per-tool 产出配置（当前唯一 implemented 工具是 run-report，全局足够；
  出现第二个有产出诉求的工具再按 buyer 提升）。
- Workbench UI 展示 output 配置（响应已透出，UI 消费随后续 change）。
- CLI 侧 gen-run-report.py 的 --out-dir 语义不变（本就可配，配置在 Host 侧）。

## Decisions

1. 配置属 Host adapter 层，不进共享 tool-registry（ADR-0039：调用形态绑定
   细节在 adapter；registry 只管工具身份与暴露元数据）。
2. `runs-root` 模式语义：产出集中到 `<runsRoot>/<subdir>/<runDir>`，原 run
   目录不被污染——服务「批量查看/清理报告」的真实用法。
3. 双重防护：配置加载时校验（base 枚举 + subdir 单段），Invoke 时再验解析后
   路径仍在 runs 根内（`tool-output-escapes-runs-root` fail-closed）。
4. 引号剥离后显式空值（`subdir: ""`）= 非法（fail-closed），不静默回退默认。
5. PNL-008 的 D4 纪律保持：响应中产物引用恒为 runs 根相对路径。

## Alternatives rejected

- 产出根允许指向 runs 根外任意路径：破坏相对路径纪律并泄露宿主路径。
- 模板字符串（如 `{runDir}/report`）：两个正交字段已覆盖真实需求，模板
  引擎是无 buyer 的复杂度。

## Acceptance

1. 配置测试全绿（含真调用 runs-root 模式）。
   - method: dotnet test tests/UniClaw.Host.Dsh.Tests
   - expected: 19/19
   - actual: 19/19 pass
   - evidence: verification 段命令 1
2. 仓库 profile 落盘并被默认加载（run-dir/report）。
   - method: LoadDefaultInRepoUsesCommittedProfile
   - actual: base=run-dir, subdir=report
   - evidence: RuntimeToolConfigTests
3. 非法配置 fail-closed：坏 base / `../escape` / `a/b` / 引号空串全部抛
   InvalidOperationException。
   - method: InvalidConfigFailsClosed 四理论用例
   - actual: 全部拒绝
   - evidence: 同上

## verification（2026-10-07，CONTRACT/DETERMINISTIC 级）

```text
1. dotnet test tests/UniClaw.Host.Dsh.Tests → 19/19 pass
   （含 InvokeHonorsRunsRootOutputBase：真 python3 调用，产出
   reports/run-cfg-001/report.{json,md}，断言原 run 目录无 report/ 子目录）
2. 解析器引号剥离修复由 InvalidConfigFailsClosed("subdir: \"\"") 用例锁定
```

## Status

- 2026-10-07 IMPLEMENT：配置类 + 仓库 profile + Host 接线 + 端点透出 + 7 用例。待 REVIEW。
