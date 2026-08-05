# Verify 证据链修正 — 身份判定三 Bug 修复

> 日期: 2026-08-05
> 状态: proposed
> 来源: `artifacts/runs/manual-roi-verify/locate-one-item/20260804T170915736Z-9d605e0c50ee465` 真机验证 + trace-analyzer 深度诊断
> 范围: `src/UniClaw.TraceTool/LocateOneItemRule.cs` + `src/UniClaw.Host/Commands/HostCommands.cs` + `src/UniClaw.Host/Artifacts/FileAssetStore.cs` + `src/UniClaw.Core/Observability/ITracePipeline.cs` + `src/UniClaw.Host/HostServices/AnalysisWritingDecorator.cs`

## 1. Motivation

delete-uia + roi-scroll-detection 改动后，locate-one-item 场景在 local-vision provider 下**引擎遍历正常**（target_found，12 步，4/4 动作成功，3 次 ROI 滚动），但 TraceTool verify 判定 `target_page_identity_not_verified`（`final_identity='<none>'`）——engineer 和 verify 层的结论矛盾。

trace-analyzer 深度诊断定位三个独立 bug：

| # | Bug | 层级 | 直接导致判定失败？ |
|---|-----|------|--------------------|
| 1 | `IdentityMatches` 空串短路 | TraceTool 判定层 | ✅ 是（当前 fallback 到空 name 直接失败） |
| 2 | post-target 分析不入证据链 | Host 装配层 | ✅ 是（即使 Bug 1 修复，post-action 页面身份不可达） |
| 3 | analysis.jsonl 覆盖写 | 资产 I/O 层 | ⚠️ 加剧（若不修，多帧分析只留一帧，depends on 该帧正好是 post-target 分析） |

三个 bug **独立但相互放大**。单独修任何一个都不足以让 verify 通过，必须三个一起修。

## 2. Bug 1 — IdentityMatches 空串短路

### 2.1 根因

[LocateOneItemRule.cs:80-87](src/UniClaw.TraceTool/LocateOneItemRule.cs#L80-L87)：

```csharp
private static bool IdentityMatches(string actual, string expected)
{
    var normalizedActual = Normalize(actual);     // "" → ""
    var normalizedExpected = Normalize(expected); // "about device" → "aboutdevice"
    return string.Equals(normalizedActual, normalizedExpected, StringComparison.Ordinal)
           || normalizedActual.Contains(normalizedExpected, StringComparison.Ordinal)
           || normalizedExpected.Contains(normalizedActual, StringComparison.Ordinal);
    //  ↑ "aboutdevice".Contains("") = true ← BUG
}
```

当 `actual` 为空串（OCR 空识别 item 的 Name=""）时：
- `"".Contains("aboutdevice")` = false ✅
- `"aboutdevice".Contains("")` = **true** ❌

第一个空 name item 短路命中 → `finalIdentity=""` → `identityMatched=false` → 判定失败。

**关键**：即使 Bug 2/3 修好后 `analysis.jsonl` 里有了 post-target 页面的 "About emulated device" 菜单项，如果页面里同时有 OCR 空识别的 item（常见：图标无文字、分隔线、空白区域），空串短路仍会导致 verification 失败。

### 2.2 修复

```csharp
private static bool IdentityMatches(string actual, string expected)
{
    if (string.IsNullOrWhiteSpace(actual) || string.IsNullOrWhiteSpace(expected))
        return false;

    var normalizedActual = Normalize(actual);
    var normalizedExpected = Normalize(expected);
    return string.Equals(normalizedActual, normalizedExpected, StringComparison.Ordinal)
           || normalizedActual.Contains(normalizedExpected, StringComparison.Ordinal)
           || normalizedExpected.Contains(normalizedActual, StringComparison.Ordinal);
}
```

2 行守卫。纯判定层改动，不影响引擎 / Host / 产物。

### 2.3 影响范围

- `src/UniClaw.TraceTool/LocateOneItemRule.cs` — 1 文件，+2 行
- 测试：`LocateOneItemRule` 新增空串 / 空白串 / null 输入 case

---

## 3. Bug 2 — post-target 分析不入证据链

### 3.1 根因

[HostCommands.cs:923](src/UniClaw.Host/Commands/HostCommands.cs#L923) — 引擎 `target_found` 后执行一次 post-target 页面分析：

```csharp
finalAnalysis = await services.VisualPageAnalyzer.AnalyzeCurrentPageAsync(ct);
```

`VisualPageAnalyzer` 是 `CreateRunServices` 工厂里传入的裸 `providerBrain.PageAnalyzer`（[HostCommands.cs:598](src/UniClaw.Host/Commands/HostCommands.cs#L598)）——不经 `AnalysisWritingDecorator`，快照永不落 `analysis.jsonl`。

而 `RunEvidenceLoader` 只读 `analysis.jsonl` 最后一行。即使引擎 12 步分析全部正常写入（Bug 3 修复后），最后一行仍是引擎最后一步的**click 前** Settings 主页——post-target 的 "About emulated device" 页面身份永不可达。

**与 delete-uia 的关系**：D-201 原语义中 post-target 页面身份来自 UIA（`trusted UIAutomator title`）。delete-uia 移除 UIA 后 Host 改为视觉 AI 分析（[HostCommands.cs:921-922](src/UniClaw.Host/Commands/HostCommands.cs#L921-L922)注释），但该分析走 raw provider 未接入证据链——这是 delete-uia 迁移的遗漏项。

### 3.2 修复

在 `CreateRunServices`（[HostCommands.cs:494-612](src/UniClaw.Host/Commands/HostCommands.cs#L494-L612)）中，对 `VisualPageAnalyzer` 也套 `AnalysisWritingDecorator`：

```csharp
// 原（598 行）:
providerBrain.PageAnalyzer,

// 改:
accessor is not null
    ? new AnalysisWritingDecorator(providerBrain.PageAnalyzer, accessor, pipeline, assets.RunDirectory)
    : providerBrain.PageAnalyzer,
```

- `accessor` 非 null（local provider）→ 套 decorator，post-target 分析自动写入 pipeline
- `accessor` null（非 local provider）→ 保持裸 analyzer，不新增行为
- pipeline / assets.RunDirectory 已在工厂方法作用域内（569-571 行主 `pageAnalyzer` 装配处使用）

**D-19x 不受影响**：`AnalyzeUntilSettledAsync`（[HostCommands.cs:1129](src/UniClaw.Host/Commands/HostCommands.cs#L1129)）的约束是"不走 `InvalidatingPageAnalysisCache`"（防止首帧退化结果毒化引擎缓存）。`AnalysisWritingDecorator` 只做写入 + accessor 更新，不缓存，不改变分析结果本身。reset 阶段的多次 poll 快照也会写入 `analysis.jsonl`——多几行证据，不影响 correctness（`RunEvidenceLoader` 取最后一行）。

### 3.3 影响范围

- `src/UniClaw.Host/Commands/HostCommands.cs` — `CreateRunServices` 方法，~3 行改动
- 测试：确认 `analysis.jsonl` 最后一行是 post-target 页面

---

## 4. Bug 3 — analysis.jsonl 覆盖写

### 4.1 根因

[AssetStagingWriter.cs:22-24](src/UniClaw.Host/Artifacts/AssetStagingWriter.cs#L22-L24)：每次 `WriteBytesAsync` 执行 `tmp+move`（`File.Move(tmp, path, overwrite: true)`）——整文件替换。

`AnalysisWritingDecorator.SubmitSnapshot`（[AnalysisWritingDecorator.cs:75-87](src/UniClaw.Host/HostServices/AnalysisWritingDecorator.cs#L75-L87)）每次分析提交一行 JSONL，但 `FileAssetStore` 的 `WriteAsync` 无条件调用 `AssetStagingWriter.WriteBytesAsync` → 第二次写入覆盖第一次 → N 次分析只留最后一次。

`RunEvidenceLoader`（[RunEvidenceLoader.cs:63-78](src/UniClaw.TraceTool/RunEvidenceLoader.cs#L63-L78)）读全部行取最后一行——但只读到 1 行（最后一次写入的），实际损失了 N-1 行。

D-197 的设计意图是 **"append-only JSONL，一行一个分析"**（[AnalysisWritingDecorator.cs:18](src/UniClaw.Host/HostServices/AnalysisWritingDecorator.cs#L18)），但实现层破坏了该语义。

### 4.2 修复

三部曲：

**a) `AssetSubmission` 加 `Append` 标志**（[ITracePipeline.cs](src/UniClaw.Core/Observability/ITracePipeline.cs)）

```csharp
public sealed record AssetSubmission(
    string Category,
    byte[] Bytes,
    string RelativePath,
    bool Append = false);  // 默认 false 保持现有行为不变
```

**b) `FileAssetStore.WriteAsync` 分支处理**（[FileAssetStore.cs:24-33](src/UniClaw.Host/Artifacts/FileAssetStore.cs#L24-L33)）

```csharp
public async Task WriteAsync(string runId, string relativePath, byte[] bytes, CancellationToken ct = default)
{
    // ... path resolution + directory create ...
    if (append)
    {
        await using var stream = new FileStream(fullPath, FileMode.Append,
            FileAccess.Write, FileShare.Read);
        await stream.WriteAsync(bytes, ct);
    }
    else
    {
        await AssetStagingWriter.WriteBytesAsync(fullPath, bytes, ct);
    }
}
```

**c) `AnalysisWritingDecorator.SubmitSnapshot` 传 `Append: true`**（[AnalysisWritingDecorator.cs:83-87](src/UniClaw.Host/HostServices/AnalysisWritingDecorator.cs#L83-L87)）

```csharp
_pipeline.Submit(new AssetSubmission(
    AssetCategories.AnalysisSnapshot,
    Encoding.UTF8.GetBytes(line),
    "analysis.jsonl",
    Append: true));
```

### 4.3 设计决策

- **显式 `Append` 优于文件名隐式推断**：`AssetSubmission` 是数据对象，`Append` 语义属于提交者的意图表达，不应由 store 按文件扩展名猜测
- **默认 `false` 保持现有行为**：截图等二进制资产不变
- **`FileShare.Read` 允许并发读**：diagnose/verify 可能在 run 执行期间读取

### 4.4 影响范围

- `src/UniClaw.Core/Observability/ITracePipeline.cs` — `AssetSubmission` record，+1 字段
- `src/UniClaw.Host/Artifacts/FileAssetStore.cs` — `WriteAsync`，+7 行
- `src/UniClaw.Host/HostServices/AnalysisWritingDecorator.cs` — `SubmitSnapshot`，+1 参数
- 测试：验证 `analysis.jsonl` 行数 = 分析次数（reset polls + engine steps + post-target）

---

## 5. 修复后完整链路

```
RunScenarioAsync:
  │
  ├─ AnalyzeUntilSettledAsync(VisualPageAnalyzer)  ← reset poll (2-5 次)
  │    └─ decorator → analysis.jsonl (append)
  │
  ├─ engine.RunAsync:
  │    step#1  pageAnalyzer → decorator → analysis.jsonl (append)
  │    step#2  pageAnalyzer → decorator → analysis.jsonl (append)
  │    ...
  │    step#12 pageAnalyzer → decorator → analysis.jsonl (append)
  │    → target_found ✓
  │
  ├─ [wait 750ms stabilization]
  │
  ├─ VisualPageAnalyzer.AnalyzeCurrentPageAsync  ← post-target (NOW decorated)
  │    └─ decorator → analysis.jsonl (append) ← About emulated device 页面
  │
  └─ TraceTool verify:
       RunEvidenceLoader → last row = post-target 页面
         items[] 含 "About emulated device" (y=0.9125)
         ↓
       LocateOneItemRule.IdentityMatches("About emulated device", expected)
         ↓ 空值守卫: !IsNullOrWhiteSpace ✓
         ↓ Contains 匹配 ✓
         ↓
       target_page_identity_verified ✓
```

---

## 6. 测试策略

### 6.1 单元测试

| 测试 | 覆盖 Bug | 验证点 |
|------|----------|--------|
| `IdentityMatches("", "About device")` → false | Bug 1 | 空串守卫 |
| `IdentityMatches(null, "About device")` → false | Bug 1 | null 守卫 |
| `IdentityMatches("  ", "About device")` → false | Bug 1 | 空白串守卫 |
| `IdentityMatches("About emulated device", "About device")` → true | Bug 1 | 正向 containment |
| `BuildYoloBboxes` + `RoiSelector` density passthrough | 密度回归 | 已覆盖（InterceptionHandlerRoiTests + RoiSelectorTests）|
| `AssetSubmission` Append 默认 false | Bug 3 | 序列化/反序列化 |
| `AnalysisWritingDecorator.SubmitSnapshot` Append=true | Bug 3 | 参数传递 |

### 6.2 集成测试

| 测试 | 验证点 |
|------|--------|
| 10.3 scenario-locate 完整跑通 + verify 通过 | Bug 1+2+3 修复后 `target_page_identity_verified` |
| `analysis.jsonl` 行数检查 | 行数 ≥ engine steps + post-target + reset polls |
| `analysis.jsonl` 最后一行 identity | 含 expected identity 的 item 名 |
| 非 local provider 不受影响 | Bug 2 的 accessor null 分支 |

### 6.3 回归

- `dotnet test` 全量通过（Core + Host + TraceTool）
- 已有 RoiSelectorTests / InterceptionHandlerRoiTests 不变
- UIA 已删除的测试不再恢复

---

## 7. 任务拆解

1. **Bug 1**：`LocateOneItemRule.IdentityMatches` 加空值守卫 + 单元测试
2. **Bug 3a**：`AssetSubmission` record 加 `Append` 字段
3. **Bug 3b**：`FileAssetStore.WriteAsync` 按 `Append` 分支
4. **Bug 3c**：`AnalysisWritingDecorator.SubmitSnapshot` 传 `Append: true`
5. **Bug 2**：`CreateRunServices` 中 `VisualPageAnalyzer` 套 `AnalysisWritingDecorator`
6. 构建验证（`dotnet build` Release）
7. 单元测试（`dotnet test`）
8. 集成测试（10.3 scenario-locate → verify 通过）
9. 全量回归（1325+ tests）

---

## 8. 相关

- D-197: 分析证据落盘（`AnalysisWritingDecorator` append-only 语义）
- D-201: identity fallback 语义（`LocateOneItemRule` 移植自 `ScenarioCompletionVerifier`）
- D-19x: reset 验证 settle gate（不走缓存，decorator 满足约束）
- delete-uia: UIAutomator 移除 → post-target 身份依赖从 UIA title 迁移到视觉分析
- Pre-existing bug（不在此 PRD 修复）: `ScenarioRunOutcome.SuccessEvidence = default` → 序列化 `InvalidOperationException`。独立一行修复，不阻塞 verify 判定。
