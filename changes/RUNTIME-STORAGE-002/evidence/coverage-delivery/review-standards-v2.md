# STORAGE-002 Coverage delivery — Standards review v2

范围：仅审更新 `candidate.json` 固定的 22 文件及 `candidate.patch`；22 个当前 SHA-256 均与清单吻合。未运行 build/test，未计入 Overlay/ExpectedEffect/ObservationRecovery 并行上下文。

## 复核结论：有界通过（无残余可行动 finding）

- **v1/P1 canonical generator：已处置。** `CoverageReportDeliveryOptions` 已删除 `GeneratorPath`，`RequireAsync` 只能调用私有 `InstallGeneratorAsync`（`CoverageReportDelivery.cs:7-14,66-77`）；后者由 `GeneratorSource` 的固定字节/摘要命名并写入私有 store（`CoverageReportDelivery.Generator.cs:8-76`）。因此此前“任意产品脚本生成不同 JSON”的触发路径已不存在；probe 的 `pythonExecutable` 仅是既有有界进程执行器的测试代理，不改被固定的 canonical generator。

- **v1/P1 原 RunId：已处置。** 身份改为非空类型；冻结、固定来源重试与 pinned generator 都拒绝 null、空白、首尾空白及超长 RunId（`CoverageReportDelivery.cs:15-18`，`CoverageReportDelivery.Sources.cs:25-40,92-101,192-193`，`CoverageReportDelivery.Generator.cs:21-28`）。成功后的重复 Capture 仍以完整不可变 material 比较，故不同 Kernel RunId 不能覆写。没有把 RunId 错绑为 Require 时尚未知的目录名，符合其来自 Kernel 的语义。新增 identity tests 覆盖 null/空白/首尾空白、未固定来源 RunId 改写与 retry 拒绝。

- **新增附件大小写修正：通过。** 接受阶段以 `OrdinalIgnoreCase` 拒绝声明冲突（`RequiredReportDelivery.cs:466-473`）；目录展开阶段保留 ordinal source 清单，但显式拒绝大小写变体（`CoverageReportDelivery.Sources.cs:42,68-82`），与 generator 的 `casefold` ZIP 检查一致。新增 review test 覆盖声明路径 `A.xml/a.xml`。

六态交付复用、旧 null 字段读取兼容、来源/用途保护、固定输入 SHA、结果与 Runner outcome 隔离、共享有界进程及 Host/Dsh 重试入口保持职责集中；未见其它未由工具覆盖的 Fowler smell。
