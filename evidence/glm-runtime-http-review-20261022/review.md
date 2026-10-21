# UniClaw Runtime HTTP 模块只读职责审查（RuntimeHttpServer.cs）

> Date: 2026-10-22
> Executor: GLM runtime-http review worker（DSH 会话）
> Mode: 只读。未修改任何产品源码/测试/配置/Change 状态；未运行设备、模型或服务；未提交推送。
> 对象：`src/UniClaw.Host.Dsh/RuntimeHttpServer.cs`（729 行）为主，装配入口 `Program.cs`、`RuntimeToolHost.cs` 作对照。
> 权威锚点：产品架构基线（§7 Uni Kernel、§21.2 realization replaceability、§24 不变量 42/43 族）、`src/UniClaw.Host.Dsh/README.md`（"本目录只做宿主映射"）、ADR-0022/0031/0039/0044/0045、RUNTIME-IDENTITY-001、PRF-002/005、PNL-008/010/011/012、CAP-006。
> 判定纪律：拆分依据=职责混杂与缝的真实消费者，**不是**类大小或行数；区分【已证实】与【可选】。

## 1. 职责与依赖清单

RuntimeHttpServer 当前承担 8 项职责（行号为 2026-10-22 工作树实况）：

| # | 职责 | 证据行 |
|---|---|---|
| R1 | HTTP 路由与端点注册（health/tasks/sessions/runs×4/recover/tools×2） | 68-84 |
| R2 | 请求解析（两种范式）+ 统一响应/错误 envelope | 104-112, 123-131, 143-155, 600-619, 703-712 |
| R3 | Task/Session/Run 身份绑定 + activation 守卫（Owner/幂等/拒绝语义） | 104-139, 148-198, 212-221 |
| R4 | Run 启动编排：store 创建 → BindRun/Finalize → 202 | 169-221, 281 |
| R5 | DSH 装配：profile/bindings、模型 resolve、envelope、peer/channel/agent、slow 传输 | 64-67, 225-235, 273, 540-598 |
| R6 | 运行驱动（后台）：adb 场景铺底 + HostRunner.RunOnce + 场景组合 | 280, 346-391 |
| R7 | 产物终结：metadata 投影、事件导出、报告调用、artifacts、兜底日志 | 393-421, 600-701 |
| R8 | 工具面端点 + ToolHost 懒装配 + 两张错误映射表 | 448-529 |

依赖清单（箭头=调用方向）：

```text
RuntimeHttpServer
├── UniClaw.Host.Runtime: RuntimeRunStore(投影/事件) · TaskInstanceStore ·
│   ProductSessionStore · ActivationRecordStore   ← 四个 jsonl store，构造于 46-52
├── UniClaw.Host: HostRunner(RunOnce/LaunchContext/HostOptions) · LivePerception.LiveAssets
├── UniClaw.Host.Capability: RuntimeIntegrationCapabilityComposition (367-368)
├── UniClaw.Host.SettingsCoverage: SettingsCoverageConfig (366)
├── UniClaw.Agent.Dsh: DshModelManagement · UniagentDshBindings(Yaml) ·
│   DshOpenedHttpPeer · DshOpenedDecisionChannel · DshAgentAdapter · ProductHandshake · ModelConfiguration
├── UniClaw.Agent.Profile: UniAgentProfile(Yaml)
├── UniClaw.Kernel: Runtime(RunDriveStatus) · Capability(LanguageInspectionSelection/AgentDecision)
├── 本目录: InitializationEnvelope · RuntimeToolHost(+RuntimeToolConfig)
└── 环境: adb 进程 · 文件系统布局(RepoRoot 四级 Parent) · env(UNICLAW_UNIAGENT_PROD_MODEL) · tool-registry.yaml
```

## 2. 五条调用链追踪（任务指定）

**C1 HTTP 解析与错误映射**：两条解析范式——身份端点手解 `JsonElement?`（104-112/123-131），run 端点走 typed `LaunchRequest` record + `Required/HasReference/JsonLabel/RequestedDevice` 助手（143-155, 276-278, 703-712）。错误统一走 `Error(status, code, message, retryable)`（612-619）；cursor 的 `FormatException→400 invalid-cursor`（310-313, 336-339）；工具错误的 code 靠 `InvalidOperationException.Message` 字符串前缀反解（520-527, 575-586, 486）。

**C2 Task/Session/Run 身份绑定**：POST /tasks 由 TaskInstanceStore 铸 taskInstanceId（幂等键=idempotencyKey，104-117）→ POST /sessions 验 task 存在后由 ProductSessionStore 铸 1:1 session 并回写 BindSession（123-139）→ POST /runs 交叉验证双向绑定（157-165），activationKey=(acceptedContractViewRef, contractGeneration)，经 Reserve→冲突族→Finalize（173-221）。RunId 永不由调用方提供（167-171 注释即契约）。

**C3 任务启动**：校验（143-165）→ Reserve/守卫（173-198）→ store.Create（200-211）→ BindRun+Finalize（212-221）→ 模型 resolve（225-229）→ envelope（231-232）→ device 解析（233）→ Attach（234-256，失败→transition failed+503）→ transition running+HostSessionRef（258-266）→ LaunchContext+DshAgentAdapter（267-273）→ `Task.Run(RunBackground)` fire-and-forget（280）→ 202+Location（281）。

**C4 DSH 装配**：PRF-002 分轨加载 profile+bindings（64-67）→ CAP-006 经产品缝 resolve 模型（225-229；重复于 545-549）→ PRF-005 envelope（231-232）→ peer（构造零网络副作用）+channel(30s attach)+agent(110s turn)（234-235, 273）→ 诊断工具复用 slow 端点 TransportAsync（591-598）。对照：Program.cs 同一装配链的完整版（121-182）。

**C5 产物终结**：RunBackground 内 RunOnce 返回后（386-391 状态/结局映射）→ WriteRunMetadata（653-685，含 HostSessionRef 回读）→ ExportRuntimeEvents（687-701，store 仍是 authority）→ ToolHost().InvokeAsync("run-report")（402）→ BuildArtifacts（621-633）→ 终态 transition（416-421）；产物链失败只落 report-generation.log、**不改变终态**（393-414 注释+try/catch）；finally 统一 Revoke/Dispose（431-437）。

## 3. 已证实的职责混杂点（附代码证据）

**P1 场景编排混入宿主 adapter（R6）【已证实】**
350-351 `RunAdb(... "am","start","-S","-a","android.settings.WIFI_SETTINGS") + Task.Delay(1500)`、373 `SettingsTraversal = true`、379-384 `"wifi-settings"` + RepoRoot() 拼 perception venv。README 自declared "本目录只做宿主映射"；基线 §21.2 要求 realization 可替换不携带语义。同构复制品在 Program.cs 184-185（SETTINGS 页 + 2000ms）——同一场景铺底逻辑在两条入口各自硬编码、参数互异，证明它是共享的产品侧关注点而非 DSH 特有映射。且该设备动作发生在任何 Effect Boundary 之外（基线 §7"保证所有 external effects exclusively pass through Effect Boundary"约束的是 Kernel 内执行环；adapter 预铺底是否豁免属 owner 裁决，本文只登记证据）。

**P2 错误分类依赖异常消息字符串【已证实，缝已半存在】**
RuntimeToolHost 全部执法路径抛 `InvalidOperationException("code: message")`（RuntimeToolHost.cs 125-133, 182-200, 216-227, 261, 279-285）；HTTP 层用 `Split(':',2)[0]`+前缀匹配反解（520-527, 486, 575-586）。两张 switch 映射表已经漂移（`diagnosis-timeout→504` 只在 579-586 存在）。typed 形状 `InvokeError(Code,Message)`（RuntimeToolHost.cs 42-47）**已定义但没有任何抛出路径使用它**——缝的消费者已就位，只差把 throw 换成 typed 异常携带 code。

**P3 模型解析装配重复（R5）【已证实】**
225-229 ≡ 545-549：同一 `FromBindings(_bindings,_agentProfile,env)+ResolveDshModel(AgentDecision)` 块（含 env var 名字符串）出现两次；第三处变体在 Program.cs 126-130。

**P4 attach 成功后至 Task.Run 前存在无防护区间（R4×R5 资源生命周期）【已证实·静态】**
try 只覆盖 AttachAsync（237-247）；拒绝分支覆盖 249-256。258-280 之间 `Transition`(258)/`LaunchContext`(267)/`new DshAgentAdapter`(273) 任一抛出：已 attach 的 DSH 会话不 revoke、channel/peer 泄漏、响应走 ASP.NET 默认 500（不经 Error envelope）、run 停在 created 态而 activation 已 Finalize（215）。静态路径可证；公开接口无法注入故障复现（见验收用例 N-11 说明）。

**P5 身份严格度不一致（R3）【已证实·裁决权在 owner】**
/tasks 强制 idempotencyKey（113-114 缺失→400）；/runs 却对缺失的 launchId/idempotency/correlation 静默铸造 `launch-{Guid}`/`idem-{Guid}`（169-171）——省键调用方获得零重放保护，而 RecoverAsync（440-446）恰以这两键查找。两端口身份纪律不同，是否有意兼容旧 web launch 流，须 owner 判断。

**P6 同项目内 RepoRoot/RunAdb 双实现且健壮性互异【已证实】**
RuntimeHttpServer.RepoRoot()（714）= `AppContext.BaseDirectory` 四级 Parent，无标记校验、发布布局即错；Program.cs RepoRoot()（409-416）= 自 CurrentDirectory 向上找 AGENTS.md+UniClaw.Kernel.slnx，带显式报错。RunAdb 同理：Program.cs 版（418-446）处理超时（Kill+typed 报错）；RuntimeHttpServer 版（718-728）丢弃 `WaitForExit(10000)` 返回值，超时后读 `ExitCode` 抛未分类异常。消费点：RepoRoot→382-384 与 453；RunAdb→350。

**P7 身份/激活守卫与传输编排内联交织（R3×R4×R5）【已证实·混杂】**
StartRunAsync 单方法顺序承担：字段校验→身份交叉验证→activation 状态机（reserved/blocked/failed/finalized 四分支+replay）→store 创建→绑定回写→模型/设备/传输装配→后台发射。守卫语义（Owner/幂等/拒绝的 409 族）与 DSH 传输装配（peer/channel）无共享状态却互为前后缀，测试任一半都必须 boot 整个 HTTP+DSH 栈。

## 4. 可选建议（不构成拆分依据）

- O1 ListRunsAsync 297-298：revision/observedAt 是页内 `Max`，空页返回 0/now——页语义≠store 语义，建议在响应注释/文档言明，不改行为。
- O2 GetEventsAsync 340-343 KeyNotFoundException 兜底与 320-322 前置存在性检查冗余；保留无害。
- O3 RuntimeToolHost.InvokeAsync 155：async 方法内同步 `WaitForExit(120_000)` 占用线程池线程；可 `await WaitForExitAsync`。行为不变，低优先。
- O4 RunBackground 413 `catch { }` 吞日志写失败——注释已声明意图（不改终态），保留。
- O5 `_toolHost` 懒加载字段（448-460）在 GET 内抛构造异常再由 handler 捕获；消费者单一，维持现状可接受。

## 5. 最小内部拆分方案（全部 internal，不改公开 HTTP 形状，不新增插件框架/第二驱动）

按第 2 节五条链切缝；每条缝列真实消费者（≥1 调用点+测试面），满足"两适配器才成真缝"或"内部缝仅供自身测试"的纪律：

| 缝 | 形状 | 承接 | 消费者 | 消除的混杂 |
|---|---|---|---|---|
| S1 `RuntimeHttpContract`（internal static） | Error/Response/schema 常量 + Required/HasReference/JsonLabel/RequestedDevice + cursor 映射 | R2 | 全部端点（≥6 调用点） | 协议知识从 8 处散点归一 |
| S2 `ActivationGate`（internal sealed） | 持三 identity store；CreateTask/CreateSession/GuardAndReserve 三入口，返回「冲突结果 \| 就绪激活」 | R3 | 3 个 POST 端点 | P7：身份/幂等/拒绝语义可脱离 HTTP+DSH 测试；**Reserve→Fail 的有限副作用原样保留** |
| S3 `DshRunAssembly`（internal sealed） | ResolveAgentDecisionModel + envelope 写 + peer/channel/agent 构造 + Attach（含拒绝映射） | R5 | StartRunAsync、InvokeProcedureToolAsync（模型解析两处=真实第二消费者） | P3 重复；P4 的资源区间收敛到单一 try 边界内 |
| S4 `RunArtifactFinalizer`（internal sealed） | WriteRunMetadata/ExportRuntimeEvents/BuildArtifacts/report 调用/兜底日志 | R7 | RunBackground | "失败不改终态"不变式集中一处可测 |
| S5 RunBackground 保留为编排壳 | 状态/结局映射（386-391）+ finally 清理（431-437） | R4/R6 | — | P1 的场景常量（WIFI_SETTINGS/wifi-settings/SettingsTraversal）上移出 adapter：候选载体=既有本地 profile 机制（product/tasks/*.yaml，先例 RuntimeToolConfig）或 launch 请求字段——**由 owner 裁决，本审查不预造抽象** |

配套微修（不属拆分）：P2 将 RuntimeToolHost 抛出改为携带 `InvokeError.Code` 的 typed 异常（复用已存在的 InvokeError record），HTTP 层删两张字符串映射表；P6 统一采用 Program.cs 的标记法 RepoRoot 与超时安全 RunAdb（同项目内已存在，零新抽象）。

**明确反约束**：不建 IRuntimePlugin/IHostLayer 类通用框架；不引入第二种运行驱动实现；S1-S4 均为 internal 且有当日即可指认的调用点；不改任何端点 URL/状态码/错误码/envelope 字段。

## 6. 逐文件修改建议

| 文件 | 现状 | 建议 | 优先级 | 性质 |
|---|---|---|---|---|
| RuntimeHttpServer.cs | 8 职责混载；P1-P7 证据所在 | 按 S1-S4 提取 internal 缝；补 258-280 防护区间（try→revoke/dispose+transition failed+Error envelope）；P5 待 owner 裁决后再动 | 高 | 已证实 |
| RuntimeToolHost.cs | 执法完善但错误靠消息前缀；InvokeError record 无人用 | throw 改 typed（code 走异常属性而非字符串）；O3 可选 | 高（P2）/低（O3） | 已证实/可选 |
| Program.cs | runtime-http 分支薄（78-90）；RepoRoot/RunAdb 为健壮版 | 健壮实现下沉为 internal 共享（供 RuntimeHttpServer 复用，消 P6）；CLI 主体的 capability-facts 双段重复（301-326 vs 348-369）不在本次范围，仅登记 | 中 | 已证实（P6 部分）/登记 |
| InitializationEnvelope.cs / RuntimeToolConfig.cs / FileDecisionConsult.cs | 单一职责、无混杂证据（本次通读头部与调用点） | 不动 | — | — |

## 7. 语义保真清单（任何拆分必须逐条保持）

1. 身份 Owner：RunId/TaskInstanceId/ProductSessionId 一律 Runtime 铸造；调用方 RunId 仅作忽略输入（167-171）。
2. 幂等：/tasks 按 idempotencyKey、/sessions 按 taskInstance 1:1、/runs finalized→202 idempotent=true 重放（191-198）。
3. 拒绝族：activation-key-conflict / product-session-already-bound / activation-closed / activation-reserved(retryable) / binding-mismatch / binding-not-found 的码、状态码、retryable 位逐一不变。
4. 零副作用边界：400 族发生任何 store 写之前；409 族允许的有限 activation 写（Reserve 174、Fail 182/219）为**既有语义**，保持原写序。
5. 产物终结失败不改 run 终态（393-414）。
