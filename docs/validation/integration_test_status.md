# Integration Test Coverage Report

**Project**: Uni-Claw  
**Version**: V6.5  
**Generated**: 2026-06-06  
**Changes**: trace-integration (V6.3) + simulation-interface-alignment (V6.4) + state-machine-operation-integration (V6.5)

---

## Executive Summary

| 状态 | 文件数 | 测试数 |
|------|--------|--------|
| ✅ 正常运行 | 6 | 96 |
| ❌ 预存失败 (collection error) | 3 | 47 |
| ⚠️ 预存断言失败 | 1 | 1 |
| **合计** | **10** | **144** |

**有效通过率**: 96/144 (67%)，其中 48 个失败均为预存问题，非 V6.3-V6.5 引入。

---

## 1. ✅ 正常运行的集成测试 (96 tests)

### 1.1 状态机错误处理集成 (`tests/v6/test_state_machine_error_integration.py`)

**20 tests** — 全部通过

| 测试类 | 场景 |
|--------|------|
| `TestStateMachineErrorIntegration` | 错误处理器初始化、网络错误恢复、元素错误处理、重试计数、skip 重置 |
| `TestStateMachineErrorRecoveryScenarios` | 错误上下文存储、恢复摘要、重置后保持 handler |
| `TestStateMachineErrorHandlingIntegration` | 状态转移 + 错误处理、多错误类型会话、网络错误恢复序列 |

### 1.2 状态机容器处理集成 (`tests/v6/test_state_machine_container_integration.py`)

**20 tests** — 全部通过

| 测试类 | 场景 |
|--------|------|
| `TestStateMachineContainerIntegration` | 容器 handler 初始化、完整/不完整容器、上下文存储、统计追踪 |
| `TestStateMachineContainerRecoveryScenarios` | 重置后保持 handler、状态转移 + 容器处理、上下文传递 |
| `TestStateMachineContainerHandlingIntegration` | 多容器会话、深度嵌套、慢处理、复杂菜单结构 |

### 1.3 状态机弹窗处理集成 (`tests/v6/test_state_machine_popup_integration.py`)

**20 tests** — 全部通过

| 测试类 | 场景 |
|--------|------|
| `TestStateMachinePopupIntegration` | 弹窗 handler 初始化、检测到/未检测到弹窗、上下文存储 |
| `TestStateMachinePopupHandlingScenarios` | 统计追踪、重置后保持、状态转移 + 弹窗处理 |
| `TestStateMachinePopupHandlingIntegration` | 多弹窗会话、权限弹窗、错误弹窗、广告弹窗 |

### 1.4 Trace 集成 (`tests/v6/test_trace_integration.py`)  — V6.3 新增

**12 tests** — 全部通过

| 测试类 | 场景 |
|--------|------|
| `TestMockEngine` | MockEngine 端到端 trace 生成、节点类型/span 类型完整性 |
| `TestTraceOutputFormat` | JSON 可序列化、trace_id 一致性、JSONL 格式读写 |
| `TestSessionJson` | session.json 创建/内容验证、FileStorage 读写 |
| `TestScreenshotIndexMapping` | 截图引用 ID 映射写入/读取 |
| `TestContextRecoveryIntegration` | 从 MockEngine trace 恢复上下文、恢复正确性验证 |
| `TestTraceDirectoryStructure` | 目录结构验证 (trace.jsonl + screenshots/index.json) |

### 1.5 AI Trace 集成 (`src/ai/test/trace/test_integration.py`)

**22 tests** — 全部通过

| 测试类 | 场景 |
|--------|------|
| `TestTraceIntegration` | Span 启停、带 parent span 的调用链、上下文注入、success/failure finish、metrics 记录 |
| `TestTraceIntegrationWithMock` | 活跃 span 查询、provider 级 metrics、health check、trace logger 集成 |

### 1.6 Exception 集成 (`src/exception/test/test_integration.py`)

**7/8 tests** — 7 通过，1 预存断言失败

| 场景 | 状态 |
|------|------|
| 元素未找到 → 重试成功 | ✅ |
| 元素未找到 → 重试耗尽 → backtrack | ✅ |
| 设备离线 → 终止 | ✅ |
| 弹窗检测 → 关闭 → 继续 | ✅ |
| App 崩溃 → 重启 → 导航回原页面 | ✅ |
| 处理器自身异常不破坏链 | ✅ |
| 严重度覆盖 | ✅ |
| 同优先级多处理器 (`test_multiple_handlers_same_priority`) | ❌ 预存断言失败 |

---

## 2. ❌ 预存失败 (非 V6.3-V6.5 引入)

### 2.1 AI 集成测试 (`tests/integration/test_ai_integration.py`)

**13 tests** — 全部 collection error

**根因**: `conftest.py` 中 `mock_adb`/`mock_vision`/`mock_state` fixtures 引用不存在的模块。

| 场景 | 预期行为 |
|------|----------|
| 规则失败时调用 AI | AI advisor 作为规则引擎 fallback |
| 规则无法定位时调用 AI | AI 补充目标定位 |
| 异常链耗尽时调用 AI | AI 作为最后恢复手段 |
| 危险操作被拒绝 | SafetyFilter 拦截危险 action |
| 被阻止的操作类型被拒绝 | 类型级 block |
| 超时返回 unsafe | AI 调用超时降级策略 |
| 缓存命中/未命中 | 相同/不同上下文缓存行为 |
| 防抖限制/重置/禁用 | AI 调用频率控制 |

### 2.2 E2E 仿真测试 (`tests/integration/test_simulation_e2e.py`)

**12 tests** — 全部 collection error

**根因**: 依赖 `complete_fixtures`/`test_fixtures_dir` conftest fixtures 不存在。

| 场景 | 预期行为 |
|------|----------|
| 完整仿真工作流 | 端到端执行 + 结果验证 |
| 页面分析集成 | vision mock 集成到 runner |
| Mock 视觉/动作集成 | 组件级注入验证 |
| Trace 断言器集成 | 结构化 trace 断言 |
| 所有核心 fixtures 执行 | 批量 fixture 遍历 |
| 跨组件上下文持久化 | 状态跨组件一致性 |
| 可视化输出 | ASCII/Mermaid 渲染 |
| 带错误处理的完整工作流 | 异常路径 |
| 统计计算准确性 | 数据正确性校验 |
| 性能目标达成 | 延迟阈值检查 |

### 2.3 图-状态-Trace 集成 (`tests/integration/graph_state_trace/test_integration.py`)

**22 tests** — 全部 collection error

**根因**: 导入错误。

| 场景 | 预期行为 |
|------|----------|
| 模板注册表匹配 | 动态节点匹配 |
| 节点栈深度优先顺序 | DFS 遍历顺序 |
| 编排器初始化 + root 节点 | GraphTraversalEngine 创建 |
| TraceRecorder + 编排器 | 录制器注入到引擎 |
| Trace 回放工作流 | 录制→存储→回放 |
| 图模式配置开关 | 开关图遍历模式 |
| 简单/动态图遍历 | 静态子节点 + 动态匹配 |
| 深度优先正确性 | 遍历算法验证 |
| 混合模式静态根+动态子 | 模式混合正确性 |
| 线性→图切换 | 模式迁移兼容 |
| 编排器错误处理 | 引擎异常路径 |
| 栈错误恢复 | 中断后栈恢复 |
| 完整录制工作流 | FileStorage 端到端 |
| Trace 清理 | 生命周期管理 |

---

## 3. 覆盖盲区

| 盲区 | 严重度 | 说明 |
|------|--------|------|
| **仿真 → 真实引擎端到端** | 🔴 高 | V6.4 打通了 SimulationRunner → Engine 调用链，但无集成测试验证完整 trace |
| **handler → span 端到端** | 🔴 高 | V6.5 handler 调用 vision/action 后，无集成测试验证 ai_call/execution span 正确写入 |
| **FileStorage + 引擎高负载** | 🟡 中 | 集成测试只用 MemoryStorage，未验证 FileStorage 异步队列不丢数据 |
| **上下文恢复完整流程** | 🟡 中 | ContextRebuilder 有单元测试，无"运行→中断→恢复→继续遍历"集成场景 |
| **跨组件 trace 一致性** | 🟡 中 | 无测试验证 AI 调用→状态转移→动作执行→错误处理的完整 trace 链 |
| **多 trace 并发** | 🟢 低 | MemoryStorage 有多 trace 隔离单元测试，无并发写入场景 |
| **真实 VisionService + 引擎** | 🟢 低 | 全部用 MockVisionService，未覆盖 ClaudeVisionService |
| **真实 OperationExecutor + 引擎** | 🟢 低 | 全部用 MockActionExecutor，未覆盖真实 ADB 执行器 |

---

## 4. 建议

### 短期（V6.5 收尾）
1. 新增 `test_simulation_engine_e2e.py` — SimulationRunner + 真实引擎 + 完整 trace span 验证
2. 新增 `test_handler_span_e2e.py` — 验证 handler → metrics → ai_call/execution span 完整链路

### 中期（V6.6+）
3. 修复 `tests/integration/` 下 3 个 collection error（补充缺失 fixtures）
4. 补充 FileStorage 异步写入压力测试（高并发写入 + flush 正确性）
5. 补充上下文恢复"中断→保存→恢复→继续"完整集成场景

### 长期
6. 真实 VisionService + 真实 OperationExecutor 的集成测试（需要真实设备/模拟器）
