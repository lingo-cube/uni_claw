## 1. TraversalResult.Reasons 常量新增

- [x] 1.1 在 `Traversal/TraversalResult.cs` Reasons 类中新增 `TargetFound = "target_found"` 和 `Timeout = "timeout"` 两个 const string

## 2. RunAsync CompletionPolicy 检查块插入

- [x] 2.1 在 `Traversal/TraversalEngine.cs` RunAsync 循环中，AntiLoop 检查之后 (约 line 225) 插入 CompletionPolicy 检查块，包含 TargetFound/Timeout/MaxSteps 三个 if 分支
- [x] 2.2 TargetFound 分支: 匹配 `_ctx.CurrentFrame.Operation?.Target?.Value` (fallback Name)，MatchMode.Exact 用 OrdinalIgnoreCase，MatchMode.Contains 用 OrdinalIgnoreCase
- [x] 2.3 Timeout 分支: `stopwatch.Elapsed.TotalSeconds > policy.TimeoutSeconds` (严格大于)
- [x] 2.4 MaxSteps 分支: `i + 1 >= policy.MaxSteps` (CompletionPolicy 软上限优先引擎硬上限)
- [x] 2.5 CompletionPolicy null 或 Type=None 时跳过整个检查块

## 3. Done() GlobalState 映射变更

- [x] 3.1 修改 `Traversal/TraversalEngine.cs` Done() 方法 GlobalState 映射: TargetFound → Completed, Timeout → Terminated
- [x] 3.2 修改 Done() Success 映射: reason is AllVisited/AntiLoop/TargetFound → true

## 4. 单元测试

- [x] 4.1 `TargetFound_StopsAtTargetNode` — 目标节点精确匹配后终止 (3 页 fixture, DynamicMatch + menu_rule, CompletionPolicy TargetFound "Wi-Fi" Exact)
- [x] 4.2 `TargetFound_ContainsMatch` — 子串匹配后终止 (Contains "Blue" 匹配 "Bluetooth")
- [x] 4.3 `Timeout_ExceedsPolicySeconds` — elapsed 超过 TimeoutSeconds=0.001 后终止 (DelayPerStepMs=50 确保超时)
- [x] 4.4 `MaxStepsPolicy_ReachesUserLimit` — CompletionPolicy.MaxSteps=5 优于引擎硬上限=1000
- [x] 4.5 `CompletionPolicy_None_NoEffect` — CompletionPolicyType.None 不触发额外终止，正常走 AllVisited

## 5. 验证

- [x] 5.1 `dotnet test` 运行全部测试，新增 5 个测试全绿 (521/521 passed)
- [x] 5.2 原有 516 测试不受影响 (CompletionPolicy=null 时检查块被跳过)
