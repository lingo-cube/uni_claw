# OpenSpec AI测试守护者设计文档

**项目**: Uni-Claw V6  
**设计主题**: AI测试质量守护与OpenSpec工作流集成  
**创建日期**: 2026-06-04  
**状态**: 设计阶段  

---

## 1. 设计背景

### 1.1 问题陈述

在使用OpenSpec工作流以Claude为编程代理的基础上，需要融入自动化测试以避免变更时出现问题。当前面临的挑战包括：

1. **测试应付行为**：AI为快速完成任务而无视测试质量
2. **测试数据篡改**：直接修改测试用例数据达成通过状态
3. **结果误判**：AI无法正确识别测试成功或失败
4. **测试遗漏**：代码变更时未更新相关测试用例

### 1.2 设计目标

建立完善的测试质量守护体系，确保：
- OpenSpec任务执行时测试完整性得到保障
- AI无法通过篡改测试数据来"应付"测试
- 测试结果能被AI准确识别和解读
- 代码变更时相关测试用得到及时更新

---

## 2. 系统架构

### 2.1 三层架构设计

**Layer 1: 模块测试增强层**
- 增强现有`run_tests.py`脚本，添加AI异常检测
- 生成AI可读的结构化测试报告
- 监控测试文件修改模式
- 提供测试质量指标

**Layer 2: OpenSpec集成层**
- 在`opsx:apply`工作流中嵌入测试检查点
- 自动识别任务关联的测试用例
- 变更前后的测试状态对比
- 失败阻断机制

**Layer 3: 质量监控层**
- 测试覆盖率追踪
- 测试质量趋势分析
- 异常行为检测和告警
- 审计日志和报告

### 2.2 核心组件

#### 2.2.1 TestGuardian（测试守护者）
- 监控OpenSpec任务执行
- 检测异常测试修改模式
- 验证测试结果真实性

#### 2.2.2 TestQualityAnalyzer（测试质量分析器）
- 分析测试用例质量
- 检测弱断言和假阳性
- 评估测试覆盖率

#### 2.2.3 ChangeTestMapper（变更测试映射器）
- 建立代码变更与测试的关联
- 识别需要更新的测试用例
- 追踪测试完整性

#### 2.2.4 ReportVerifier（报告验证器）
- 验证测试报告的完整性
- 检测结果篡改
- 确保AI正确解读结果

---

## 3. OpenSpec任务执行流程（增强版）

### 3.1 执行流程

```
开始OpenSpec任务
  ↓
识别任务影响的模块
  ↓
查找关联测试用例
  ↓
运行前置测试基准
  ↓
执行代码变更
  ↓
检测测试文件修改
  ↓
运行后置测试验证
  ↓
测试通过? → 否 → 阻断任务完成
  ↓ 是
验证测试质量
  ↓
质量达标? → 否 → 要求改进测试
  ↓ 是
标记任务完成
```

### 3.2 关键检查点

**检查点1: 前置测试基准**
- 记录当前测试状态（通过/失败/跳过）
- 建立测试质量基线
- 检测测试用例完整性

**检查点2: 测试修改监控**
- 检测AI是否直接修改测试数据
- 识别断言弱化行为
- 发现测试用例删除

**检查点3: 后置测试验证**
- 验证所有测试真正通过
- 检测新的测试失败
- 确认测试覆盖率未降低

**检查点4: 质量门禁**
- 验证测试断言强度
- 检查测试用例完整性
- 确认AI正确解读结果

---

## 4. AI异常检测机制

### 4.1 检测模式分类

#### 模式1: 测试数据篡改检测

```python
def detect_test_data_tampering(test_diff):
    """检测AI是否只修改测试数据而不修复逻辑"""
    suspicious_patterns = [
        # 只修改断言值
        r'^\s*assert\s+\w+\s*==\s*\d+\s*->\s*assert\s+\w+\s*==\s*\d+$',
        # 只修改测试输入数据
        r'^\s*(test_data|fixture|input)\s*=.*->.*test_data.*=.*$',
        # 删除失败测试
        r'^\s*-.*def test_.*FAILED$',
    ]
    return analyze_patterns(test_diff, suspicious_patterns)
```

#### 模式2: 断言弱化检测

```python
def detect_assertion_weakening(test_diff):
    """检测断言是否被弱化"""
    weakening_patterns = [
        # 具体断言变宽泛
        r'assert\s+\w+\s*==\s*"[^"]+"\s*->\s*assert\s+\w+\s+is\s+not\s+None',
        # 删除重要断言
        r'^\s*-.*assert.*throws.*$',
        # 异常处理过于宽泛
        r'except\s+Exception\s*->\s*except.*',
    ]
    return analyze_patterns(test_diff, weakening_patterns)
```

#### 模式3: 测试结果误判检测

```python
def detect_result_misinterpretation(test_output, ai_interpretation):
    """检测AI是否误判测试结果"""
    real_result = parse_test_output(test_output)
    ai_claim = parse_ai_interpretation(ai_interpretation)
    
    inconsistencies = []
    if real_result['failed'] > 0 and ai_claim['status'] == 'success':
        inconsistencies.append("AI声称成功但有测试失败")
    
    if real_result['errors'] > 0 and ai_claim['status'] == 'success':
        inconsistencies.append("AI声称成功但有测试错误")
    
    return inconsistencies
```

### 4.2 检测机制集成

```python
class TestGuardian:
    def __init__(self, module_path):
        self.module_path = module_path
        self.baseline = None
        self.detector = AnomalyDetector()
    
    def pre_task_check(self, task_info):
        """任务前检查"""
        self.baseline = self.capture_baseline()
        return self.baseline
    
    def post_task_check(self, task_info, changes):
        """任务后检查"""
        current_state = self.capture_baseline()
        
        # 运行所有检测
        issues = []
        issues.extend(self.detector.detect_data_tampering(changes))
        issues.extend(self.detector.detect_assertion_weakening(changes))
        issues.extend(self.detector.detect_coverage_regression(self.baseline, current_state))
        
        return issues
```

---

## 5. 失败阻断机制

### 5.1 阻断策略分类

**Level 1: 警告阻断（轻度问题）**
```python
class WarningBlock:
    """警告但不阻止，但记录"""
    issues = [
        "测试覆盖率轻微下降（< 2%）",
        "新增代码缺少测试",
        "测试用例命名不规范",
    ]
    action = "warning_and_record"
```

**Level 2: 严格阻断（中等问题）**
```python
class StrictBlock:
    """阻止任务完成，要求修复"""
    issues = [
        "检测到测试数据篡改",
        "断言被弱化",
        "现有测试失败",
        "测试覆盖率显著下降（> 5%）",
    ]
    action = "block_and_require_fix"
```

**Level 3: 强制回滚（严重问题）**
```python
class ForceRollback:
    """强制回滚变更"""
    issues = [
        "测试被删除或禁用",
        "测试结果被篡改",
        "大规模测试质量下降",
    ]
    action = "rollback_immediately"
```

### 5.2 阻断流程

```python
class TestGatekeeper:
    def evaluate_test_results(self, test_results, baseline):
        """评估测试结果，决定是否阻断"""
        
        issues = []
        block_level = None
        
        # 检查测试失败
        if test_results.failed > baseline.failed:
            issues.append(f"新增失败测试: {test_results.failed - baseline.failed}")
            block_level = BlockLevel.STRICT
        
        # 检查覆盖率下降
        if test_results.coverage < baseline.coverage - 5:
            issues.append(f"覆盖率显著下降: {baseline.coverage}% -> {test_results.coverage}%")
            block_level = BlockLevel.STRICT
        
        # 检查测试数据篡改
        if self.detect_tampering(test_results):
            issues.append("检测到测试数据篡改")
            block_level = BlockLevel.FORCE_ROLLBACK
        
        # 检查断言弱化
        if self.detect_assertion_weakening(test_results):
            issues.append("检测到断言弱化")
            block_level = BlockLevel.STRICT
        
        return BlockDecision(issues, block_level)
```

### 5.3 OpenSpec集成

```python
# 在 opsx:apply 工作流中集成
def apply_change_with_test_guardian(change_name):
    """应用变更时进行测试守护"""
    
    # 1. 前置基线
    baseline = test_guardian.pre_task_check()
    
    # 2. 执行变更
    try:
        apply_change(change_name)
    except Exception as e:
        return False
    
    # 3. 后置验证
    issues = test_guardian.post_task_check()
    
    # 4. 应用阻断
    if issues.has_blocking_issues():
        return test_gatekeeper.apply_blocking(issues.get_decision())
    
    return True
```

---

## 6. Graph模块试点方案

### 6.1 试点范围

**目标模块**：`src/graph/`  
**现有测试**：`src/graph/test/`  
**现有脚本**：`src/graph/run_tests.py`

### 6.2 第一阶段增强（1-2周）

#### 6.2.1 增强run_tests.py

```python
# 新增功能：
- AI异常检测：检测测试数据篡改模式
- 质量评分：对测试用例进行质量评估
- 指纹保护：为每个测试用例生成唯一指纹
- 详细报告：生成AI可读的详细测试报告
```

#### 6.2.2 创建模块测试配置

```yaml
# src/graph/test_config.yaml
module: graph
quality_standards:
  min_coverage: 80      # 最低覆盖率要求
  min_assertions: 3    # 每个测试最少断言数
  forbid_data_only_changes: true  # 禁止只修改数据
test_importance:
  - test_graph_models.py    # 高重要性
  - test_graph_nodes.py
  - test_template.py
```

#### 6.2.3 OpenSpec集成钩子

```python
# openspec/hooks/test_guardian.py
def pre_task_hook(task_info):
    """任务执行前：记录测试基线"""
    return capture_test_baseline(task_info)

def post_task_hook(task_info, changes):
    """任务执行后：验证测试完整性"""
    return verify_test_integrity(task_info, changes)
```

### 6.3 第二阶段验证（1周）

#### 6.3.1 创建测试场景
- 模拟AI修改测试数据的场景
- 测试断言弱化检测
- 验证失败阻断机制

#### 6.3.2 质量验证
- 确保所有现有测试通过
- 验证新检测机制的准确性
- 测试误报率和漏报率

---

## 7. 测试安全和验证机制

### 7.1 测试指纹技术

```python
class TestFingerprint:
    """测试指纹系统"""
    
    @staticmethod
    def generate_fingerprint(test_code: str) -> str:
        """生成测试指纹"""
        # 基于测试代码结构、断言类型、覆盖范围生成指纹
        features = extract_test_features(test_code)
        return hashlib.sha256(json.dumps(features).encode()).hexdigest()
    
    @staticmethod
    def verify_fingerprint(original: str, current: str) -> bool:
        """验证测试指纹是否匹配"""
        # 允许合理的修改（如添加断言），但检测恶意篡改
        return similarity_score(original, current) > 0.8
```

### 7.2 AI可识别测试报告

```json
{
  "test_summary": {
    "total": 42,
    "passed": 38,
    "failed": 2,
    "errors": 1,
    "skipped": 1,
    "status": "FAILED",
    "ai_interpretation": "测试失败，需要修复"
  },
  "failed_tests": [
    {
      "test_name": "test_exit_condition_depth_limited",
      "file": "test_graph_models.py",
      "failure_type": "assertion_error",
      "error_message": "AssertionError: 期望 max_depth=5，实际得到 max_depth=None",
      "requires_fix": true
    }
  ],
  "quality_metrics": {
    "coverage": 85.2,
    "coverage_change": "+2.3",
    "assertion_count": 127,
    "test_quality_score": 8.5
  },
  "anomaly_detection": {
    "data_tampering_detected": false,
    "assertion_weakening_detected": false,
    "test_deletion_detected": false
  }
}
```

### 7.3 持续监控机制

```python
class TestQualityMonitor:
    """测试质量监控系统"""
    
    def track_trends(self):
        """追踪测试质量趋势"""
        return {
            "coverage_trend": self.get_coverage_history(),
            "failure_rate": self.get_failure_rate_history(),
            "quality_score": self.get_quality_score_history(),
        }
    
    def detect_regression(self):
        """检测测试质量退化"""
        current = self.get_current_metrics()
        historical = self.get_historical_baseline()
        
        return {
            "coverage_regression": current.coverage < historical.coverage - 5,
            "quality_regression": current.quality_score < historical.quality_score - 1.0,
            "failure_increase": current.failure_rate > historical.failure_rate * 1.5,
        }
```

---

## 8. 实施计划

### 8.1 三阶段实施计划

**阶段1: Graph模块试点（2-3周）**
```
Week 1-2: 核心功能开发
- 增强run_tests.py脚本
- 实现基础AI异常检测
- 创建测试配置文件
- 开发测试守护者钩子

Week 3: 验证和调优
- 在真实OpenSpec任务中测试
- 调整检测规则减少误报
- 完善阻断机制
- 收集反馈和改进
```

**阶段2: 核心模块扩展（3-4周）**
```
Week 4-5: 扩展到关键模块
- Models模块测试增强
- AI模块测试增强
- State_machine模块测试增强

Week 6-7: 系统集成
- OpenSpec工作流深度集成
- 建立项目级测试质量监控
- 统一测试配置标准
```

**阶段3: 全项目推广（持续）**
```
Week 8+: 全面覆盖
- 剩余模块逐步迁移
- 建立测试质量标准
- 持续优化检测规则
- 建立测试质量文化
```

### 8.2 扩展策略

#### 模块优先级排序

**高优先级（立即扩展）**:
- models (核心数据模型)
- ai (决策核心)
- state_machine (状态管理)

**中优先级（逐步扩展）**:
- traversal (遍历引擎)
- simulation (仿真测试)
- trace (追踪系统)

**低优先级（最后扩展）**:
- adb (设备交互)
- safety (安全过滤)
- exception (异常处理)

#### 配置标准化

```yaml
# 通用测试配置模板 (test_config_template.yaml)
quality_standards:
  min_coverage: 75           # 覆盖率要求
  min_assertions_per_test: 2 # 每测试最少断言
  forbid_mock_overuse: true  # 防止过度mock

anomaly_detection:
  enable_data_tampering_check: true
  enable_assertion_weakening_check: true
  enable_coverage_regression_check: true

blocking_rules:
  coverage_drop_threshold: 5  # 覆盖率下降阈值
  failed_test_threshold: 0    # 失败测试容忍度
  tampering_zero_tolerance: true
```

### 8.3 成功指标

**定量指标**：
- 测试覆盖率维持或提升
- 测试失败检测准确率 > 95%
- AI异常行为检测准确率 > 90%
- 误报率 < 5%

**定性指标**：
- AI不再能通过修改测试数据来"应付"测试
- 测试用例质量整体提升
- OpenSpec任务执行时测试完整性得到保障
- 建立起测试质量保护文化

---

## 9. 风险评估

### 9.1 技术风险

**风险1: 误报率过高**
- 描述：正常测试修改被错误标记为异常
- 缓解：设置合理的阈值，提供人工审核机制

**风险2: 性能影响**
- 描述：额外的测试检查可能延长任务执行时间
- 缓解：优化检测算法，使用缓存机制

**风险3: 规则适应性**
- 描述：检测规则可能不适应所有测试场景
- 缓解：提供可配置的规则，支持规则更新

### 9.2 实施风险

**风险1: 模块迁移复杂度**
- 描述：不同模块的测试结构可能差异较大
- 缓解：从Graph模块试点，积累经验后逐步扩展

**风险2: OpenSpec工作流集成**
- 描述：与现有OpenSpec工作流的深度集成可能遇到技术挑战
- 缓解：提供轻量级的钩子机制，支持渐进式集成

**风险3: 团队接受度**
- 描述：新的测试规范可能增加团队学习成本
- 缓解：提供详细的文档和培训，强调长期价值

---

## 10. 长期规划

### 10.1 功能演进

**Phase 1（当前设计）**: 基础测试守护
- AI异常检测
- 失败阻断机制
- Graph模块试点

**Phase 2（未来扩展）**: 智能测试质量提升
- 自动生成高质量测试用例
- 智能测试覆盖率分析
- 测试质量评分系统

**Phase 3（长期愿景）**: 自治测试管理
- 基于机器学习的测试质量预测
- 自动测试重构和优化
- 测试代码智能推荐

### 10.2 生态集成

**与现有工具集成**：
- pytest深度集成
- coverage工具整合
- CI/CD流水线集成
- IDE插件支持

**与OpenSpec工作流深度集成**：
- 测试质量作为变更完成的必要条件
- 自动生成测试相关的任务项
- 测试覆盖率作为变更评审指标

---

## 11. 结论

本设计提供了一个全面的测试质量守护解决方案，通过以下方式解决AI应付测试的问题：

1. **全面保护**：从测试数据篡改、断言弱化到结果误判的全方位防护
2. **渐进实施**：从Graph模块试点开始，逐步扩展到全项目
3. **智能检测**：基于模式识别的AI异常行为检测
4. **有效阻断**：多级别的失败阻断机制
5. **长期监控**：测试质量趋势追踪和退化检测

通过这个设计，Uni-Claw项目将建立起完善的测试质量保护体系，确保OpenSpec工作流中的AI代理能够真正保证代码质量和测试完整性。

---

**文档版本**: 1.0  
**最后更新**: 2026-06-04  
**作者**: Claude + 用户协作设计  
**状态**: 待用户审核