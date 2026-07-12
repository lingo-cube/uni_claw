## 1. ITraversalContext 接口修改

- [x] 1.1 移除 `CurrentFrame` 属性的 setter（保留 getter）
- [x] 1.2 移除 `GlobalState` 属性的 setter（保留 getter）
- [x] 1.3 移除 `LastError` 属性的 setter（保留 getter）
- [x] 1.4 验证编译通过（dotnet build）

## 2. TraversalRuntimeContext 添加 SetXxx() 方法

- [x] 2.1 添加 `SetCurrentFrame(ITraversalNode? value)` 方法
- [x] 2.2 添加 `SetGlobalState(GlobalState value)` 方法
- [x] 2.3 添加 `SetLastError(Exception? value)` 方法
- [x] 2.4 移除 ITraversalContext 实现中的 3 个属性 setters
- [x] 2.5 验证编译通过（dotnet build）

## 3. TraversalFSM 添加 RuntimeContext 属性

- [x] 3.1 添加 `RuntimeContext` 属性（返回 TraversalRuntimeContext）
- [x] 3.2 更新 Line 118: `Context.LastError = ex` → `RuntimeContext.SetLastError(ex)`
- [x] 3.3 更新 Line 217: `Context.LastError = ex` → `RuntimeContext.SetLastError(ex)`
- [x] 3.4 如需要：添加 `SetGlobalState()` 调用（异常处理路径）
- [x] 3.5 验证编译通过（dotnet build）

## 4. PopupHandler 改用 SetXxx() 方法

- [x] 4.1 更新 Line 350: `context.CurrentFrame = ...` → `context.SetCurrentFrame(...)`
- [x] 4.2 更新 Line 362: `context.GlobalState = ...` → `context.SetGlobalState(...)`
- [x] 4.3 更新 Line 365: `context.LastError = ...` → `context.SetLastError(...)`
- [x] 4.4 验证编译通过（dotnet build）

## 5. 测试更新

- [x] 5.1 搜索所有使用 `Context.LastError =` 的测试并更新
- [x] 5.2 搜索所有使用 `Context.GlobalState =` 的测试并更新
- [x] 5.3 搜索所有使用 `Context.CurrentFrame =` 的测试并更新
- [x] 5.4 运行 `dotnet test` 验证所有测试通过
- [x] 5.5 验证 CI 测试通过（617 tests 全绿）

## 6. 文档更新

- [x] 6.1 更新 `docs/system/decisions/log.md` D-7 状态为 Fixed
- [x] 6.2 添加实施完成记录到 design.md
- [x] 6.3 提交所有变更

## 7. 验证与归档

- [x] 7.1 最终验证：617 tests 全绿
- [x] 7.2 运行 `/opsx:archive` 归档此 change
