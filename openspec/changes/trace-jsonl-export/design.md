## Context

C-7 约束 (P3 C-bucket backlog) 要求 trace 数据可持久化到文件, 供 Python 仪表板消费。当前 Observability 层只有 InMemoryTraceStorage — 全在内存, 无文件输出。Python 有完整 FileStorage (JSONL + session.json + 后台写线程)。C# 需对应实现实现工具链互操作。

详细设计文档已存在于 `docs/refactor/2026-07-20-trace-filestorage-jsonl-design.md`, 本 design.md 提取关键决策并标注 OpenSpec D-id。

现有 trace 体系:
- ITraceStorage: 13 sync 方法 (3 lifecycle + 5 write + 5 read + 1 export)
- InMemoryTraceStorage: flat list + incremental Dictionary index (GetByNodeId/GetBySpanType off-interface per ISP)
- InMemoryTraceService: 注入 InMemoryTraceStorage (concrete), 利用 index 方法做 query
- ITraceRecorder: async wrapper (D-22)
- 5 flat record types + TraceContext + TraceSession

## Goals / Non-Goals

**Goals:**
- 实现 FileTraceStorage (JSONL 写 + session.json 元数据)
- 实现 IFileProvider 抽象解耦 System.IO
- 保持 Core classlib 不直接依赖 System.IO (PhysicalFileProvider 在 File/ 子目录, 通过 IFileProvider 注入)
- JSONL 格式兼容 Python dashboard 消费 (record_type 鉴别器)
- ITraceStorage.Export 方法正式化 (之前 InMemoryTraceStorage 内部有, 现需确认在接口上)
- Observability 目录重组 (InMemory/, File/ 子目录)

**Non-Goals:**
- Python TraceNode hierarchy 兼容 (A-7 已移除)
- DB/S3 storage backend
- IAsyncTraceStorage (Phase 3 roadmap)
- TraceContext 字段扩展 (VisitSpanId + ParentSpanId)
- Dashboard/visualization in Core classlib (A-10)
- 后台写线程/缓冲队列 (Python 有, C# ITraceStorage sync 不需要)

## Decisions

### D-91: IFileProvider 6 方法定义 — sync, YAGNI

**选择**: 6 sync 方法 (EnsureDirectory, AppendLine, ReadAllText, ReadAllLines, FileExists, DirectoryExists)
**替代方案**:
- A: 6 sync 方法 — 覆盖 FileTraceStorage 全部 I/O 需求, 与 D-22 ITraceStorage sync-first 一致
- B: async IFileProvider — 与 D-22 违背, ITraceStorage 已 sync-first, I/O 也应 sync
- C: 更多方法 (Delete, Copy, Move) — YAGNI, trace storage 不需要文件管理

**理由**: A 最简洁。Trace write 是 append-only (JSONL line append), 不需要文件管理操作。Sync 与 ITraceStorage 一致, async 层由 ITraceRecorder 处理。

### D-92: JSONL line format — record_type 鉴别器 + flat JSON

**选择**: 每行独立 JSON 对象, `record_type` 字段作类型鉴别器 (execution/state_transition/error/page_transition/ai_call)
**替代方案**:
- A: JSONL + record_type 鉴别器 — Python dashboard 可按 record_type dispatch, 无需 C# 特有 schema
- B: 分文件 (execution.jsonl, transition.jsonl, ...) — 过多文件, I/O 分散, 不利于 Python 一行行消费
- C: 单 JSON 文件 (array format) — 大 trace 无法 append, 必须全量重写

**理由**: A 是 Python 现有格式标准。Append-only 适合 trace write, 无需全量重写。record_type 鉴别器让 Python dashboard 一行行 parse 并 dispatch。

### D-93: Error handling — throw on write failure, not log-and-continue

**选择**: Write 方法 throw IOException (propagate 到 caller)
**替代方案**:
- A: Throw — ITraceStorage sync, caller 期待 success/fail, silent discard 破坏 engine↔trace 一致性
- B: Log-and-continue (Python 风格) — sync 上下文无 queue buffer, 无法异步重试; silent discard 比 crash 更危险

**理由**: A 正确。Python FileStorage 有后台 queue 可以缓冲, C# ITraceStorage 是直写, 无缓冲层。Throw 让上层决定 (retry, degrade to InMemory, terminate traversal)。

### D-94: Index methods — query-time computation, not persistent storage

**选择**: GetByNodeId/GetBySpanType 每次 call 时 ReadAllLines → deserialize → filter → build temp Dictionary → 返回 → discard
**替代方案**:
- A: Query-time computation — 无持久 index, 与 InMemoryTraceStorage incremental approach 不同但正确 (文件不是内存, 无法 O(1) update)
- B: Persistent index (sidecar file) — 过度工程, trace 文件本身已足够
- C: 不提供 index 方法 — 降低 query 能力, InMemoryTraceService 需 index 支持

**理由**: A 最实用。Index 方法是 FileTraceStorage-specific (off-interface per ISP), 用户可选择 InMemory (fast index) 或 File (query-time, 适合后分析)。不需要 sidecar 文件。

### D-95: Directory layout — traces/{traceId}/trace.jsonl + session.json

**选择**: `{baseDir}/{traceId}/trace.jsonl` + `{baseDir}/{traceId}/session.json`
**替代方案**: (与 Python 完全一致, 无替代方案讨论)

**理由**: 与 Python FileStorage 目录结构一致, Python dashboard 可直接消费 C# 产出。baseDir 参数化 (默认 "traces"), 不硬编码。

## Risks / Trade-offs

- **[Risk] IFileProvider.AppendLine 在高频 write 时性能瓶颈** → Mitigation: ITraceStorage 是 sync-first (D-22), 每个 step 只写 1-2 条记录 (不是高频 I/O)。物理 append 使用 File.AppendAllText (OS-level buffered)。如需优化, PhysicalFileProvider 可换为 buffered 实现 (不改接口)。

- **[Risk] JSONL 读取全量反序列化性能]** → Mitigation: FileTraceStorage index 方法 (GetByNodeId/GetBySpanType) 是 off-interface, 只在后分析时调用, 不是引擎 runtime 路径。引擎 runtime 只写, 不读。大 trace (>10K lines) 后分析可用专用工具, 不在 Core classlib scope。

- **[Risk] Export 方法在接口上 — InMemoryTraceStorage 已有内部实现, 需确认] → Mitigation: 检查 ITraceStorage 接口定义, 如 Export 已在接口上则不需修改 spec; 如不在则需新增。

- **[Trade-off] InMemory/ file 移动是纯文件组织, 不改 namespace] → 接受: 纯文件移动, 零代码变更, 不影响任何 test 或 import。
