# E2E测试执行流程图

## 主流程

```mermaid
graph TD
    A[开始E2E测试] --> B[加载测试数据]
    B --> C{验证测试数据}
    C -->|失败| D[报错退出]
    C -->|成功| E[初始化Mock组件]
    
    E --> F[创建SimulationRunner]
    F --> G[启动DFS遍历]
    
    G --> H{还有未访问元素?}
    H -->|是| I[选择下一个元素]
    H -->|否| P[遍历完成]
    
    I --> J{元素类型}
    J -->|navigate| K[导航到新页面]
    J -->|toggle| L[执行切换操作]
    J -->|其他| M[执行默认操作]
    
    K --> N[记录TraceStep]
    L --> N
    M --> N
    
    N --> O[更新访问状态]
    O --> H
    
    P --> Q[生成SimulationResult]
    Q --> R[执行断言验证]
    
    R --> S{断言通过?}
    S -->|是| T[生成测试报告]
    S -->|否| U[记录失败原因]
    
    T --> V[保存多种格式]
    U --> V
    
    V --> W[输出测试结果]
    W --> X[结束]
    
    style A fill:#e1f5e1
    style T fill:#e1f5e1
    style X fill:#e1f5e1
    style D fill:#f8e8e8
    style U fill:#f8e8e8
```

## 数据转换流程

```mermaid
graph LR
    A[测试数据] --> B[MockVisionService]
    A --> C[MockActionExecutor]
    A --> D[PageAnalyzer]
    
    B --> E[SimulationRunner]
    C --> E
    D --> E
    
    E --> F[InMemoryTracer]
    F --> G[TraceStep对象]
    
    G --> H[to_dict转换]
    H --> I[Dict格式]
    
    I --> J[TraceAsserter]
    J --> K[自然语言事件]
    
    K --> L[断言匹配]
    L --> M[AssertionResult]
    
    M --> N[报告生成器]
    N --> O1[TXT]
    N --> O2[HTML]
    N --> O3[JSONL]
    N --> O4[Mermaid]
    
    style A fill:#e3f2fd
    style E fill:#fff3e0
    style G fill:#f3e5f5
    style K fill:#e8f5e8
    style M fill:#fce4ec
```

## 断言验证流程

```mermaid
graph TD
    A[接收TraceStep序列] --> B[转换为自然语言事件]
    B --> C[提取预期事件序列]
    
    C --> D[子序列匹配检查]
    D --> E{预期事件在实际中?}
    
    E -->|是| F[统计匹配事件]
    E -->|否| G[记录缺失事件]
    
    F --> H[检查步数范围]
    G --> H
    
    H --> I{步数在范围内?}
    I -->|是| J[检查完成原因]
    I -->|否| K[记录步数违规]
    
    J --> L{完成原因匹配?}
    L -->|是| M[检查违规项]
    L -->|否| N[记录原因违规]
    
    M --> O{发现违规关键词?}
    O -->|是| P[记录违规项]
    O -->|否| Q[断言通过]
    
    K --> R[断言失败]
    N --> R
    P --> R
    
    Q --> S[生成AssertionResult]
    R --> S
    
    S --> T[返回测试结果]
    
    style Q fill:#e1f5e1
    style T fill:#e1f5e1
    style R fill:#f8e8e8
```

## 报告生成流程

```mermaid
graph TD
    A[SimulationResult] --> B{选择报告格式}
    
    B -->|TXT| C[文本报告生成器]
    B -->|HTML| D[HTML报告生成器]
    B -->|JSONL| E[JSONL导出器]
    B -->|Mermaid| F[Mermaid图生成器]
    B -->|ASCII| G[ASCII树生成器]
    
    C --> H[处理统计信息]
    D --> I[渲染HTML模板]
    E --> J[序列化JSON]
    F --> K[生成状态图语法]
    G --> L[递归渲染树]
    
    H --> M[添加事件列表]
    I --> M
    J --> M
    K --> M
    L --> M
    
    M --> N[格式化输出]
    N --> O[保存文件]
    
    style A fill:#e3f2fd
    style O fill:#e1f5e1
```

## DFS遍历算法流程

```mermaid
graph TD
    A[开始DFS遍历] --> B[初始化: current_path = [], visited = {}]
    B --> C[访问root节点]
    
    C --> D{遍历深度 < max_depth?}
    D -->|否| E[触发go_back]
    D -->|是| F{当前页面有未访问元素?}
    
    E --> Q{path栈为空?}
    Q -->|是| R[遍历完成]
    Q -->|否| S[弹出栈顶元素]
    S --> T[更新current_path]
    T --> D
    
    F -->|否| E
    F -->|是| G[获取下一个未访问元素]
    
    G --> H{元素类型判断}
    H -->|navigate| I[current_path.append元素名]
    H -->|toggle| J[执行切换操作]
    H -->|其他| K[执行默认操作]
    
    I --> L[记录navigate事件]
    J --> M[记录toggle事件]
    K --> N[记录action事件]
    
    L --> O[标记元素已访问]
    M --> O
    N --> O
    
    O --> P[返回D继续遍历]
    P --> D
    
    R --> S1[返回遍历结果]
    
    style A fill:#e1f5e1
    style R fill:#e1f5e1
    style S1 fill:#e1f5e1
    style E fill:#fff3e0
```

## 事件转换规则流程

```mermaid
graph TD
    A[TraceStep.to_dict] --> B[基础字段映射]
    B --> C[特殊字段转换]
    
    C --> D{action字段处理}
    D --> E[action_type = action or click]
    
    E --> F{node_id字段处理}
    F --> G[current_node = node_id]
    
    G --> H{screen_info处理}
    H --> I[target_info.element_id = screen_info.target]
    I --> J[target_info.element_type = screen_info.element_type]
    
    J --> K[metadata处理]
    K --> L[completion_reason = metadata.completion_reason]
    
    L --> M[返回Dict格式]
    
    M --> N[TraceAsserter.step_to_nl]
    N --> O{action_type判断}
    
    O --> P{特殊规则匹配}
    P -->|navigate + Settings| Q[返回: 点击 'Settings' 按钮]
    P -->|toggle + slider| R[返回: 操作 'slider' 滑块并恢复]
    P -->|go_back + root| S[返回: 遍历完成]
    P -->|其他| T[通用描述生成]
    
    Q --> U[自然语言事件]
    R --> U
    S --> U
    T --> U
    
    style A fill:#e3f2fd
    style M fill:#f3e5f5
    style U fill:#e8f5e8
```

## 测试失败诊断流程

```mermaid
graph TD
    A[测试失败] --> B{失败类型}
    
    B -->|事件不匹配| C[检查事件描述]
    B -->|步数超出范围| D[检查遍历逻辑]
    B -->|完成原因错误| E[检查completion_reason]
    B -->|发现违规项| F[检查trace内容]
    
    C --> G[打印实际事件列表]
    G --> H[对比预期事件格式]
    H --> I{格式是否一致?}
    I -->|否| J[修正test_case.json]
    I -->|是| K[检查step_to_nl规则]
    
    D --> L[检查max_depth设置]
    L --> M[检查should_go_back逻辑]
    M --> N[验证元素识别正确性]
    
    E --> O[检查最后一步TraceStep]
    O --> P[确认metadata.completion_reason]
    P --> Q[验证_log_trace_step调用]
    
    F --> R[分析违规关键词]
    R --> S[定位违规TraceStep]
    S --> T[检查screen_info内容]
    
    J --> U[重新测试]
    K --> U
    N --> U
    Q --> U
    T --> U
    
    U --> V{测试通过?}
    V -->|是| W[问题解决]
    V -->|否| A
    
    style W fill:#e1f5e1
    style A fill:#f8e8e8
```

## 关键决策点

### 1. 遍历决策
```
should_go_back() 判断:
├── 深度 >= max_depth? → YES, 返回
├── 所有元素已访问? → YES, 返回
├── 存在可交互元素? → NO, 返回
└── 默认 → NO, 继续深入
```

### 2. 事件描述规则优先级
```
step_to_nl() 决策:
├── 特殊规则匹配 → navigate+Settings → "点击 'Settings' 按钮"
├── 通用动作处理 → toggle+restore → "操作 'X' 并恢复"
├── 状态特殊处理 → go_back+root → "遍历完成"
└── 默认描述 → action_type + current_node
```

### 3. 报告格式选择
```
报告格式决策:
├── 调试阶段 → TXT + ASCII (快速查看)
├── 演示阶段 → HTML + Mermaid (可视化)
├── 数据分析 → JSONL (机器处理)
└── 存档阶段 → 全部格式 (完整记录)
```

---

**使用说明**: 这些流程图展示了E2E测试系统的核心流程和决策逻辑，可以帮助理解系统运作机制。配合 [TESTING_ARCHITECTURE.md](TESTING_ARCHITECTURE.md) 和 [TESTING_QUICK_REFERENCE.md](TESTING_QUICK_REFERENCE.md) 使用效果更佳。