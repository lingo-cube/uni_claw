# ADR-0020: Live 感知的确定性锚是服务响应 JSON，而非截屏图像

## Context

PER-005 引入 live 感知后，一次观察产生两个 RawArtifact：

- **capture artifact**：`adb screencap` 的 PNG 字节（设备原始证据）；
- **derived artifact**：感知服务对同一帧的响应 JSON（YOLO/OCR/fusion 输出）。

`LiveVisionStrategy`（以及一切 replay/parity 验收）必须锚定其中之一作为
确定性输入。截屏图像看似是更"原始"的锚，但链路上存在多个非确定性源：
PNG→传输图像编码（legacy 路线是 JPEG q92，SkiaSharp 版本相关）、预处理
resize 的实现差异、以及未来编码器升级。响应 JSON 则是推理完成后的纯文本
输出，不受图像编码链路影响。

## Decision

**确定性锚 = derived artifact（服务响应 JSON 原文，逐字节）。** capture
PNG 保留为原始证据与未来 slow path（VLM）输入，不承担 replay 语义。
具体含义：

1. `LiveVisionStrategy.Observe` 的输入 artifact payload = 响应 JSON 原文
   （transport 分类通过后才 artifact 化）。
2. P-1 parity（live 解析 vs corpus 导入）与全部行为冻结验收（PER-007
   迁移、PER-008 管道化）都锚定「同帧响应的语义数组全等」。
3. derived artifact 的 `CaptureScope = derived:vision-service:{captureId}`
   携带 lineage（per-帧分组语义与 PER-003 D3 一致）。

## Considered Options

1. **锚 capture PNG**（rejected）：把策略输入定为图像、每次 replay 重跑
   推理——依赖编码器/推理环境稳定，跨版本 drift 直接破坏 replay；推理
   成本也使 replay 不可负担。
2. **锚响应 JSON**（accepted）：编码链路的任何漂移只影响"这一帧的推理
   质量"，不影响"已产出观察的确定性"；replay 零推理成本。
3. 双锚并重（rejected）：两套锚 = 两套真相，违背单真相源纪律。

## Consequences

- 图像编码/传输格式的改变（如未来换 JPEG 或 resize 参数）**不构成**
  replay 破坏——这正是 PER-007 迁移与 R 系列重构能以「语义全等」验收的
  前提。
- 推理本身的环境漂移（torch/ort 版本）不破坏既有 corpus 的 replay，但会
  使**新采集帧**与历史帧的分布可比性下降——由四层身份
  （pipelineRevision/deploymentId，PER-008）如实报告，而非隐瞒。
- capture PNG 仍然入 ledger（原始证据），未来 slow path/审计可直接引用。
- 若未来出现"从图像重算观察"的真实 buyer（如模型升级后的历史重标注），
  需另立 change 定义重算语义与新锚——本 ADR 不预设。

（谱系：PER-005 D9/grill 锤定；PER-007/008 两次零漂移验收实证；ADR 升格
判据由 ARCH-DOC-014 核对。）
