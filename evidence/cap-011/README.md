# CAP-011 证据 — 感知维度 R5 可替换性执法

四元组与原始输出索引。变更面：仅新增
`tests/UniClaw.Kernel.Tests/Capability/PerceptionProtocolReplaceabilityTests.cs`
（3 例）。未改产品源码，未动公开面与场景文件。

## A1/A2/A3 — R5 感知互换测试

```sh
dotnet test tests/UniClaw.Kernel.Tests \
  --filter "FullyQualifiedName~Capability|FullyQualifiedName~DocsMetadata|FullyQualifiedName~KernelRuntimeSurfaceWhitelist"
# expected: 全部通过（含 PerceptionProtocolReplaceabilityTests 3 例）
# actual:   已通过! 失败: 0，通过: 105（2026-10-07）
```

执法内容：

- 消费闭包只写 L1 双协议面（ISemanticPerception + IUiElementPerception，
  诊断经 ICapabilityHealthCheckable mixin），闭包内无 L2 具体类型；
- 两个 L2：产品 UniPerceptionCapability（fast 资产面 + 模型端两源）与
  replay 形状替换件（字典驱动 + Max 排序聚合，结构刻意不同）；
- 可互换契约 = 同一实例双面 + L0 双协议自述 + 健康聚合律（worst-of /
  混合 Unknown→Degraded / 探针异常→Degraded），对两个 L2 分别成立；
- L2 替换只经组合根（fresh registry Register + Resolve 取回），同一闭包
  代码零改动；替换件注册首次行使 ValidateImplementation 对非生产 L2 的
  协议-接口一致性执法。

## A4 — 全量无回归

```sh
dotnet test UniClaw.Kernel.slnx
# expected: 全部通过
# actual:   1389/1389 绿（832 Kernel 含 3 新例；见 full-solution-tests.txt）
```

## 文件

- `targeted-tests.txt` — 专项 105/105。
- `full-solution-tests.txt` — 全量 1389/1389。
