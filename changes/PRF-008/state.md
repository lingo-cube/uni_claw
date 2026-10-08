# PRF-008 — 任务 profile 搬迁：.dsh/profiles/ → product/tasks/

lifecycle_state: closed · disposition: none · depth: standard · base: working-tree

## Intent（WHAT/WHY）

ADR-0041 第二批（设计既定）：任务 profile 是产品概念，从宿主目录
`.dsh/profiles/` 迁到产品声明面 `product/tasks/`；`.dsh/` 只留宿主绑定。
按 ADR-0040，任务 profile 只声明能力要求与参数——本 change 只改位置与
引用，语义零变（字段、env 覆盖变量、解析行为全部不动）。

## Scope

- `git mv` 三个任务 profile：
  - `.dsh/profiles/settings-coverage.yaml` → `product/tasks/settings-coverage.yaml`
  - `.dsh/profiles/language-inspection.yaml` → `product/tasks/language-inspection.yaml`
  - `.dsh/profiles/tool-runtime.yaml` → `product/tasks/tool-runtime.yaml`
- 三个 loader 默认发现路径常量跟随（沿用 PRF-002 的"仓库根向上发现"模式）：
  - `SettingsCoverageConfig.DefaultConfigRelativePath`
  - `LanguageInspectionAllowlist.DefaultConfigRelativePath`
  - `RuntimeToolConfig.DefaultConfigRelativePath`
- 相关注释/文档字符串跟随（RuntimeHttpServer、RuntimeToolHostTests）。
- 一处随搬迁暴露的相对路径解析修正（语义保持）：`ResolvePolicyPath`
  原锚定"自配置目录向上第一个 AGENTS.md"；配置落 `product/tasks/` 后
  中间的 `product/AGENTS.md` 会截断（解析成 `product/product/policy/...`）。
  改为锚定链上最外层（仓库根）AGENTS.md——旧位置（`.dsh/profiles/`）链上
  只有仓库根一个 AGENTS.md，行为不变；yaml 内 `actionPolicy.path` 值不动。
- `product/AGENTS.md` 允许放置表增 `tasks/` 行。
- `docs/architecture/repository-role-boundaries-v0.1.md` product/ 行补
  tasks/（PRF-008 增补）。

## Out of Scope

- 任务 profile 内容/字段/语义任何变更（ADR-0040 语义零变）。
- env 覆盖变量改名（`UNICLAW_SETTINGS_COVERAGE_CONFIG` /
  `UNICLAW_LANGUAGE_INSPECTION_ALLOWLIST` / `UNICLAW_TOOL_RUNTIME_CONFIG`
  全部保持）。
- settings-coverage.yaml 内部 `actionPolicy.path`
  （product/policy/android-settings-forbidden-actions.json）不动。
- 历史文档（changes/、evidence/、docs 旧 ADR）中的旧路径引用不改。
- profileRevision 钉扎/revision 执法（PRF-005 范畴）。

## Decisions

1. 路径解析照抄 PRF-002 先例（commit 98fb0f6c）：`DefaultConfigRelativePath`
   改为 `product/tasks/<name>.yaml`，发现算法（装配目录向上 + AGENTS.md
   边界）不变——只换相对路径字面量。
2. `.dsh/profiles/` 目录随搬迁清空移除：该目录下已无其他文件
   （uniagent-prod.yaml 已在 PRF-002 出走），宿主绑定在 `.dsh/product/`。
3. docs/evidence 历史记录里的旧路径命中视为历史事实，不改写。

## Acceptance

1. 三个文件在 `product/tasks/`；`.dsh/profiles/` 旧路径消失。
2. grep `\.dsh/profiles/(settings-coverage|language-inspection|tool-runtime)`
   在 src/tests 零命中（docs/evidence 历史命中可接受）。
3. 语义零变：三个 yaml 内容 byte 级不变（git mv，无内容 diff）；
   env 覆盖变量名不变。
4. 全解 `dotnet test UniClaw.Kernel.slnx --no-restore` 全绿；
   src 变更后 `tools/scenario_certify.py --change PRF-008 --all` 重封；
   `tools/gen-open-changes.py` 再生 INDEX。

## Verification

| level | method | expected | actual | evidence |
|---|---|---|---|---|
| CONTRACT | 文件位置 | 三文件在 product/tasks/；`.dsh/profiles/` 消失 | PASS | git status 三 R 记录；`ls .dsh/profiles/` 目录已删 |
| CONTRACT | yaml 语义零变 | git mv 纯重命名，无内容 diff | PASS | `git status` R 记录（rename，无 M） |
| CONTRACT | 旧路径引用 | src/tests 零命中 | PASS | grep `\.dsh/profiles/(settings-coverage\|language-inspection\|tool-runtime)` src tests --include=*.cs 零命中 |
| DETERMINISTIC | 全解 `dotnet test UniClaw.Kernel.slnx --no-restore` | 全绿 | PASS 1475/1476；唯一失败为 DocsMetadataTests，针对任务开始前已存在的未跟踪文件 `docs/analysis/ui-automation-agent-gap-assessment-2026-10-08.md` 缺 Status/Authority 头（与本 change 无关，预存在） | 全解输出 |
| SCENARIO | `tools/scenario_certify.py --change PRF-008 --all` + `--check` | 0 违规，src 哈希重封 | PASS：20/28 块重封；--check PASS (28 files, 0 violations) | `tools/scenario_certify.py` 输出 |
| CONTRACT | 登记 | product/AGENTS.md tasks/ 行 + 职责基线 product/ 行补 tasks/ | PASS | 两文件 diff |
| INDEX | `tools/gen-open-changes.py` | INDEX 再生 | PASS（177 changes, 4 open） | `changes/INDEX.md` diff |

## Status log

- 2026-10-08 · IMPLEMENT · git mv 三文件 + 三个 loader 路径常量/注释 +
  测试注释 + product/AGENTS.md tasks/ 行 + 职责基线 product/ 行 +
  ResolvePolicyPath 锚定修正；本 state.md 落地。
- 2026-10-08 · IMPLEMENT → REVIEW → VERIFY · 首轮全解暴露 24 失败
  （product/product/policy 双拼），ResolvePolicyPath 修为最外层 AGENTS.md
  锚定后 Host.Tests 200/200 绿；scenario 20 块 PRF-008 重封，--check
  PASS(28,0)；INDEX 再生。VERIFY 交付，CLOSED 留 Leader。
  遗留（非本 change）：未跟踪 docs/analysis 新文件缺 metadata 头，
  Kernel.Tests DocsMetadataTests 1 失败。
