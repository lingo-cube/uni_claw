# PNL-008 — Runtime Host 工具暴露与 run-report 调用适配

lifecycle_state: closed · disposition: none · depth: standard · base: working-tree

## Intent（WHAT/WHY）

把 PNL-007 已冻结的 Harness Tool Registry 接到 DSH Runtime Host 的消费面：
Workbench 能读取已登记工具的只读投影，并在安全的 run 目录边界内调用已实现的
deterministic-script。planned 工具必须保持不可调用，工具执行结果只返回进程事实，
不把 Harness 工具提升为 Product Capability。

## Scope

- `RuntimeToolHost`：解析 `tool-registry.yaml` 的冻结子集，按 surface 查询，执法
  implemented/deterministic-script/workbench 前置条件，限制 run 目录为 runs root
  下单层目录，并执行工具进程。
- `RuntimeHttpServer`：新增工具清单投影和工具调用端点；复用现有错误 envelope，
  返回 schemaVersion、exitCode、stdout/stderr 尾部及报告产物相对引用。
- DSH Host 运行时工具测试项目加入 `UniClaw.Kernel.slnx`，覆盖真实清单、合成清单、
  planned/未知工具、路径越界、缺失目录和 run-report happy path。

## Out of Scope

- 新 Tool 或 Capability 语义；清单字段与枚举仍由 PNL-007/ADR-0039 冻结。
- Workbench UI、权限审批、外部副作用工具、model-procedure 执行器。
- run-report 本身的 correlator/schema 语义（PNL-006）。

## Decisions

1. Runtime Host 只消费 registry 元数据，不能硬编码工具路径或把 Tool 注册进
   CapabilityHub。
2. 调用入口只接受 runs root 下的单层目录名，拒绝绝对路径、分隔符、`..` 和
   不存在目录；工具进程超时 120 秒并终止进程树。
3. `status != implemented`、`invocation != deterministic-script` 或未暴露到
   `workbench` 的条目统一拒绝调用；planned 诊断工具保持 read-only 规划态。
4. HTTP 端点只返回事实投影；报告路径用相对 runs root 的引用，不泄露宿主绝对路径。

## Acceptance

1. 真实 registry 在 workbench surface 返回 `run-report` 和 `run-diagnosis`，字段
   解析保持稳定。
2. 未实现工具、未知工具、危险/缺失 run 目录均 fail-closed；合法 run 目录调用
   run-report 生成 JSON/Markdown 报告并返回 exitCode=0。
3. Runtime Host 工具适配测试纳入 solution，构建与全量确定性测试通过。
4. Tool registry、workspace schema、scenario certification 与工作树 diff 检查通过。

## Verification（2026-10-06）

| level | method | expected | actual | evidence |
|---|---|---|---|---|
| CONTRACT | `dotnet test tests/UniClaw.Host.Dsh.Tests/UniClaw.Host.Dsh.Tests.csproj --no-restore` | 编译成功且测试全绿 | PASS 12/12 | `tests/UniClaw.Host.Dsh.Tests/RuntimeToolHostTests.cs` |
| CONTRACT | `dotnet test UniClaw.Kernel.slnx --no-restore` | 全 solution 测试通过 | PASS 1417/1417（845 Kernel、178 Host、188 Simulation、12 Tool Host 等） | 本地测试输出 |
| DETERMINISTIC | `python3 tools/validate-tool-registry.py` | registry 合法 | PASS，2 tools | `tool-registry.yaml` |
| DETERMINISTIC | `python3 tools/validate-workspace-schemas.py` | schemas/examples 全绿 | PASS 13 schemas | `schemas/workspace/` |
| SCENARIO | `python3 tools/scenario_certify.py --check` | 无认证哈希违规 | PASS，28 files / 0 violations | `scenarios/*.json` |
| CONTRACT | `git diff --check` | 无 whitespace 错误 | PASS | 工作树 |

## Status log

- 2026-10-06 · IMPLEMENT → REVIEW → VERIFY → CLOSED · RuntimeToolHost、HTTP 两端点、
  安全 run 目录边界与真实 run-report 调用完成；测试项目补入 solution，并修复测试
  文件缺失 xUnit 引用后 12/12 通过。所有 acceptance 已有可复验证据。
