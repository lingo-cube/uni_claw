## 1. 输出 schema

- [ ] 1.1 在 `src/UniClaw.Core/UniBrain/Schemas.cs` 加 `AnalyzeVisual` 常量：`PageAnalysis` 输出 JSON schema（镜像 §5.3 设计稿）。**items 只含 `name/type/coordinate{x,y}/parent`，不含 `expected_action`/`expects_page_change`/`expects_state_change`**（§12-A）。`type` 不在 schema enum 硬约束（宽容 AI 返回，code 侧 `ElementTypeMapper` 校验）。含 `level1_dir`/`level2_dir`（enum left/right/top/bottom）、`level1_menus`/`level2_menus`（name/coordinate/active）、`current_path`、`items`、`is_popup`、`popup_info`（可空，title/content/close_button）、`close_button`/`back_button`（可空 x,y）、`has_scroll`、`is_end_of_list`

## 2. IScreenCapture 截图接缝

- [ ] 2.1 新建 `src/UniClaw.Core/Traversal/IScreenCapture.cs`：Core 接口（与 `IActionExecutor` 共置），`Task<byte[]> CaptureAsync(CancellationToken ct = default)`；XML 注释说明「Core 持有抽象，真机实现（`AdbScreenCapture`）属 host」（§12-B / D5）。Core 不引用任何具体捕获实现

## 3. PageAnalyzer 真实实现

- [ ] 3.1 新建 `src/UniClaw.Core/UniBrain/PageAnalyzer.cs`：`sealed class : IPageAnalyzer`，ctor `(IModelProvider modelProvider, IPromptLibrary promptLibrary, IScreenCapture screenCapture)`（**D-8：注入 `IModelProvider` 不注入 `IModelRouter`**），三参数 null → `DomainValidationException` fail-fast（镜像 `TextUnderstanding`，但路由步装配期完成、方法体内无 `router.Resolve`）
- [ ] 3.2 实现 `AnalyzeCurrentPageAsync` 7 步：① `_screenCapture.CaptureAsync(ct)` 取截图 bytes ② `promptLibrary.GetTemplate(ModelCapabilities.AnalyzeVisual)` 缺失 → `DomainValidationException`（不发模型调用）③ `template.Resolve({})`（Variables 空，截图走 byte 参数）④ `new ModelRequest(resolved.User, resolved.System, Schemas.AnalyzeVisual, MaxTokens, Capability: AnalyzeVisual)` ⑤ `await _modelProvider.CompleteVisionAsync(modelRequest, screenshotBytes, ct)` ⑥ `resp.Success==false` → `DomainValidationException` 带 `resp.ErrorMessage` ⑦ `JsonSerializer.Deserialize<PageAnalysisDto>(resp.Content, DomainJsonOptions.Default)` → `MapToPageAnalysis(dto)` → `PageAnalysis`
- [ ] 3.3 内部私有 DTO（宽松承载）：`PageAnalysisDto` / `MenuInfoDto(Name, Coordinate, Active)` / `ItemDto(Name, Type, Coordinate, Parent)` / `CoordDto(X, Y)` / `PopupInfoDto(Title, Content, CloseButton)`。反序列化 null → 视为无效 JSON 走 fail-fast（镜像 `TextUnderstanding` 的 `?? throw JsonException` 通路）
- [ ] 3.4 `MapToPageAnalysis(dto)` 逐字段映射 + §12-A 派生：`MenuInfoDto → MenuInfo(Name, new Coordinate(x,y), Active)`（Coordinate 0-1 fail-fast）；`ItemDto → MenuItem`：`itemType = ElementTypeMapper.ToMenuItemType(dto.Type)`、`action = ElementTypeMapper.ToExpectedAction(dto.Type)`、`pageChange/stateChange = DeriveChangeFlags(action)`（私有 helper：Navigate/Action→(true,false)；Toggle→(false,true)；None→(false,false)）；`Direction.FromValue(level1_dir/level2_dir)`（非法 → `DomainValidationException`）；DTO 必填缺失（Items null / ItemDto.Type 空）→ `DomainValidationException`；构造 `new PageAnalysis(...)`
- [ ] 3.5 `FindAppEntryAsync` / `VerifyPageTypeAsync` 抛 `NotImplementedException("PageAnalyzer.<method> pending future slice.")`（D1 / D-143 idiom）

## 4. prompt 模板与 mock fixture

- [ ] 4.1 移植 Python `main` 分支 `src/ai/vision_service.py:19-112` 的 `PROMPT_STRUCTURE` → `PromptTemplate`（capability=`ModelCapabilities.AnalyzeVisual`）。**§12-A 剥散文**：删 `BUTTON TYPE CLASSIFICATION` 段（type→action 映射 + 4 example）；删 `expected_action`/`expects_page_change`/`expects_state_change` 输出字段要求；**保留** 任务描述 + 输出 JSON 格式（字段名、坐标归一化 0-1）+ **type 词表**（menu_item/tab/back_button/switch/toggle/button/icon/link/text/readonly）。`Variables = ImmutableArray<string>.Empty`（截图走 byte 参数，无变量）。终稿回填 design.md
- [ ] 4.2 新建 `tests/UniClaw.Core.Tests/Fixtures/analyze_visual.mock.json`：`MockModelEntry` 响应（`capability=analyze_visual`，返回合法 `PageAnalysis` JSON——含 level1/level2 menu、current_path、items（type 覆盖 Navigate/Action/Toggle/None 4 分支）、is_popup、has_scroll 等，**items 不含 action 3 字段**）

## 5. 测试

- [ ] 5.1 新建 `tests/UniClaw.Core.Tests/UniBrain/PageAnalyzerTests.cs`（mock `IScreenCapture` + mock `IModelProvider.CompleteVisionAsync`，无网络）：① ctor 三参数 null → `DomainValidationException` ② 模板缺失 → fail-fast（不发模型调用）③ happy path：mock 返固定 JSON → 验证 `PageAnalysis` 各字段（menu / current_path / popup / close_button / back_button / scroll）④ **§6.1 派生 4 分支全覆盖**（type=switch→Toggle/stateChange；type=menu_item→Navigate/pageChange；type→Action/pageChange；type→None/both false）⑤ fail-fast：非法 type / 非法 Direction / coordinate 越界 / Items null / ItemDto.Type 空 ⑥ **截图透传**：mock `CaptureAsync` 返特定 bytes → 验证传到 `CompleteVisionAsync` 的 byte[] 一致 ⑦ NIE：`FindAppEntryAsync`/`VerifyPageTypeAsync` 抛 `NotImplementedException`（含 "pending future slice"）
- [ ] 5.2 观测闭环断言：装配期 `router.Resolve(AnalyzeVisual)` 套 `ObservingModelProvider` + `InMemoryTraceRecorder`，`AnalyzeCurrentPageAsync` 产生 `AICallRecord`（`mode="vision"`, `capability=analyze_visual`）—— 验证多模态观测闭环（`InMemoryTraceStorage.GetAICalls()` 非空）
- [ ] 5.3 新建 `AnalyzeVisualEndToEndTests.cs`：真实 `PageAnalyzer` + 装配期 `router.Resolve(AnalyzeVisual)`（套观测 decorator，产物为 mock `IModelProvider` 声明式返 `analyze_visual.mock.json`）+ mock `IScreenCapture` + 注册 `analyze_visual` prompt stub —— 一条端到端验证 `CaptureAsync → CompleteVisionAsync(req, bytes) → Content → PageAnalysis`，并验证 D-8「router 降为装配期工厂」的正确性

## 6. 验证

- [ ] 6.1 `dotnet build src/UniClaw.Core.sln`：0 错误、0 功能性警告（新文件零警告）
- [ ] 6.2 `dotnet test src/UniClaw.Core.sln`：全绿（含本 slice 新增单元 + 端到端）
- [ ] 6.3 charter guard 不受影响：无新 enum 值（`Direction`/`MenuItemType`/`ExpectedAction` 锁定值仅消费）、无新 layer 引用、`ArchitectureGuardTests` 随全量套件 0 失败；`IPageAnalyzer`/`IModelProvider`/`IModelRouter`/`ModelRequest`/`ObservingModelProvider`/`UniBrainService` 任一签名零改动
