# 集成和验证入口

调用链：归一化YOLO/OCR当前布局（可选前页/已记录操作/预期）→现有Registry的
ISemanticPerception→UniPerception执法→Jev adapter→独立观察/关系/符合性→
SemanticObservationProjector→UniKernel.Process(P2)→World当前容器的Slice辅助语义。

Host组合示意（models/config/http由现有组合根提供，不让产品查找Harness凭据）：

```csharp
await using var supplier = new JevSemanticPerceptionAdapter(models, options, http);
var registry = PerceptionCapabilityComposition.RegisterProductPerception(
    uniPerception: supplier.CreateCapability(fastHealthSources));
var semantic = (ISemanticPerception)registry.Resolve("uni.perception")!;
var result = await semantic.ObserveSemanticsAsync(request, cancellationToken);
foreach (var proposal in SemanticObservationProjector.CreateProposals(request, result))
    kernel.Process(proposal);
```

替换供货只换注入的执行实现，调用方不识别Jev/provider/HTTP。既有无执行供货的
marker返回Unsupported。Host持有并释放supplier和自己创建的HttpClient；Registry不
驱动资源释放。实例按Run组合，attempt记录的生命周期与该实例相同。

request.Current必须有真实归一化布局、capture/cycle/session/time/space、新鲜度及
已接受EvidenceIds。来源是derived evidence时显式给SourceLeafEvidenceIds；默认只
适用于直接叶证据。前页保留自己的capture/time/basis；动作中的SourceCaptureId
指前页，元素描述须注明实际来源（OCR/任务标注等），不伪装成YOLO产出或回执。
预期只写Intent。候选目录由调用方给出，语义类别不作为ContainerId。
Current的PageName/PagePurpose/PageKey必须为空；这些可选项只用于前页人工/已知
上下文，并在供应商请求中明确单列来源含义。完整请求Web默认UTF-8 JSON上限
128KiB；超限不发请求、不裁剪后伪称完整。

World关联还要求当前basis包含或派生自身份观察，capture与时间保持对齐；仅有
OCR父证据而无当前页面观察关联时可入账，但不会猜测挂到哪个Slice。供货不造
ui.screen、current-container、导航边或Effect proof，ExpectedMatch不投影。

可重复验证：Kernel的SemanticPerceptionCapabilityTests、World的
SemanticCapabilityWorldIntegrationTests、Host的JevSemanticPerceptionTests。
实际.NET回放入口为tools/experiments/jev-semantic-cap-20261021；输入/请求/响应/
结果在evidence/jev-semantic-cap-001。run.mjs只负责实验时在内存注入已有凭据；
产品adapter不读取这些文件。重跑会产生外部API消费并覆盖本地回放结果，先另存
既有证据或更换输出目录；本轮不为改善分数反复重跑。

本轮未更改运行中DSH供货配置；没有启动设备操作。真实结果为用途11/12，
前后关系/符合性各12/12；样本小、开发集复用和人工上下文限制见live-summary.json。
