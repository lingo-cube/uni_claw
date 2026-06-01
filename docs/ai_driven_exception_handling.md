# AI 驱动的异常处理设计

> 利用 AI 理解当前状态和截图，做出智能的异常处理决策

---

## 一、核心概念

### 1.1 传统异常处理的问题

```python
# 传统方式：硬编码规则
if "no devices" in error_message:
    reconnect_adb()
elif "element not found" in error_message:
    retry_with_ai()
elif "popup detected" in error_message:
    close_popup()
# ... 规则越来越多，难以维护
```

**问题：**
- 规则固化，无法应对新场景
- 无法理解"为什么"出错
- 恢复策略单一

### 1.2 AI 驱动的异常处理

```python
# AI 方式：理解上下文，智能决策
ai_handler.handle_exception(exception_context)
# AI 分析：
# 1. 当前在哪（状态树路径）
# 2. 截图显示什么（视觉理解）
# 3. 出了什么问题（异常类型）
# 4. 应该怎么恢复（智能决策）
```

**优势：**
- 理解上下文，做出合理决策
- 可以应对未预见的异常场景
- 决策可解释

---

## 二、AI 异常处理器设计

### 2.1 AI 异常处理器

```python
class AIDrivenExceptionHandler(ExceptionHandler):
    """AI 驱动的异常处理器"""

    def __init__(self, vision_service, max_retries: int = 3):
        self.vision = vision_service
        self.max_retries = max_retries
        self.decision_history: List[AIDecision] = []

    def can_handle(self, context: ExceptionContext) -> bool:
        """优先使用 AI 处理所有可恢复的异常"""
        return (
            context.severity in [ExceptionSeverity.ERROR, ExceptionSeverity.CRITICAL]
            and context.retry_count < self.max_retries
        )

    def handle(self, context: ExceptionContext) -> ExceptionHandlingResult:
        """使用 AI 分析并处理异常"""
        # 1. 收集上下文信息
        analysis_input = self._build_analysis_input(context)

        # 2. AI 分析
        ai_decision = self._analyze_with_ai(analysis_input)
        self.decision_history.append(ai_decision)

        # 3. 执行 AI 决策
        return self._execute_decision(ai_decision, context)

    def _build_analysis_input(self, context: ExceptionContext) -> dict:
        """构建 AI 分析输入"""
        current_node = context.node
        state_machine = context.state_machine

        return {
            # 异常信息
            "exception_type": type(context.exception).__name__,
            "exception_message": str(context.exception),

            # 当前状态
            "current_state": context.state.value,
            "current_path": [n.name for n in state_machine.current_path],
            "current_level": len(state_machine.current_path) - 1,

            # 目标信息
            "target_node": {
                "name": current_node.name if current_node else None,
                "type": current_node.node_type.value if current_node else None,
                "coordinates": current_node.coordinates if current_node else None,
            } if current_node else None,

            # 状态树结构
            "state_tree": self._serialize_tree(state_machine.root),

            # 可用的导航选项
            "navigation_options": self._get_navigation_options(state_machine),

            # 重试信息
            "retry_count": context.retry_count,
            "max_retries": self.max_retries,
        }

    def _analyze_with_ai(self, input_data: dict) -> AIDecision:
        """使用 AI 分析异常并给出决策"""

        # 构建 AI 提示词
        prompt = self._build_ai_prompt(input_data)

        # 获取当前截图
        screenshot = self._capture_screenshot()

        # 调用 AI 分析
        response = self.vision.analyze_with_context(
            prompt=prompt,
            image=screenshot,
            context=input_data
        )

        # 解析 AI 决策
        return AIDecision.from_ai_response(response)

    def _build_ai_prompt(self, input_data: dict) -> str:
        """构建 AI 分析提示词"""
        return f"""你是遍历系统的智能异常处理助手。

## 当前情况
- 异常类型: {input_data['exception_type']}
- 异常信息: {input_data['exception_message']}
- 当前状态: {input_data['current_state']}
- 当前路径: {' -> '.join(input_data['current_path']) if input_data['current_path'] else '无'}
- 重试次数: {input_data['retry_count']}/{input_data['max_retries']}

## 目标信息
{self._format_target(input_data.get('target_node'))}

## 状态树结构
{self._format_tree(input_data['state_tree'])}

## 可用的导航选项
{self._format_options(input_data['navigation_options'])}

## 你的任务
分析当前情况，给出最佳处理方案。返回 JSON 格式：

{{
  "analysis": "简要分析问题原因",
  "decision": "RETRY|SKIP|BACKTRACK|RECOVER|NAVIGATE",
  "reason": "决策理由",
  "action_params": {{
    // 如果是 NAVIGATE，指定目标路径
    "target_path": ["节点1", "节点2"],

    // 如果是 RECOVER，指定恢复动作
    "recovery_action": "RECONNECT_ADB|RESTART_APP|RESTORE_POSITION",

    // 如果是 SKIP，指定跳到哪个节点
    "skip_to": "节点名称"
  }}
}}

## 决策原则
1. 优先保证遍历继续进行
2. 如果当前节点暂时不可访问，跳过它继续后续节点
3. 如果路径错误，尝试导航到正确位置
4. 如果是临时问题（加载中、动画），短暂等待后重试
5. 如果是致命问题（APP崩溃、设备离线），标记失败并回退
"""
```

### 2.2 AI 决策数据结构

```python
@dataclass
class AIDecision:
    """AI 的异常处理决策"""
    analysis: str              # 问题分析
    decision: str              # 决策类型
    reason: str                # 决策理由
    action_params: dict        # 动作参数
    confidence: float = 0.0    # 置信度

    @classmethod
    def from_ai_response(cls, response: str) -> 'AIDecision':
        """从 AI 响应解析决策"""
        try:
            data = json.loads(response)
            return cls(
                analysis=data.get('analysis', ''),
                decision=data.get('decision', 'SKIP'),
                reason=data.get('reason', ''),
                action_params=data.get('action_params', {}),
                confidence=data.get('confidence', 0.0)
            )
        except json.JSONDecodeError:
            # AI 响应无效，使用默认决策
            return cls(
                analysis='AI 响应无效',
                decision='SKIP',
                reason='无法解析 AI 响应，使用安全策略',
                action_params={},
                confidence=0.0
            )
```

---

## 三、AI 决策类型

### 3.1 决策类型定义

| 决策类型 | 说明 | 使用场景 | 参数 |
|----------|------|----------|------|
| **RETRY** | 重试当前操作 | 临时问题（加载中、动画） | wait_time |
| **SKIP** | 跳过当前节点 | 节点不可访问 | skip_to (目标节点) |
| **BACKTRACK** | 回退到上级 | 当前路径无法继续 | backtrack_level |
| **NAVIGATE** | 导航到指定路径 | 路径错误，需要重新定位 | target_path |
| **RECOVER** | 执行恢复动作 | 设备/APP 异常 | recovery_action |
| **WAIT_AND_RETRY** | 等待后重试 | 需要等待加载 | wait_time |

### 3.2 AI 决策示例

```json
// 场景1：元素正在加载，等待后重试
{
  "analysis": "目标元素'移动数据'存在但处于禁用状态，可能是正在加载",
  "decision": "WAIT_AND_RETRY",
  "reason": "元素当前不可交互，等待加载完成后重试",
  "action_params": {"wait_time": 2},
  "confidence": 0.9
}

// 场景2：路径错误，需要重新导航
{
  "analysis": "当前在'DiPilot'菜单，但目标在'DiLink'菜单",
  "decision": "NAVIGATE",
  "reason": "位置不匹配，需要切换到正确的菜单",
  "action_params": {"target_path": ["车辆设置", "DiLink", "互联"]},
  "confidence": 0.95
}

// 场景3：节点不存在，跳过继续
{
  "analysis": "目标节点'已删除功能'在当前界面不存在，可能已被移除",
  "decision": "SKIP",
  "reason": "节点不存在，跳过它继续后续节点",
  "action_params": {"skip_to": "下一个节点"},
  "confidence": 0.85
}

// 场景4：弹窗遮挡，先处理弹窗
{
  "analysis": "检测到弹窗'系统提示'遮挡了目标元素",
  "decision": "RECOVER",
  "reason": "需要先关闭弹窗才能继续",
  "action_params": {"recovery_action": "CLOSE_POPUP"},
  "confidence": 0.92
}

// 场景5：APP 卡死，重启恢复
{
  "analysis": "界面无响应超过5秒，APP 可能卡死",
  "decision": "RECOVER",
  "reason": "APP 无响应，需要重启",
  "action_params": {"recovery_action": "RESTART_APP"},
  "confidence": 0.88
}
```

---

## 四、AI 决策执行

### 4.1 决策执行器

```python
class AIDecisionExecutor:
    """执行 AI 的决策"""

    def __init__(self, state_machine: HierarchicalStateMachine):
        self.sm = state_machine

    def execute(self, decision: AIDecision, context: ExceptionContext) -> ExceptionHandlingResult:
        """执行 AI 决策"""
        if decision.decision == "RETRY":
            return self._execute_retry(decision, context)
        elif decision.decision == "SKIP":
            return self._execute_skip(decision, context)
        elif decision.decision == "BACKTRACK":
            return self._execute_backtrack(decision, context)
        elif decision.decision == "NAVIGATE":
            return self._execute_navigate(decision, context)
        elif decision.decision == "RECOVER":
            return self._execute_recover(decision, context)
        elif decision.decision == "WAIT_AND_RETRY":
            return self._execute_wait_and_retry(decision, context)
        else:
            # 未知决策，使用安全策略
            return self._execute_safe_default(context)

    def _execute_retry(self, decision: AIDecision, context: ExceptionContext) -> ExceptionHandlingResult:
        """执行重试"""
        wait_time = decision.action_params.get('wait_time', 1)
        if wait_time > 0:
            time.sleep(wait_time)

        return ExceptionHandlingResult(
            action=ExceptionAction.RETRY,
            new_state=context.state,
            message=f"AI 建议: {decision.reason}"
        )

    def _execute_skip(self, decision: AIDecision, context: ExceptionContext) -> ExceptionHandlingResult:
        """执行跳过"""
        if context.node:
            context.node.state = NodeState.SKIPPED
            context.node.last_error = f"AI 跳过: {decision.reason}"

        # 查找下一个节点
        next_node = self.sm.find_next_unvisited_node()
        if next_node:
            self.sm.descend_to(next_node)
            return ExceptionHandlingResult(
                action=ExceptionAction.SKIP,
                new_state=TraversalState.TRAVERSING_ITEM,
                message=f"跳过当前节点，切换到: {next_node.name}"
            )

        # 没有下一个节点，回退
        return ExceptionHandlingResult(
            action=ExceptionAction.BACKTRACK,
            new_state=TraversalState.RECOVERING,
            message="当前分支无更多节点"
        )

    def _execute_backtrack(self, decision: AIDecision, context: ExceptionContext) -> ExceptionHandlingResult:
        """执行回退"""
        level = decision.action_params.get('backtrack_level', 1)
        if context.node:
            context.node.state = NodeState.FAILED
            context.node.last_error = f"AI 回退: {decision.reason}"

        self.sm.retreat_to_level(level)
        return ExceptionHandlingResult(
            action=ExceptionAction.BACKTRACK,
            new_state=TraversalState.RECOVERING,
            message=f"回退到层级 {level}"
        )

    def _execute_navigate(self, decision: AIDecision, context: ExceptionContext) -> ExceptionHandlingResult:
        """执行导航"""
        target_path = decision.action_params.get('target_path', [])
        if not target_path:
            return ExceptionHandlingResult(
                action=ExceptionAction.TERMINATE,
                new_state=TraversalState.ERROR,
                message="AI 决策缺少目标路径"
            )

        success = self.sm.navigate_to_path(target_path)
        if success:
            return ExceptionHandlingResult(
                action=ExceptionAction.RECOVER,
                new_state=TraversalState.TRAVERSING_ITEM,
                message=f"已导航到: {'/'.join(target_path)}"
            )

        return ExceptionHandlingResult(
            action=ExceptionAction.BACKTRACK,
            new_state=TraversalState.RECOVERING,
            message="导航失败，尝试回退"
        )

    def _execute_recover(self, decision: AIDecision, context: ExceptionContext) -> ExceptionHandlingResult:
        """执行恢复"""
        recovery_action = decision.action_params.get('recovery_action', '')

        if recovery_action == "RECONNECT_ADB":
            success = self._reconnect_adb()
        elif recovery_action == "RESTART_APP":
            success = self._restart_app()
        elif recovery_action == "RESTORE_POSITION":
            success = self._restore_position()
        elif recovery_action == "CLOSE_POPUP":
            success = self._close_popup()
        else:
            success = False

        if success:
            return ExceptionHandlingResult(
                action=ExceptionAction.RECOVER,
                new_state=TraversalState.TRAVERSING_ITEM,
                message=f"恢复成功: {recovery_action}"
            )

        return ExceptionHandlingResult(
            action=ExceptionAction.BACKTRACK,
            new_state=TraversalState.RECOVERING,
            message=f"恢复失败: {recovery_action}"
        )

    def _execute_wait_and_retry(self, decision: AIDecision, context: ExceptionContext) -> ExceptionHandlingResult:
        """等待后重试"""
        wait_time = decision.action_params.get('wait_time', 2)
        time.sleep(wait_time)

        return ExceptionHandlingResult(
            action=ExceptionAction.RETRY,
            new_state=context.state,
            message=f"等待 {wait_time}s 后重试"
        )
```

---

## 五、完整集成

### 5.1 更新异常处理链

```python
class ExceptionHandlingChain:
    """更新后的异常处理链"""

    def __init__(self, vision_service, state_machine):
        # AI 处理器放在优先位置
        self.handlers: List[ExceptionHandler] = [
            FatalExceptionHandler(),                    # 1. 致命异常（最高优先级）
            AIDrivenExceptionHandler(vision_service),   # 2. AI 驱动处理（核心）
            DeviceExceptionHandler(),                   # 3. 设备异常（兜底）
            UIExceptionHandler(),                       # 4. 界面异常（兜底）
            RetryHandler(max_retries=1),                # 5. 简单重试（AI 后的兜底）
            BacktrackHandler(max_retries=3),            # 6. 回退处理（最后手段）
        ]
        self.executor = AIDecisionExecutor(state_machine)
```

### 5.2 AI 处理器完整实现

```python
class AIDrivenExceptionHandler(ExceptionHandler):
    """完整的 AI 驱动异常处理器"""

    def handle(self, context: ExceptionContext) -> ExceptionHandlingResult:
        """使用 AI 分析并处理异常"""
        # 1. 构建分析输入
        analysis_input = self._build_analysis_input(context)

        # 2. AI 分析
        ai_decision = self._analyze_with_ai(analysis_input)

        # 3. 记录决策（用于学习和调试）
        self._log_decision(ai_decision, context)

        # 4. 执行决策
        return self._execute_decision(ai_decision, context)

    def _execute_decision(self, decision: AIDecision, context: ExceptionContext) -> ExceptionHandlingResult:
        """执行 AI 决策"""
        result = self.executor.execute(decision, context)

        # 记录执行结果
        self.decision_history.append(AIDecisionRecord(
            decision=decision,
            context=context,
            result=result,
            timestamp=datetime.now()
        ))

        return result
```

---

## 六、AI 学习与优化

### 6.1 决策历史记录

```python
@dataclass
class AIDecisionRecord:
    """AI 决策记录"""
    decision: AIDecision
    context: ExceptionContext
    result: ExceptionHandlingResult
    timestamp: datetime
    final_outcome: str = ""  # 最终结果（后续更新）

class AIDecisionLearner:
    """AI 决策学习器"""

    def __init__(self):
        self.decision_records: List[AIDecisionRecord] = []

    def record_decision(self, record: AIDecisionRecord):
        """记录决策"""
        self.decision_records.append(record)

    def analyze_effectiveness(self) -> dict:
        """分析决策有效性"""
        total = len(self.decision_records)
        by_decision = defaultdict(list)
        by_outcome = defaultdict(int)

        for record in self.decision_records:
            by_decision[record.decision.decision].append(record)
            by_outcome[record.final_outcome] += 1

        return {
            "total_decisions": total,
            "by_decision": {
                decision: len(records)
                for decision, records in by_decision.items()
            },
            "by_outcome": dict(by_outcome),
            "success_rate": by_outcome.get("success", 0) / total if total > 0 else 0,
        }

    def get_feedback_for_learning(self) -> List[dict]:
        """获取用于学习的数据"""
        feedback = []
        for record in self.decision_records:
            feedback.append({
                "exception_type": type(record.context.exception).__name__,
                "decision": record.decision.decision,
                "reason": record.decision.reason,
                "outcome": record.final_outcome,
                "context": {
                    "current_path": [n.name for n in record.context.state_machine.current_path],
                    "retry_count": record.context.retry_count,
                }
            })
        return feedback
```

### 6.2 反馈循环

```python
class AIDecisionFeedback:
    """AI 决策反馈系统"""

    def update_decision_outcome(self, record: AIDecisionRecord, outcome: str):
        """更新决策结果"""
        record.final_outcome = outcome

        # 如果决策效果不好，记录用于改进
        if outcome == "failure":
            self._record_failed_decision(record)

    def _record_failed_decision(self, record: AIDecisionRecord):
        """记录失败决策，用于改进"""
        # 可以用来：
        # 1. 分析失败模式
        # 2. 调整决策权重
        # 3. 生成训练数据
        pass

    def generate_improvement_prompt(self) -> str:
        """生成改进提示词"""
        failed_cases = [r for r in self.decision_records if r.final_outcome == "failure"]

        if not failed_cases:
            return ""

        prompt = "以下是一些失败的决策案例，请分析原因并给出改进建议：\n\n"
        for case in failed_cases[:5]:  # 最多展示5个
            prompt += f"## 案例 {case.timestamp}\n"
            prompt += f"- 异常: {type(case.context.exception).__name__}\n"
            prompt += f"- AI 决策: {case.decision.decision}\n"
            prompt += f"- AI 理由: {case.decision.reason}\n"
            prompt += f"- 结果: {case.result.message}\n"
            prompt += f"- 最终结果: {case.final_outcome}\n\n"

        prompt += "\n请分析这些失败案例，给出改进建议。"
        return prompt
```

---

## 七、使用示例

### 7.1 基础使用

```python
# 初始化（自动集成 AI 处理器）
sm = HierarchicalStateMachine(vision_service=vision)

# 正常使用（异常自动由 AI 处理）
sm.click_node(target_node)

# AI 自动分析异常并决策
# 例如：
# - 元素正在加载 → WAIT_AND_RETRY
# - 路径错误 → NAVIGATE
# - 节点不存在 → SKIP
```

### 7.2 查看 AI 决策历史

```python
# 查看 AI 的所有决策
for record in sm.ai_handler.decision_history:
    print(f"{record.timestamp}: {record.decision.decision}")
    print(f"  分析: {record.decision.analysis}")
    print(f"  理由: {record.decision.reason}")
    print(f"  结果: {record.result.message}")

# 分析决策有效性
stats = sm.ai_learner.analyze_effectiveness()
print(f"成功率: {stats['success_rate']:.1%}")
print(f"决策分布: {stats['by_decision']}")
```

### 7.3 反馈与改进

```python
# 标记决策结果（可以自动或手动）
sm.mark_decision_outcome(record_id, "success")

# 生成改进建议
improvement_prompt = sm.ai_feedback.generate_improvement_prompt()
if improvement_prompt:
    # 可以用来优化 AI 提示词
    print(improvement_prompt)
```

---

## 八、配置与调优

### 8.1 AI 异常处理配置

```python
@dataclass
class AIExceptionHandlingConfig:
    """AI 异常处理配置"""

    # 启用/禁用
    enabled: bool = True

    # 最大重试次数
    max_retries: int = 3

    # 决策置信度阈值
    confidence_threshold: float = 0.6

    # 超时时间
    analysis_timeout: int = 30  # 秒

    # 学习模式
    learning_mode: bool = True

    # 决策记录
    record_decisions: bool = True

    # 回退策略（AI 失败时）
    fallback_to_rule_based: bool = True
```

### 8.2 性能优化

```python
class CachedAIDecisionHandler(AIDrivenExceptionHandler):
    """带缓存的 AI 决策处理器"""

    def __init__(self, vision_service, cache_size: int = 1000):
        super().__init__(vision_service)
        self.decision_cache = LRUCache(maxsize=cache_size)

    def handle(self, context: ExceptionContext) -> ExceptionHandlingResult:
        """优先使用缓存的决策"""
        # 生成缓存键
        cache_key = self._generate_cache_key(context)

        # 查找缓存
        if cache_key in self.decision_cache:
            cached_decision = self.decision_cache[cache_key]
            return self._execute_decision(cached_decision, context)

        # 调用 AI 分析
        result = super().handle(context)

        # 缓存决策
        if result.confidence > 0.8:
            self.decision_cache[cache_key] = result.decision

        return result

    def _generate_cache_key(self, context: ExceptionContext) -> str:
        """生成缓存键"""
        return (
            f"{type(context.exception).__name__}_"
            f"{context.state.value}_"
            f"{'_'.join(n.name for n in context.state_machine.current_path)}_"
            f"{context.retry_count}"
        )
```

---

## 九、总结

### AI 驱动异常处理的优势

| 方面 | 传统规则 | AI 驱动 |
|------|----------|---------|
| **适应性** | 固化规则，新场景需添加规则 | 理解上下文，自动适应 |
| **决策质量** | 基于预定义条件 | 基于视觉和状态理解 |
| **可维护性** | 规则增多难以维护 | 提示词集中管理 |
| **可解释性** | 决策过程隐式 | AI 给出分析理由 |
| **学习能力** | 无 | 可从失败中学习 |

### 关键设计点

1. **上下文感知** - AI 理解当前状态树 + 截图
2. **决策可解释** - AI 给出分析理由
3. **学习反馈** - 记录决策结果，持续优化
4. **性能优化** - 缓存高频决策
5. **兜底策略** - AI 失败时回退到规则

### 与状态机集成

```
异常发生
  ↓
构建 ExceptionContext (包含状态树、截图等)
  ↓
AI 分析 (理解上下文 + 视觉理解)
  ↓
AI 决策 (RETRY/SKIP/NAVIGATE/...)
  ↓
执行决策 (触发状态转换)
  ↓
继续遍历/回退/终止
```
