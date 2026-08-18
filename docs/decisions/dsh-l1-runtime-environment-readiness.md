# DSH L1 Runtime Environment Readiness — Record

> Status: DSH_L1_RUNTIME_ENVIRONMENT_READY（B2/B3 均 READY，真实 LLM seam + 全 transport smoke PASS）
> Date: 2026-08-17
> Prerequisites: VISION_DEPLOYMENT_IDENTITY_ADMISSION GRADUATED · B1 = READY
> 本 gate 零仓库代码变更（临时验证脚本已清理）；架构全冻结。

---

## 1. Canonical DSH 应用入口

- **包**：`@deepseek-ai/dsh`（apps/cli），bin = `dsh` → `lib/bin.js`（构建产物已存在）
- **启动**：`dsh`（交互 TUI/web profile）；`dsh --profile headless "<task>"`（一次性任务，可脚本化）
- **编程入口**：`@deepseek-ai/dsh-app-boot` `boot(name, configPath)`（真实 host，非交互）
- **包管理器**：pnpm workspace（仓库根 node_modules 已安装）
- **配置**：`~/.dsh/settings.yaml`（用户设置，热重载）+ profile `cordis.yml`/`cordis.patch.yml`（插件/服务组合）
- **服务组合**：`@deepseek-ai/dsh-base` bundle（cordis.patch.yml insert：llm、llm-deepseek、credentials、commands、session 等标准服务）

## 2. 本地 DSH 状态（重新核验，非旧观察）

| 项 | 状态 |
|---|---|
| DSH 构建产物 | ✅ `apps/cli/lib/bin.js` 存在；`dsh --version` = 0.1.0-rc.5 运行成功 |
| 依赖 | ✅ 仓库 node_modules 已安装；服务包 lib 产物齐全 |
| settings.yaml | ✅ `agent-default-model: deepseek-official / deepseek-v4-flash / reasoningEffort high` |
| 凭证 | ✅ `~/.dsh/.credentials.yaml` 的 `DEEPSEEK_API_KEY` **已设置（35 字符）**——先前"空凭证"观察为误判（正确机制 = 环境变量引用 + 托管凭据文档，适配器 per-request 解析） |
| ctx.llm | ✅ 真实 boot 中 `ctx.get('llm')` PRESENT（LlmRuntime 注册） |

## 3. 插件激活路径

- **生产挂载点**：profile 的 `cordis.patch.yml` 添加 dsh-plugin-uniclaw loader entry（DEMO 文档化方式；client 插件包挂 web 实例 modules）
- **验证**：真实 boot（cordis.yml 含 llm + llm-deepseek + credentials + commands + dsh-plugin-uniclaw，`assistance.consumer: llm`）→ **插件 ACTIVE、bridge 构建（consumer=llm）、DriverHost connected**
- 配置意图：`assistance.consumer = "llm"`（无 hidden deterministic fallback——resolveAssistanceBridge 显式分支）

## 4. LlmRuntime / ctx.llm 注册

- `dsh-llm`（`LlmRuntime extends Service`）由 base bundle insert；`dsh-llm-deepseek` 注册 `deepseek-official` route（provider-neutral `registerAdapter`）；`dsh-credentials-local` 提供凭证 seam
- 模型路由：`settings.yaml` / 插件 config `llm.provider/model`
- 错误行为：route/credential 缺失 → LlmRuntime 错误码（NO_ADAPTER / credential），**启动不需要凭证，仅调用时解析**
- **LlmAssistanceConsumer 零代码变更即可消费**（consumer 端口 + `ctx.get('llm')` seam 已就绪）

## 5–6. 凭证机制与所需凭证

- **canonical 机制**：环境变量引用（`DEEPSEEK_API_KEY`）经 `dsh-credentials-local` 托管（`.credentials.yaml` 永不 materialize 进进程环境；支持 env/.env 回退）
- **所需**：Provider `deepseek-official` · CredentialName `DEEPSEEK_API_KEY` · RequiredFields API key · ModelRoute `deepseek-official/deepseek-v4-flash` · **CredentialPresent = YES**（35 字符，不暴露值）
- 非 `MODEL_ROUTE_CONFIGURATION_GAP`

## 7. 启动配置模型

最小可复现 profile：app entry（dsh CLI）→ 插件启用（cordis.patch.yml 挂 uniclaw）→ DriverHost endpoint（127.0.0.1:port）→ `assistance.consumer=llm` + `llm.provider/model` → credential 经 canonical credentials 机制。全部仓库原生配置，无"手动 export 一次 shell"依赖。

## 8–9. B2 / B3 就绪

- **B2 = READY**：真实 DSH boot 成功（ctx.llm PRESENT / 插件 ACTIVE / DriverHost connected / 无 fatal 启动错误）；既有 read-only/run.start 命令可注册
- **B3 = READY**：凭证已存在于 canonical 机制（无需用户再供）——非 `USER_SECRET_REQUIRED`

## 10–11. 真实 seam + 全 transport smoke（PASS）

- **REAL_LLM_SEAM_SMOKE = PASS**：真实 `ctx.llm.stream`（deepseek-official/deepseek-v4-flash，1979ms）返回精确结构化输出 `{"recommendation":"re-observe","reason":"probe"}`
- **FULL L1 TRANSPORT SMOKE = PASS**（真实模型，受控 AssistanceRequest）：
  - .NET DriverHost 注册 Contradicted pending（worldVersion=7）→ 真实 DSH bridge poll `assistance.pending` → LlmAssistanceConsumer → 真实 LlmRuntime → 结构化 advice → `assistance.resolve`（echo/worldVersion 匹配）→ registry 消费（PENDING_AFTER=0）→ 请求方收 `ADVICE_OK recommendation=re-observe`（reason 语义合理：belief contradicted、观测太稀疏、建议再观测）
  - 未记录凭证/完整 prompt/CoT

## 12. 架构冻结

零改动：Runtime/Agent/Vision/IAssistanceProvider/AssistanceWireProvider/AssistanceBridge 权威/LlmAssistanceConsumer 词汇；零新增 General Agent/Subagent/tools/L2/wire 方法/Runtime 事件/模型抽象。本 gate 零仓库代码变更。

## 13. 环境 blocker 最终状态

```
B1_VISION_ENVIRONMENT          = READY
B2_DSH_APPLICATION_INSTANCE    = READY
B3_MODEL_CREDENTIALS           = READY
```

## 14. 下一步

**`REOPEN_L1_ASSISTANCE_REAL_WORLD_VALIDATION`**——真实设备 + 真实感知 + 真实 DSH/模型全链 L1 验证（对照实验 S1–S7、L0 控制、每咨询指标、失败分类、策略调优）。该 gate 将证明真实模型 advice 是否把 fail-closed run 转为真实继续/完成（RecoveryConversionRate / CompletionLift）。
