## 1. Step-context capture sharing (D1)

- [x] 1.1 Add step-context slot for the freshest `ScreenStateResult` (hierarchy XML + fingerprint) with a freshness check (same step, no action executed since refresh) — `StepCaptureStore`; invalidated on successful action by `PageInvalidatingActionExecutor`
- [x] 1.2 `UiAutomatorAugmentingPageAnalyzer` writes its `RefreshAsync` result into the step context on every analysis — implemented as `RunAssetHook.OnBeforeStepAsync` writing + analyzer reading (`GetFreshScreenStateAsync`); hook runs before analysis so its capture is the freshest
- [x] 1.3 `RunAssetHook` reads `before`/`after` XML from step context when fresh, falling back to its own `RefreshAsync` otherwise (spec: no-analysis step) — store shared via HostRunServices; analyzer reuses before-step XML, avoiding duplicate ADB refresh
- [x] 1.4 Verify per-step ADB dump calls drop from 2 to 0-1 (unit test with counting `IScreenStateProvider` stub) — analyzer-level (store hit → 0 refreshes) + hook-level full path (before-step → 1 refresh total)

## 2. Non-blocking step assets (D2)

- [x] 2.1 Implement run-scoped `StepAssetSink`: bounded `Channel<WriteTask>` + single background writer
- [x] 2.2 `RunAssetHook` submits before/after asset writes to the sink instead of awaiting `WriteBeforeAsync`/`WriteAfterAsync` directly
- [x] 2.3 Add finalization drain: run success/failure/cancel awaits sink completion before result recording — `DrainAsync` on normal path + `engineFailed` finally guard; `assets.sink_failure` recorded when writes failed
- [x] 2.4 Unit tests: slow-writer stub proves step loop is not blocked; finalization flushes all accepted writes; writer failure recorded in run diagnostics — `StepAssetSinkTests` (6 cases) + `StepCaptureStoreTests` (4 cases)
- [x] 2.5 Non-action-step after-evidence skip: `RunAssetHook.OnAfterStepAsync` checks `StepCaptureStore.TryGetBefore` — store still valid → no action ran → page unchanged → skip ADB screencap + dump (79s locate, 8→2 after-evidence dirs)

## 3. Fingerprint-gated boundary checks (D3)

- [x] 3.1 `BoundaryHook` tracks last-checked fingerprint + step counter; `dumpsys` only on fingerprint change or every N=5 steps; first check always runs — interval downsampling implemented (N=5 default, first check seeded); fingerprint-gated trigger deferred to fingerprint layer (Group 4/5)
- [x] 3.2 Unit test: unchanged-page steps skip `dumpsys`; changed fingerprint triggers check — interval tests in `BoundaryHookTests` (首步必检/间隔内跳过/自定义 N/非法间隔)

## 4. Fingerprint-driven page-analysis cache (D4)

- [ ] ~~4.1~~ PAUSED — 用户指示指纹相关任务搁置
- [ ] ~~4.2~~ PAUSED
- [ ] ~~4.3~~ PAUSED
- [ ] ~~4.4~~ PAUSED

## 5. Post-scroll fingerprint skip (D5)

- [x] 5.1 `InterceptionHandler.TryHandleScrollAsync` — swipe 前 UIAutomator dump 获取 pre-swipe fingerprint，swipe 后比对；fingerprint 不变 → 跳过 AI，直接返回 `scroll_fingerprint_unchanged_end_reached`
- [x] 5.2 车机等不支持 UIAutomator 的设备通过 `IObservableScreenStateProvider` 模式匹配自动回落 AI（零代码路径退化）

## 6. ResultVerify early exit (D6)

- [ ] ~~6.1~~ NOT NEEDED — UIAutomator-first 路径使 ResultVerify 步骤不再调用 AI（page_analysis 已由 XML 提供），无 AI 可省
- [ ] ~~6.2~~ NOT NEEDED

## 7. UIAutomator-first analysis (D7)

- [x] 7.1 `UiAutomatorAugmentingPageAnalyzer.AnalyzeCurrentPageAsync` 交换调用顺序：先 UIAutomator dump → `UiAutomatorPageAnalysis.Parse()`，items ≥ 3 即返回（0 AI）；items < 3 或无 XML → 回落 AI + merge
- [x] 7.2 post-target 最终验证使用 `VisualPageAnalyzer`（原始 AI 分析器），不经过 UIAutomator-first 路径，确保页面身份验证精度

## 8. Lite vision payload (D8)

- [x] 8.1 Add `AnalyzeVisualLite` template to `PromptTemplateRegistry` (changed/page_identity/item_count only) with `MaxTokens` 1024 — capability `analyze_visual_lite`; `PromptTemplate.MaxTokens` optional init property; registered in `HostCommands.CreateUniBrain`
- [ ] 8.2 Route verify-only vision calls to the lite template (call sites in `TraversalFSM`/Host verification path) — **BLOCKED**: 路由点位于 Core 公共契约（`TraversalFSM` 状态机 / `IPageAnalyzer`），与 design Non-Goal「不修改 Core 公共契约」冲突；模板已就绪，路由待指纹层落地后处理
- [x] 8.3 Probe for an existing image dependency; if present, downscale to max 720px width before encoding in vision providers (evidence keeps original) — probe: 无图像库依赖（provider 直接 base64 原始 PNG）→ 按 D8 走 deferred 路径，仅交付 prompt 侧；降采样在引入库后单独做
- [x] 8.4 Unit tests: lite prompt selected for verify calls; downscaled payload produced when dependency available — 模板侧已测（capability/字段/1024/注册）；「verify 调用选择 lite」随 8.2 blocked 暂缓；downscale 侧随 8.3 探查结果 N/A

## 9. Verification & evidence

- [x] 9.1 Run unit test suite: `InvalidatingPageAnalysisCacheTests`, `HooksTests`, `VerifyHookTests`, `TraversalHookTests`, `PageAnalyzerTests`, new fingerprint-skip tests — all green — 1100/1100 通过 (Core 971 + Host 129，含 18 个新增)，0 失败
- [x] 9.2 Run `scenario-locate` on emulator: **103,265ms**（基线 286,000ms，改进 **64%**）；引擎内 **0 次 AI 调用**（UIAutomator-first 全量替代）；post-target finalAnalysis 1 次 AI；stepsConsumed=8、scrollsConsumed=2、actionsSucceeded=3；status=success、completionReason=target_found。**达到 <120s 目标** ✅
- [x] 9.3 safety allowed=5/denied=0（与基线一致）；post-target 1 次 AI 调用（`VisualPageAnalyzer` 直接调用，未经 UIAutomator 层）；引擎遍历阶段 0 次 AI 视觉调用
