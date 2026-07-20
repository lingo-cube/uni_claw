## Why

Observability 层目前只有 InMemoryTraceStorage — 全部 trace 数据在内存中, 无法持久化到文件供 Python 仪表板消费。Python 已有完整 FileStorage (JSONL 格式 + session.json + 后台写线程), C# 缺少对应实现, 导致 C# trace 数据无法导出到 Python 可读格式, 工具链互操作断裂 (C-7 约束)。

## What Changes

- **新增 IFileProvider**: 6 方法的抽象接口 (EnsureDirectory, AppendLine, ReadAllText, ReadAllLines, FileExists, DirectoryExists), 使 Core classlib 不直接依赖 System.IO
- **新增 PhysicalFileProvider**: System.IO 实现, 6 方法委托到 Directory/File 静态方法
- **新增 FileTraceStorage**: ITraceStorage 实现, JSONL 格式写入 trace 记录 (每行独立 JSON 对象, `record_type` 鉴别器), session.json 元数据文件
- **重组 Observability 目录**: InMemoryTraceStorage/InMemoryTraceService 移至 Observability/InMemory/ 子目录; 新代码放 Observability/File/; 接口和 record 类型留根目录 (纯文件组织, namespace 不变)

## Capabilities

### New Capabilities
- `trace-file-storage`: FileTraceStorage JSONL 写入 + IFileProvider 抽象 + 目录结构 (traces/{traceId}/trace.jsonl + session.json)

### Modified Capabilities
- `trace-storage`: ITraceStorage 接口增加 ExportTrace 方法 (之前只有 InMemoryTraceStorage 内部的 Export, 现在提升到接口级别, 使 FileTraceStorage 也能导出完整 trace JSON)

## Impact

- **代码**: 新增 3 文件 (IFileProvider, PhysicalFileProvider, FileTraceStorage); 移动 2 文件 (InMemoryTraceStorage → InMemory/, InMemoryTraceService → InMemory/)
- **测试**: 新增 ~20-25 FileTraceStorageTests (MockFileProvider 注入, 无真实文件系统); PhysicalFileProvider 用 temp directory
- **API**: IFileProvider 新增公开接口; ITraceStorage 新增 ExportTrace 方法 (如果之前不在接口上)
- **依赖**: Core classlib 不新增 System.IO 引用 (已有); PhysicalFileProvider 使用 System.IO 但通过 IFileProvider 解耦
- **Python 互操作**: JSONL 格式设计兼容 Python dashboards (record_type 鉴别器)
