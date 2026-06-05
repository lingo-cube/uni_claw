# Uni-Claw 统一测试流程图

**版本**: 1.0  
**创建**: 2026-06-05  
**目的**: 可视化统一测试方案的完整流程

---

## 🎯 核心流程图

```mermaid
flowchart TD
    A[代码变更] --> B[触发测试]
    B --> C{选择测试范围}
    
    C -->|v6开发| D[UnifiedTestCoordinator<br/>python scripts/unified_test_coordinator.py v6]
    C -->|完整验证| E[UnifiedTestCoordinator<br/>python scripts/unified_test_coordinator.py all]
    C -->|CI/CD| F[UnifiedTestCoordinator<br/>python scripts/unified_test_coordinator.py ci]
    C -->|单元测试| G[UnifiedTestCoordinator<br/>python scripts/unified_test_coordinator.py unit]
    
    D --> H[执行pytest tests/v6/ -v]
    E --> I[执行pytest tests/v6/, tests/integration/, tests/models/]
    F --> J[执行pytest 关键V6测试]
    G --> K[执行pytest tests/v6/, tests/models/]
    
    H --> L[解析pytest输出<br/>parse_pytest_output]
    I --> L
    J --> L
    K --> L
    
    L --> M[生成结构化数据<br/>test_results字典]
    
    M --> N[自动生成validation报告]
    
    N --> O[生成unit_test_status.md<br/>📝 docs/validation/unit_test_status.md]
    N --> P[生成integration_test_status.md<br/>📝 docs/validation/integration_status_test_status.md]
    
    O --> Q[更新测试汇总<br/>summary: total, passed, failed, skipped]
    P --> Q
    
    Q --> R[显示终端摘要<br/>📊 终端显示测试结果]
    
    R --> S{有失败?}
    S -->|是| T[提示: 查看validation文档<br/>使用module-test技能处理]
    S -->|否| U[✅ 所有测试通过]
    
    T --> V[📋 更新工作流]
    U --> V
    
    V --> W[提交变更]
    
    style A fill:#e1f5ff
    style D fill:#fff4e6
    style E fill:#fff4e6
    style F fill:#fff4e6
    style G fill:#fff4e6
    style H fill:#d4edda
    style I fill:#d4edda
    style J fill:#d4edda
    style K fill:#d4edda
    style L fill:#ffd1dc
    style M fill:#f9f9d9
    style N fill:#f9f9d9
    style O fill:#e8f5e8
    style P fill:#e8f5e8
    style Q fill:#f9f9d9
    style R fill:#ffd1dc
    style T fill:#ffeaa7
    style U fill:#90ee90
    style V fill:#e1f5ff
    style W fill:#fff4e6
```

---

## 🔄 详细阶段分解

### 阶段1: 测试触发阶段

```mermaid
graph LR
    A[代码变更] --> B{变更类型}
    B -->|功能开发| C[选择v6范围]
    B -->|完整验证| D[选择all范围]
    B -->|CI/CD| E[选择ci范围]
    
    C --> F[执行: unified_test_coordinator.py v6]
    D --> F
    E --> F
    
    style A fill:#e1f5ff
    style F fill:#ffd1dc
```

### 阶段2: 测试执行阶段

```mermaid
graph LR
    A[UnifiedTestCoordinator] --> B[检测测试范围]
    B --> C[v6测试]
    B --> D[单元测试]
    B --> E[集成测试]
    
    C --> F[pytest tests/v6/ -v]
    D --> G[pytest tests/v6/, tests/models/]
    E --> H[pytest tests/integration/, tests/v6/test_examples.py]
    
    F --> I[pytest输出流]
    G --> I
    H --> I
    
    I --> J[parse_pytest_output解析]
    J --> K[结构化test_results]
    
    style A fill:#fff4e6
    style J fill:#d4edda
    style K fill:#ffd1dc
```

### 阶段3: 数据处理阶段

```mermaid
graph LR
    A[test_results原始数据] --> B[按模块分类]
    B --> C[simulation模块]
    B --> D[state_machine模块]
    B --> E[graph_engine模块]
    B --> F[integration模块]
    
    C --> G[计算模块统计]
    D --> G
    E --> G
    F --> G
    
    G --> H[生成汇总summary]
    H --> I[计算整体通过率]
    
    I --> J[完整test_results字典]
    
    style A fill:#e1f5ff
    style J fill:#ffd1dc
```

### 阶段4: Validation报告生成阶段

```mermaid
graph LR
    A[test_results] --> B[调用_generate_validation_reports]
    
    B --> C{检查数据类型}
    C --> D[单元测试数据]
    C --> E[集成测试数据]
    
    D --> F[_generate_unit_test_status]
    E --> G[_generate_integration_test_status]
    
    F --> H[读取现有unit_test_status.md]
    G --> I[读取现有integration_test_status.md]
    
    H --> J[累积式更新<br/>合并新结果]
    I --> K[累积式更新<br/>合并新结果]
    
    J --> L[保存unit_test_status.md]
    K --> M[保存integration_test_status.md]
    
    style A fill:#fff4e6
    style B fill:#ffd1dc
    style J fill:#90ee90
    style K fill:#90ee90
```

### 阶段5: 结果展示阶段

```mermaid
graph LR
    A[validation报告生成] --> B[终端显示摘要]
    
    B --> C[显示总测试数]
    B --> D[显示通过/失败/跳过]
    B --> E[显示通过率]
    
    C --> F{导出JSON?}
    D --> F
    E --> F
    
    F -->|是| G[export_json_report<br/>生成test_results.json]
    F -->|否| H[仅生成markdown]
    
    G --> I{更新Dashboard?}
    H --> I
    
    I -->|是| J[update_dashboard_data<br/>生成dashboard_data.json]
    I -->|否| K[完成]
    
    J --> L[Dashboard读取真实数据]
    
    C --> M{有失败?}
    E --> M
    
    M -->|是| N[提示处理方案]
    M -->|否| O[✅ 任务完成]
    
    style A fill:#e8f5e8
    style L fill:#ffd1dc
    style O fill:#90ee90
```

---

## 📋 不同使用场景的流程

### 场景1: V6功能开发

```mermaid
flowchart TD
    A[开发V6功能] --> B[本地测试]
    B --> C[python unified_test_coordinator.py v6]
    C --> D[查看validation报告]
    D --> E{测试通过?}
    E -->|是| F[提交PR]
    E -->|否| G[修复问题]
    G --> B
    
    F --> H[GitHub Actions运行CI测试]
    H --> I[python unified_test_coordinator.py ci]
    I --> J[自动更新validation报告]
    J --> K[PR合并]
    
    style A fill:#e1f5ff
    style B fill:#ffd1dc
    style C fill:#ffd1dc
    style K fill:#90ee90
```

### 场景2: CI/CD自动化

```mermaid
flowchart TD
    A[代码推送] --> B[GitHub Actions触发]
    B --> C[安装依赖]
    C --> D[python unified_test_coordinator.py ci]
    D --> E[导出JSON报告]
    E --> F[更新dashboard数据]
    F --> G{测试通过?}
    G -->|是| H[✅ CI通过]
    G -->|否| I[❌ CI失败]
    
    H --> J[生成validation报告]
    I --> K[阻止合并]
    
    style A fill:#fff4e6
    style J fill:#e8f5e8
    style K fill:#ffeaa7
```

### 场景3: 完整验证

```mermaid
flowchart TD
    A[准备发布/重大变更] --> B[运行完整测试]
    B --> C[python unified_test_coordinator.py all]
    C --> D[自动生成所有validation报告]
    D --> E[查看完整结果]
    E --> F{结果满意?}
    F -->|是| G[✅ 发布]
    F -->|否| H[调整修复]
    H --> B
    
    style A fill:#fff4e6
    style C fill:#ffd1dc
    style G fill:#90ee90
```

---

## 🔍 数据转换流程图

### pytest原始输出 → 结构化数据

```mermaid
graph LR
    A[pytest原始输出] --> B[示例: tests/v6/test_simulation.py::TestMockVisionService::test_create_with_virtual_pages PASSED]
    
    B --> C[正则匹配解析]
    C --> D[提取: file_path, test_class, test_name, outcome]
    
    D --> E[分类统计]
    E --> F[PASSED计数++]
    E --> G[FAILED计数++]
    E --> H[SKIPPED计数++]
    
    F --> I[生成summary]
    G --> I
    H --> I
    
    I --> J[结构化test_results字典]
    
    style A fill:#e1f5ff
    style J fill:#ffd1dc
```

### 结构化数据 → Validation报告

```mermaid
graph LR
    A[test_results字典] --> B[提取summary]
    
    B --> C[total: 271, passed: 241, failed: 15, skipped: 15]
    
    C --> D[计算pass_rate: 89%]
    
    D --> E[生成markdown模板]
    
    E --> F[填充unit_test_status.md]
    E --> G[填充integration_test_status.md]
    
    F --> H[添加模块详情<br/>simulation: 33/33 ✅]
    F --> I[添加模块详情<br/>state_machine: 20/35 ⏭️]
    
    H --> J[保存到docs/validation/]
    I --> J
    
    J --> K[Git追踪文档变化]
    
    style A fill:#fff4e6
    style E fill:#d4edda
    style K fill:#90ee90
```

---

## 🎯 决策点流程图

### 何时运行测试？

```mermaid
flowchart TD
    A[开始] --> B{代码变更类型}
    
    B -->|小改动| C[运行ci范围<br/>快速验证]
    B -->|功能开发| D[运行v6范围<br/>V6功能测试]
    B -->|重大变更| E[运行all范围<br/>完整验证]
    
    C --> F[查看validation报告]
    D --> F
    E --> F
    
    F --> G{测试通过?}
    G -->|是| H[提交代码]
    G -->|否| I[修复问题]
    I --> C
    
    H --> J[✅ 完成]
    
    style A fill:#e1f5ff
    style J fill:#90ee90
```

### 测试失败处理流程

```mermaid
flowchart TD
    A[测试失败] --> B[查看validation报告]
    
    B --> C{失败类型}
    
    C -->|环境问题| D[Level 0: 解决环境<br/>安装依赖]
    C -->|实现问题| E[Level 1: 分析代码]
    C -->|设计问题| F[Level 2: 查阅文档]
    C -->|不确定| G[Level 3: 咨询用户]
    
    D --> H[重新测试]
    E --> I[修复代码]
    F --> J[理解设计意图]
    G --> K[用户决策]
    
    H --> L{测试通过?}
    I --> L
    J --> L
    K --> L
    
    L -->|是| M[✅ 解决]
    L -->|否| N[继续处理]
    
    style A fill:#fff4e6
    style M fill:#90ee90
    style N fill:#ffd1dc
```

---

## 📊 Dashboard数据流程

### 真实数据流向

```mermaid
graph LR
    A[pytest执行] --> B[解析输出]
    B --> C[生成test_results]
    C --> D[调用update_dashboard_data]
    D --> E[生成dashboard_data.json]
    
    E --> F[Dashboard HTML读取]
    F --> G[展示真实测试结果]
    
    H[用户操作] --> I[Dashboard更新]
    I --> J[fetch dashboard_data.json]
    J --> K[刷新显示]
    
    style A fill:#e1f5ff
    style E fill:#ffd1dc
    style K fill:#90ee90
```

---

## 🔄 完整端到端流程

### 从代码变更到Validation报告

```mermaid
sequenceDiagram
    participant Dev as 开发者
    participant UTC as UnifiedTestCoordinator
    participant Pytest as pytest测试框架
    participant FS as 文件系统
    participant Git as Git版本控制
    
    Dev->>UTC: python unified_test_coordinator.py v6
    UTC->>UTC: 解析参数，准备测试环境
    UTC->>Pytest: pytest tests/v6/ -v
    Pytest-->>UTC: 原始测试输出
    
    UTC->>UTC: parse_pytest_output()
    UTC->>UTC: 生成结构化test_results
    
    UTC->>FS: _generate_unit_test_status()
    UTC->>FS: 写入 docs/validation/unit_test_status.md
    
    UTC->>Dev: 终端显示测试摘要
    UTC->>Git: git add docs/validation/
    UTC->>Git: git commit "自动生成validation报告"
    
    Dev->>Dev: 查看validation报告确认结果
    Dev->>Git: git push
    
    Note over $Dev:
        统一入口 → 自动报告 → 确认质量 → 提交代码
```

---

## 🎯 关键路径

### 核心数据路径

```
代码变更 → unified_test_coordinator.py → pytest
     ↓
     pytest输出 → 结构化数据 → validation报告
     ↓
     终端显示 → 开发者确认 → Git提交
     ↓
     GitHub Actions → CI验证 → PR合并
```

### 成功路径

```
python scripts/unified_test_coordinator.py v6
    ↓
pytest 271 tests: 241 passed, 15 failed, 15 skipped in 5.23s
    ↓
📊 测试执行汇总
📋 总测试数: 271
✅ 通过: 241 (89%)
❌ 失败: 15
⏭️  跳过: 15
    ↓
✅ Validation报告生成完成
  ✅ 生成: docs/validation/unit_test_status.md
  ✅ 生成: docs/validation/integration_test_status.md
    ↓
✅ 所有测试通过！
```

---

## 📋 流程检查点

### 必须通过的检查点

1. **✅ 测试执行** - pytest无错误退出
2. **✅ 结果解析** - 成功解析pytest输出
3. **✅ 报告生成** - validation文档成功创建
4. **✅ 数据完整** - summary包含所有必要字段
5. **✅ 用户确认** - 开发者确认结果满意

### 失败路径

```
pytest失败 → Level 0环境问题 → 修复依赖 → 重试
  ↓
解析失败 → 检查pytest输出格式 → 更新解析逻辑 → 重试
  ↓
报告生成失败 → 检查文件权限 → 修复权限问题 → 重试
  ↓
数据不完整 → 补充缺失字段 → 验证数据结构 → 重试
```

---

**总结**: 这个统一流程确保了从代码变更到validation报告的**端到端自动化**，解决了之前手动编写报告、数据断开、流程混乱的问题。

**核心优势**: 
- 🎯 **唯一入口** - 不再有分散的测试脚本
- 🤖 **全自动化** - 测试到报告无需手动干预  
- 📊 **真实数据** - Dashboard显示实际测试结果
- 🔄 **完整追踪** - Git追踪所有validation变化