# PRF-007 — PRF-5：assembly 执法 + 初始化审计 envelope（主线收官）

lifecycle_state: closed · disposition: none · depth: standard · base: working-tree

## Intent（WHAT/WHY）

ADR-0041/设计文档收官片：产品 profile 获得工件装配引用与 fail-closed 执法；
一次 Product Run 能完整重建「当时装配了什么」（初始化审计 envelope）。

## Scope

- `UniAgentProfile` record：`Revision`（profileRevision 出栈）+ `ProfileAssembly`
  （promptManifest/safetyPolicy 引用：path+hash）。
- loader（UniAgentProfileYaml）assembly 执法：声明即校验——路径存在、hash
  格式合法；promptManifest=目录 digest **全量重算**（manifest.json+segments，
  与 tools/prompt-manifest-hash.py 同 canonical 规则）且 == 声明值 == prompt-hash.txt；
  safetyPolicy=文件字节 sha256 == 声明值。不符即拒载（bump 提示在错误消息）。
- `product/profiles/uniagent-prod.yaml`：assembly 块（两引用带真实 hash）+
  profileRevision 2。
- `InitializationEnvelope`（Host.Dsh）：会话首个 run 落
  `runs/<productSessionId>/initialization.json`——productSessionId/firstRunId/
  recordedAt/runtimeProfileRevision/protocolSchemaHash/productPromptRevision/
  safetyPolicyRevision/modelRoute{provider,model}/dshEndpoint；同会话后续 run
  不覆盖（会话内钉扎）。RuntimeHttpServer.StartRunAsync 接线。
- `.gitignore` 增 src/UniClaw.Host.Dsh/runs/（运行产物；评审副本走 evidence）。

## Out of Scope

- Program.cs 直跑路径的 envelope（消费方出现时接同一 Write）。
- memory 接入（deferred，无 buyer）。
- 任务 profile 第二批搬迁（独立 change）。

## Decisions

1. assembly 路径相对仓库根（自程序集位置解析，与默认发现同规则）。
2. 工件修订号从被引用工件读出（prompt manifest.json/policy json），envelope
   不重复声明——单一真相。
3. envelope 会话级一次性（first run wins），进程级 revision 钉扎（Q9）的会话投影。

## Acceptance

1. assembly 声明与工件不符（hash 篡改/文件缺失）→ 拒载用例绿。
2. repo profile revision=2 + assembly 加载成功。
3. envelope 五要素齐备、first-run-wins 用例绿。
4. 全解绿（scenario 源哈希随 src 变更经 certify --change PRF-007 重封）。

## Verification

| level | method | expected | actual | evidence |
|---|---|---|---|---|
| DETERMINISTIC | UniAgentProfileYamlTests | repo 装配加载 + 两类拒载 | PASS；25/25 | `tests/UniClaw.Agent.Tests/UniAgentProfileYamlTests.cs` |
| DETERMINISTIC | InitializationEnvelopeTests | 五要素 + first-run-wins | PASS；Host.Dsh.Tests 32/32 | `tests/UniClaw.Host.Dsh.Tests/InitializationEnvelopeTests.cs` |
| SCENARIO | 全解 + 重封 | 全绿 0 违规 | PASS 1476/1476；certify 20 块 --change PRF-007 | `dotnet test UniClaw.Kernel.slnx` |
| CONTRACT | README 登记 + gitignore | 新文件入册；runs/ 屏蔽 | PASS（Kernel.Tests 849 含 ownership） | `src/UniClaw.Host.Dsh/README.md`；`.gitignore` |

## Status log

- 2026-10-08 · UNDERSTAND → RESOLVE → PERSIST → IMPLEMENT → REVIEW →
  VERIFY → CLOSED · PRF 主线五片+role 补齐全部闭环。
