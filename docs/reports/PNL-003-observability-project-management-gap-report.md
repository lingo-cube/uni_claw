# PNL-003 观测数据与项目管理 Gap Report

审计范围：`uni-harness` 当前工作树；只读检查 UniClaw Workspace 的 DSH host projection、浏览器 capability seam、任务仓库、运行产物 fixture 与现有 PNL-002 约束。本文是下一轮 change 的输入，不改变当前实现，也不引入新的任务系统或 ADR。

## 结论

当前工作区已经具备可验收的只读体验，但它仍是“运行后发现 + 展示”的 read model，不是完整的观测目录。最需要补齐的不是把字段继续堆进页面，而是建立一份稳定的观测契约：每条记录要能回答“它属于哪个 project / test set / task instance / session / run，谁产生，谁权威，何时发生，如何追到原始产物”。

当前最明显的断点有四个：

1. UniClaw Trace 已按 OTel 形状投影 Span 的核心字段，但 trace context、span 数量和 detail 仍以 `trc/0.1` 运行产物为前提；没有统一的 `runId` / `environment` / `scope` / `schemaVersion` 外层 envelope，也没有 pagination 或完整 trace read model。
2. DSH Trace 是 session event 的轻量摘要，保留 seq、时间、事件类型、摘要和少量原始引用；它不是 DSH 原始事件的完整 projection，Span-like parent、duration、attributes、links 等字段不存在，不能与 UniClaw Span 做同粒度比较。
3. Metadata 只在真实运行产物存在时追加设备、Android API、窗口尺寸、endpoint、taskSet、real、产物摘要；session header 本身缺少 host/provider/version、device identity、OS/build、runtime/run identity、project/test-set provenance 等字段。没有证据时这些字段正确地为空，但当前契约没有把“缺失”和“未采集”区分开。
4. project 当前由 `projectRef.path` 或运行产物 `metadata.workspace` 临时聚合；test set 只有 metadata 上的 `taskSet` 字符串，没有实体、版本、成员或生命周期。Task definition、Task Instance、DSH Session 的关系已存在，但 project/test set 的创建、重命名、归档、归属迁移和 provenance 没有管理面。

## 1. Trace 字段级矩阵

判定口径：OTel 的 Span 是一次操作，通常具备 trace/span/parent identity、开始结束时间、attributes、events、links 和 status；Event 是 Span 内带时间的命名记录；Link 表达跨 trace/span 的关联。下表按“当前 workspace 能否稳定展示”和“下一轮是否应成为契约字段”判断，而不是要求直接引入 OTel SDK。

| 字段或能力 | OTel 常见位置 | UniClaw 当前投影 | DSH 当前投影 | 权威来源 | 优先级 / 建议 |
|---|---|---|---|---|---|
| `traceId` | Span/Trace context | 有：来自 `observed.trace.traceId` | 无统一字段 | UniClaw RunTrace artifact | P0；纳入统一 envelope |
| `spanId` | Span | 有 | 无 | UniClaw RunTrace artifact | P0 |
| `parentSpanId` | Span | 有，可为空 | 无 | UniClaw RunTrace artifact | P0 |
| `spanKind` | Span | 有，缺失时为空 | 无 | UniClaw RunTrace artifact | P1 |
| name / operation | Span name | 有：`spanDefinitionId` / `spanId` | 以 event `type` 代替 | 产生该记录的 runtime / host | P0；统一命名为 operation/name |
| `startTime`, `endTime` | Span | 有，缺失时为空 | 只有 event timestamp | 产生该记录的 runtime / host | P0 |
| `duration` | Span derived | 有 `durationMs`，不计算伪值 | 无 | UniClaw RunTrace artifact | P1；允许由 start/end 派生 |
| status | Span | 有 raw status；另有 `structuralOutcome` | event type / 文本中可能出现结果 | UniClaw Runtime；DSH 只拥有 host event outcome | P0；保留两种语义，不互相覆盖 |
| attributes | Span | 有 object，未统一 key/type 约束 | 没有稳定容器 | 各产生方；workspace 只透传 | P1 |
| resource | Span/SDK | 有 object，可为空 | 无 | 产生 trace 的 runtime | P1；补 service.name/version/environment |
| events | Span event list | 有 eventId、timestamp、reasonCode、attributes、references | 每个 session event 是单独记录，缺少 span association | UniClaw Runtime / DSH Session 各自 | P0；需要明确 `parentSpanId` 或 correlation |
| links | Span link list | 有数组，内容未归一化 | 无 | UniClaw Runtime | P1 |
| trace state / baggage | Context | 无 | 无 | Host/runtime transport | P2；没有 buyer 前不阻塞 |
| run identity | 运行上下文 | context 有 `runId`，span 本身未必带 | 无稳定字段 | UniClaw Runtime / artifact metadata | P0；与 productSessionId、dshSessionId 分开 |
| capture sequence | 采集顺序 | 有 `captureSequence` | 有 `seq` | 各 recorder | P1；不当作业务时间 |
| schema/version | Artifact envelope | 仅文件 `trc/0.1` 被识别 | capability contract v1，event 内无 trace schema | 文件/协议 owner | P0 |
| source / authority | Workspace projection | UI 加 `source=uniclaw` | UI 加 `source=dsh` | workspace projection metadata | P0；补显式 authority/provenance |
| pagination/truncation | Read API | 固定最多 200 spans，返回 `uniclawTraceTruncated` | 无统一分页 | Workspace query contract | P0；必须有 total/cursor/snapshot |
| raw record reference | Detail | UniClaw node 可查看；trace.json 可读 | session event 原始内容依赖 DSH session query | artifact/session owner | P1 |

当前 `traceProjection()` 的优点是没有伪造时间和状态，且 references/events/attributes/resource/links 已保留；缺口是读模型没有把“观测范围、快照、截断、schema、权威”提升到同一层。下一轮应先冻结这些 envelope 字段，再决定是否接 OTel SDK 或 exporter。

## 2. Metadata 与真实 fixture 审计

当前代码路径：`sessionDetailCore()` 先从 DSH session header 建立基础 metadata；如果找到 `metadata.json`，再追加设备与运行产物摘要。真实 fixture `evidence/pnl003-real-task-android-settings-20261003/run-20261003-075727-844/metadata.json` 能证明部分字段确实存在，但它不是所有 session 的稳定契约。

| 维度 | 当前可见字段 | 当前缺口 | 建议权威 |
|---|---|---|---|
| session | `sessionId`, `createdAt`, `live`, `persisted`, `cwd`, `agentPreset` | `lastActiveAt` 恒为空；缺 `session status/version/host` | DSH session header |
| product correlation | `productSessionId`、instance/task 关联 | 未统一展示 `taskInstanceId`、correlation method、correlation observedAt | UniClaw task repository + explicit resolver |
| project | `projectRef.path` 或 artifact `workspace` | 无稳定 project id/name/version、owner、source、lifecycle | Project catalog（待建） |
| test set | artifact `taskSet` | 只是字符串；无 test-set id/version、成员、scenario/fixture revision | Test-set catalog（待建） |
| task | `taskId/title/requirement/status/createdAt/projectRef` | 无 task definition revision、labels、test-set membership | Task repository |
| instance | `instanceId/sessionId/status/startedAt/endedAt` | 无 run id、attempt、termination reason、host session lifecycle | Task instance repository + DSH session |
| device | `device`, `androidApi`, `wmSize`, `real` | 缺设备类型/serial、OS/build、locale、orientation、density、transport、capability snapshot | Device/environment adapter |
| runtime | `agentPreset`, `dshEndpoint`, `productModel`, `deliveredEffects`, `consultations` | 缺 UniClaw runtime version/build、DSH version/build、provider/model revision、config revision、environment | Runtime/Host metadata producers |
| execution | artifact `outcome/status`, `facts`, `coverage-steps` 通过 runStages 使用 | 缺明确 `runId`、attempt、started/ended/duration、result kind、verification revision | Run/Outcome authority；workspace 只投影 |
| artifact | `workspace`, `runDir`, file refs, `productSessionId`, title | 缺 artifact id/hash/schema/createdAt/retention/provenance；绝对路径不适合跨 host | Artifact source / content-addressed store |
| provenance | UI `source`、hostSessionRef | 缺 producer、authority、source ref、observedAt、snapshot id | Each producer + workspace envelope |

Fixture 事实：`metadata.json` 能提供 `dshSessionId`、`productSessionId`、`productSessionTitle`、`workspace`、`productModel`、`outcome`、`status`、`device`、`androidApi`、`wmSize`、`taskSet`、`runDir` 等；但 `artifacts.js` 只用 `dshSessionId` 做显式关联，目录名和 transcript 不参与绑定。这是正确的安全边界，但也意味着没有 `dshSessionId` 的产物不会自动进入工作区，且当前 UI 无法说明“未发现关联”与“尚未采集”的区别。

## 3. Project / Test Set / Task Instance 管理审计

当前实体关系可表达为：

```text
ProjectRef(path/workspaceId)
  └─ Task definition(taskId, title, requirement, status)
      └─ Task instance(instanceId)
          └─ DSH session(sessionId)
              └─ optional ProductSession(productSessionId)
                  └─ run artifacts / traces / evidence
```

现状与边界：

- Task definition 和 instance 由 `TaskRepository` 持久化；状态只有 task 的 `draft/active/archived` 和 instance 的 `starting/active/ended/failed`。instantiate 只允许 active task，并在创建 DSH session 后追加 instance。
- Project 不是独立持久实体。`workspaceCore()` 用 task 的 `projectRef.path` 作为 projectId；发现的 artifact 则用 `artifact:${metadata.workspace}` 临时建组。路径重命名会产生新 project，两个来源也可能产生同名但不同 id 的 project。
- Test set 没有实体或 API；`metadata.taskSet` 只在 detail metadata 中透传，不能反查成员或从 project 导航到测试集。
- 没有 project/test-set 的创建、编辑、归档、恢复、版本、权限或迁移管理。PNL-002 明确将任务编辑器、筛选、搜索和新的持久化层排除在外。
- DSH owns session storage and host lifecycle/events. UniClaw owns ProductSession/task/instance semantics and product trace/evidence authority. Workspace should remain a read-only projection; project/test-set catalog ownership still needs a decision. Artifact scan is an observation adapter, not a source of Product completion truth.

建议冻结的边界：Project/TestSet catalog 由 UniClaw workspace domain 持有最小 identity/membership/lifecycle；DSH 只提供 host workspace/session references 与 events；UniClaw Runtime 仍是 run/outcome/evidence 语义的 authority；workspace 合并显示时保留每个字段的 `source` 和 `authority`。这避免把 DSH workspace path 误当成 Product Project，也避免从 artifact metadata 反推测试集真相。

## 4. 下一轮最小可执行 Change

建议 Change：`PNL-004 Observability Contract and Catalog Read Model`，先做只读契约与对比，不做完整项目编辑器。

最小交付：

1. 增加版本化的 `ObservationEnvelope` read model，至少包含 `snapshotId/observedAt/schemaVersion/source/authority/productSessionId/taskInstanceId/dshSessionId/runId`、trace truncation/cursor；Trace Span/Event/Link 使用统一 typed fields，仍允许 source-specific attributes。
2. 对真实 fixture、DSH mock session、无 artifact session 各生成字段级 comparison fixture，明确 `present / absent / not-applicable / unavailable`，不把空值默认为事实。
3. 增加只读 Project/TestSet catalog projection：project identity、testSet identity/version、task definition membership、instance count、source/provenance；不新增写入 API，先从现有 task repository 和显式 artifact metadata 派生。
4. Metadata 增加设备与运行时字段的 typed groups，同时保留旧扁平字段兼容投影；所有新增字段必须带 authority/source 或明确 owner。

验收标准：

- 同一个真实 session 可以同时定位 `project → testSet(若有) → task → instance → productSession → dshSession → run`，任何缺失关系显示明确 gap，不静默拼接。
- Trace comparison 能逐字段报告 DSH / UniClaw / OTel shape 的 present/absent/source/priority，并证明前 200 spans 的 truncation、total 与 cursor 一致。
- 至少一条真实 Android fixture 展示设备 identity、OS/API、窗口/环境、DSH 与 UniClaw runtime provenance；不存在这些证据的 fixture 不被填充。
- Project/TestSet projection 对路径同名、artifact workspace 同名、未关联 artifact 三种情况产生稳定且可解释的 identity/correlation status。
- 现有 `TaskQuery/SessionQuery/TraceQuery/EvidenceQuery/DetailQuery` 保持兼容；Web/DSH 合同测试、真实 3083 浏览器读取与 `git diff --check` 通过。

建议证据路径：

- 规范与边界：`changes/PNL-002/state.md`、`changes/PNL-002/spec.md`、`docs/architecture/uniagent-realization-baseline-v0.1.md`
- Trace 实现：`dsh/uniclaw-task-workbench/src/index.js` 的 `traceProjection()`、`sessionDetailCore()`、`dshEventProjection()`
- Capability seam：`dsh/uniclaw-task-workbench/src/workspace-capabilities.js`、`schemas/workspace/capability.schema.json`
- 项目/实例存储：`dsh/uniclaw-task-workbench/src/repository.js`、`workspaceCore()`
- Artifact correlation：`dsh/uniclaw-task-workbench/src/artifacts.js`
- 真实数据：`evidence/pnl003-real-task-android-settings-20261003/run-20261003-075727-844/metadata.json`、`trace.json`、`facts.json`
- 验证目标：`dsh/uniclaw-task-workbench/tests/workbench.test.mjs`、`web/uniclaw-workspace/tests/`

## 未决 Human Gate

1. **Project/TestSet owner**：是否由 UniClaw workspace 持有 Project/TestSet catalog，还是由外部测试管理系统持有、workspace 只读取引用？这决定下一轮是否可以增加本地持久化。
2. **Run identity**：`runId` 是否由 UniClaw Runtime canonical 产生并要求所有 DSH/artifact 通过显式映射接入，还是允许 DSH session 作为缺省 run boundary？
3. **Trace completeness**：前端默认展示是否只承诺 canonical UniClaw Trace + DSH event projection，还是要为 DSH events 建立完整 span/event/link 归一化模型？后者会扩大 DSH adapter 范围。
4. **Device authority**：设备信息由真实 Host/device adapter 提供，还是允许测试集 metadata 提供静态声明？两者必须区分 `observed` 与 `declared`。
5. **Retention and privacy**：artifact path、设备 serial、endpoint、原始 event payload 的展示和保留边界尚未冻结。
