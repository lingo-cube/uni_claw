# Observability & Integration Conventions

> 项目约定：可观测性（Trace）设计方向 + 外部服务集成注意点。
> 最后更新: 2026-07-30

## Trace 系统设计方向

Trace 不只是"填充空壳方法"，它是**可观测性基础设施**，服务以下目标：

1. **树重建**: 记录节点访问和操作，构建带 parent-child 关系的 traversal tree
2. **类型剪枝**: 按 SpanType 过滤，从同一份 trace 数据生成不同分析视图（只看 AI 调用、只看错误处理、只看页面跳转）
3. **操作分析**: 跟踪执行的操作，用于分析、学习和缓存优化
4. **AI 调用跟踪**: 记录 AI（vision）调用延迟、触发时机、成功/失败、token 用量
5. **异常处理行为**: 记录错误分类、策略选择、恢复执行
6. **可观测服务**: 暴露 trace 数据用于优化分析 — JSON 导出、仪表盘、查询 API

### 属性驱动的 Trace 注解（Phase 2.3 规划）

`[Trace(SpanType.X)]` 属性标注在 handler 方法上，自动生成 ExecutionRecord，无需手动 TraceCoordinator 调用：

```csharp
[Trace(SpanType.ContainerHandling)]
public ContainerActionResult HandleContainer(...) { ... }
```

- 实现机制: Source Generator 或运行时反射
- 回退: 无 `[Trace]` 属性的方法仍正常工作 — trace 是增量，非必需
- 一致性: 属性生成的 ExecutionRecord 走与手动调用相同的 ITraceRecorder 路径

### 待定设计决策

1. **TraceNode vs ITraceRecorder**: TraceNode 层级（SessionNode → StepNode → SpanNode）从未被填充。需决定是连接 TraceNode tree 作为存储模型，还是删除它并在 flat record 中加 ParentNodeId
2. **StepTraceSnapshot**: TraceCoordinator 应收集 step 内所有事件到 snapshot（而非竞态的 last-value 查询）
3. **同步-over-async**: GetAwaiter().GetResult() 在 ASP.NET 中阻塞。需异步 TraceCoordinator 或 fire-and-collect 模式

## 外部服务集成注意

### Sensenova Vision Provider

sensenova (`https://token.sensenova.cn/v1`) 已配置为 litellm vision 供应商：

- **vision 档**: `openai/sensenova-6.7-flash-lite` — 视觉模型，OpenAI 协议直连
- **关键配置坑**（已解决）:
  1. litellm config 里 model 名必须带 `openai/` 前缀
  2. `api_base` 必须带 `/v1` 后缀
  3. `gateway.sh` 的 `load_secrets()` 必须导出 `SENSENOVA_API_KEY`
  4. `config_ops.py` 的 `model_label()` 需去 `openai/` 前缀

### 集成测试

`tests/UniClaw.Core.Tests/UniBrain/RealVisionIntegrationTests.cs` 使用 inline `OpenAICompatVisionProvider` 直连 sensenova（不走 litellm gateway），默认 Skip。手动去掉 Skip 可跑。

## 来源

- Memory: [[trace-vision]], [[sensenova-vision-provider]]
- 相关: [[litellmbar-maintenance]]（配置改动在 tools/litellm-bar/ 做，跑 install.sh 部署）
