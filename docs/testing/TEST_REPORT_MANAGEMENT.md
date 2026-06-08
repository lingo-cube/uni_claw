# 测试报告存放和整理规范

> **版本**: V1.0 | **日期**: 2026-06-08
> **目的**: 规范测试报告的存放、命名和整理

---

## 报告目录结构

```
tests/
├── reports/
│   ├── by-module/          # 按模块分类
│   │   ├── state_machine/
│   │   │   ├── 2026-06-08_test-generation-report.md
│   │   │   ├── coverage.json
│   │   │   └── quality-metrics.json
│   │   ├── graph/
│   │   └── traversal/
│   │
│   ├── by-date/            # 按日期分类
│   │   ├── 2026-06-08/
│   │   │   ├── state_machine_report.md
│   │   │   ├── graph_report.md
│   │   │   └── daily-summary.md
│   │   └── 2026-06-09/
│   │
│   ├── summaries/          # 汇总报告
│   │   ├── weekly-summary.md
│   │   ├── monthly-summary.md
│   │   └── trend-analysis.md
│   │
│   └── archive/            # 历史归档
│       ├── 2026-05/
│       └── 2026-04/
│
└── temp/                   # 临时报告
    └── (定期清理)
```

---

## 报告类型和命名

### 1. 测试生成报告

**命名格式**: `{module}_test-generation-report_{date}.md`

**示例**:
```
state_machine_test-generation-report_2026-06-08.md
graph_test-generation-report_2026-06-08.md
```

**内容结构**:
```markdown
# {Module} 测试生成报告

## 执行信息
- 日期: 2026-06-08
- Workflow: integrated-test-gen
- 执行者: Claude

## 检查结果
- 设计文档: ✅
- 测试场景: ✅
- 源代码: ✅

## 生成统计
- 场景数: 112
- 测试数: 85
- 覆盖率目标: 85%

## 质量评分
- Mock验证: 100%
- 断言验证: 95%
- 覆盖度验证: 90%
- 总体评分: 85/100

## 关键发现
- ...
```

---

### 2. 测试执行报告

**命名格式**: `{module}_test-execution-report_{date}_{timestamp}.md`

**示例**:
```
state_machine_test-execution-report_2026-06-08_143025.md
```

**内容结构**:
```markdown
# {Module} 测试执行报告

## 执行信息
- 日期: 2026-06-08 14:30:25
- 命令: pytest tests/state_machine/ -v
- 执行者: CI/CD

## 执行结果
- 总测试数: 85
- 通过: 82
- 失败: 2
- 跳过: 1

## 覆盖率
- 行覆盖率: 87%
- 分支覆盖率: 76%
- 函数覆盖率: 92%

## 失败详情
- test_state_transition_fails: ...
```

---

### 3. 覆盖率报告

**命名格式**: `{module}_coverage-report_{date}.json`

**示例**:
```
state_machine_coverage-report_2026-06-08.json
```

**JSON结构**:
```json
{
  "module": "state_machine",
  "date": "2026-06-08",
  "summary": {
    "percent_covered": 87.5,
    "percent_branch_covered": 76.3,
    "num_statements": 1234,
    "num_branches": 234,
    "num_missing_statements": 154
  },
  "files": {
    "state.py": {"percent": 92, "missing": []},
    "transition.py": {"percent": 85, "missing": ["line_45"]}
  }
}
```

---

### 4. 质量指标报告

**命名格式**: `{module}_quality-metrics_{date}.json`

**JSON结构**:
```json
{
  "module": "state_machine",
  "date": "2026-06-08",
  "metrics": {
    "complexity": 8.5,
    "maintainability": 72,
    "test_duplication": 15,
    "rule_compliance": 95
  }
}
```

---

## 报告生成时机

### 自动生成

| 触发事件 | 报告类型 | 存放位置 |
|----------|----------|----------|
| `/Workflow integrated-test-gen` | 测试生成报告 | `reports/by-module/{module}/` |
| `pytest` 执行完成 | 测试执行报告 | `reports/by-date/{date}/` |
| `pytest --cov` 执行 | 覆盖率报告 | `reports/by-module/{module}/` |

### 手动生成

```bash
# 生成模块测试汇总
python scripts/generate_test_summary.py state_machine

# 生成每日汇总
python scripts/generate_daily_summary.py 2026-06-08

# 生成趋势分析
python scripts/generate_trend_analysis.py
```

---

## 报告整理逻辑

### 每日整理

1. **午夜任务**: 将当日报告移动到 `by-date/{date}/`
2. **清理临时**: 删除 `temp/` 中超过24小时的报告
3. **生成汇总**: 创建 `daily-summary.md`

### 每周整理

1. **周汇总**: 生成 `summaries/weekly-summary.md`
2. **趋势分析**: 对比上周数据
3. **归档**: 将4周前的报告移到 `archive/`

### 每月整理

1. **月汇总**: 生成 `summaries/monthly-summary.md`
2. **长期归档**: 将6个月前的报告压缩归档

---

## 报告清理策略

### 保留策略

| 报告类型 | 保留期限 | 归档方式 |
|----------|----------|----------|
| 测试生成报告 | 4周 | 压缩归档 |
| 测试执行报告 | 1周 | 删除 |
| 覆盖率报告 | 4周 | 压缩归档 |
| 汇总报告 | 永久 | 保留 |

### 自动清理

```bash
# 每周执行
python scripts/cleanup_test_reports.py --older-than 7days --type execution

# 每月执行
python scripts/cleanup_test_reports.py --older-than 28days --archive
```

---

## 报告查询

### 查看最新报告

```bash
# 查看某模块最新生成报告
cat tests/reports/by-module/{module}/$(ls -t | head -1)

# 查看今日执行报告
cat tests/reports/by-date/$(date +%Y-%m-%d)/*_execution_*
```

### 查看历史趋势

```bash
# 查看覆盖率趋势
python scripts/show_coverage_trend.py {module}

# 查看质量评分趋势
python scripts/show_quality_trend.py {module}
```

---

## 集成到Workflow

`integrated-test-gen` workflow自动生成并存放报告：

```javascript
// Phase 5: Report
const report = await generateReport(moduleName, results);

// 存放报告
const reportPath = `tests/reports/by-module/${moduleName}/${moduleName}_test-generation-report_${date}.md`;
await writeFile(reportPath, report);
```

---

## 报告模板

### 测试生成报告模板

位置: `tests/reports/templates/test-generation-report.md`

### 测试执行报告模板

位置: `tests/reports/templates/test-execution-report.md`

---

**维护者**: Uni-Claw Development Team
**最后更新**: 2026-06-08
