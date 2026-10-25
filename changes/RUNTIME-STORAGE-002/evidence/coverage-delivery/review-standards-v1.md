# STORAGE-002 Coverage delivery — Standards review v1

范围：仅审 `candidate.json` 固定的 20 文件（哈希逐一吻合）及 `candidate.patch`；未计入 Overlay/ExpectedEffect/ObservationRecovery 并行上下文，未运行 build/test。

## Findings

1. **[P1] Coverage 可被任意产品生成器改写，违背唯一生成器/原字节投影边界。** `CoverageReportDeliveryOptions.GeneratorPath` 暴露了任意绝对路径（`CoverageReportDelivery.cs:7-14,67-78`），`RequireAsync` 只固定该文件哈希；`RequiredReportDelivery` 只核验输出的存在/摘要，未核验其等于 `coverage-source.json` 中的原 JSON。这样会绕过声明为唯一 canonical 实现的 `GeneratorSource`（`CoverageReportDelivery.Generator.cs:8-59`），与契约“Host 中单一定义”及“相同原 JSON 投影”（`coverage-delivery-contract.md:14,16,19`）冲突。这也是 **Speculative Generality**：契约只授权产品根与相对存储路径配置（:15），但增加了可替换生成器缝。可复现：传入一个写出 `{}` 与 `[]` 的自定义 Python `GeneratorPath`，以合法 material 调用 Require/CaptureAndDeliver；交付会 Delivered，并将非原 report 投影回 run 目录。删除该公开 override，或仅允许等于内置 generator 的固定摘要；加入字节相等回归。

2. **[P1] 原 RunId 不是必需且没有被校验/使用。** `CoverageReportSourceIdentity` 将 RunId 声明为可空（`CoverageReportDelivery.cs:17`）；`SaveMaterialAsync` 仅校验三种 recording ID（`CoverageReportDelivery.Sources.cs:25-32`），内置生成器也只检查这三项（`CoverageReportDelivery.Generator.cs:21-24`）。直接以 `RunId: null` 和三个合法 GUID 调用 CaptureAndDeliver 会成功交付，生成的 `coverage-source.json` 没有“原 RunId”，违反来源绑定契约（`coverage-delivery-contract.md:8,20`）。把 RunId 设为非空，并在冻结和生成前校验其与已接受 run 身份一致；加入 null/错配 RED。

## Bounded pass

除上述两点外，候选正确复用六态 `RequiredReportDelivery`、保留旧 null 字段兼容 fixture、将交付结果与 Runner outcome 分离；路径/用途保护、固定输入摘要、共享有界进程及 Host/Dsh 重试入口均职责集中，未见其余可行动的 Fowler smell 或已由工具执法项。
