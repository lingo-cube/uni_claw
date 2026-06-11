## Context

### Current State

测试代码中存在大量硬编码值，分为以下几类：
- **配置常量**：timeout、retry、concurrent 等分散在多个测试文件中
- **测试ID**：node_id、span_id 等无语义字符串散布在各处
- **坐标/尺寸**：设备规格和屏幕坐标直接硬编码
- **枚举字符串**：状态、决策等枚举值以字符串形式硬编码

### Constraints

1. **仅修改测试代码**：本变更不影响产品代码（src/）
2. **保持测试通过**：所有修改必须保证测试逻辑不变
3. **回滚能力**：每个任务可独立回滚
4. **向后兼容**：工厂方法需支持现有代码格式

### Stakeholders

- 测试维护者：需要更易维护的测试代码
- CI/CD 系统：需要所有测试通过
- 开发者：需要清晰的常量定义和导入规范

## Goals / Non-Goals

**Goals:**

1. 建立测试配置常量集中管理机制（仅限真正的框架配置常量）
2. 提供统一测试ID生成器，消除无语义字符串
3. 建立测试数据工厂（设备/坐标），保留灵活性
4. 规范枚举导入，从源码导入而非硬编码
5. 按37个细化任务实施，每个1-2小时可完成
6. 按影响范围从小到大排序实施

**Non-Goals:**

1. 不替换坐标为常量（使用工厂方法或魔法数字）
2. 不替换屏幕尺寸为常量（在工厂中管理）
3. 不替换测试数据（load_factor、user_count 等）
4. 不替换业务阈值（性能要求等）
5. 不修改产品代码（src/ 目录）

## Decisions

### Decision 1: 坐标使用工厂而非常量

**选择**：使用 `CoordinateFactory` 创建坐标对象，而非 `Coordinate.CENTER` 常量类。

**理由**：
- 坐标是业务生成值，由被测应用的UI布局决定
- 需要支持任意坐标值（如 0.3, 0.7），不仅限于预定义位置
- 工厂方法可扩展，支持多种坐标格式（dict、对象）

**替代方案**：
- ~~常量类~~（Coordinate.CENTER）：限制灵活性，不符合"业务生成值用魔法数字"原则

### Decision 2: ScrollThreshold 为可选使用

**选择**：提供 `ScrollThreshold.START/HALF/END` 常量，但保留直接使用数值的能力。

**理由**：
- 0.0/0.5/1.0 是常见的语义位置，可提高可读性
- 但测试可能需要任意位置（0.33），强制使用常量会限制灵活性
- 作为可选辅助，不强制迁移

**替代方案**：
- ~~强制替换~~：会限制测试场景，违反"保留测试数据灵活性"原则

### Decision 3: 任务按影响范围排序

**选择**：A（新增无影响）→ B（枚举导入）→ C（简单替换）→ D-F（复杂迁移）

**理由**：
- 新增文件零风险，可优先完成
- 枚举导入边界清晰，单文件改动
- ID迁移需要扫描引用点，风险较高，后置
- 工厂引入改变代码结构，影响最大，最后处理

### Decision 4: 任务细化到1-2小时

**选择**：37个任务，平均0.8h/任务，最大1.5h/任务。

**理由**：
- 小任务易于Code Review
- 每个任务可独立验证
- 失败后易于定位和回滚
- 可并行开发（A、B、C阶段）

### Decision 5: 枚举从源码导入

**选择**：测试从 `src.state_machine.traversal_fsm` 等模块导入枚举，而非重复定义。

**理由**：
- 单一真实来源（Single Source of Truth）
- 重构时自动同步（修改源码，测试导入自动更新）
- IDE支持自动补全和类型检查

**替代方案**：
- ~~在 tests 中重复定义枚举~~：维护成本高，容易遗漏

## Architecture

### 新增模块结构

```
tests/
├── config/                    # 新增：测试配置目录
│   ├── __init__.py
│   ├── constants.py          # Timeout, Retry, Concurrency, ScrollThreshold
│   └── test_ids.py           # TestIdGenerator
├── factories/                 # 新增/扩展现有
│   └── device_factory.py     # DeviceFactory, CoordinateFactory
└── ...
```

### 模块职责

| 模块 | 职责 | 不包含 |
|------|------|--------|
| `constants.py` | 框架配置常量（timeout、retry、concurrent） | 坐标、尺寸、业务值 |
| `test_ids.py` | 测试ID生成 | 测试数据生成 |
| `device_factory.py` | 设备/坐标数据工厂 | 配置常量 |

### 依赖关系

```
tests/config/constants.py (无依赖)
    ↓
tests/config/test_ids.py (无依赖)
    ↓
tests/factories/device_factory.py (无依赖，可选导入 constants)
    ↓
各个测试文件（逐步迁移）
```

## Migration Plan

### 实施阶段

| 阶段 | 任务 | 工时 | 验证 |
|------|------|------|------|
| A | 基础设施创建（8任务） | 6h | 文件可导入 |
| B | 枚举导入（7任务） | 4.5h | 测试通过 |
| C | 简单替换（5任务） | 2.5h | 测试通过 |
| D | 批量替换（3任务） | 2.5h | 测试通过 |
| E | 复杂替换（1任务） | 1.5h | 测试通过 |
| F | ID迁移（8任务） | 9h | grep验证无残留 |
| G | 工厂引入（5任务） | 4h | 测试通过 |
| H | 可选优化（评估） | 1h+ | 评估报告 |

### 回滚策略

- **单个任务回滚**：`git checkout <file>`
- **批量回滚**：`git reset --soft HEAD~N`
- **完全回滚**：删除新增目录 `tests/config/` 和 `tests/factories/device_factory.py`

### 验证命令

```bash
# 验证常量已替换
grep -r "timeout.*=.*[0-9]" tests/ | grep -v "Timeout\."

# 验证ID无残留
grep -r "node123" tests/

# 验证枚举已导入
grep -r "from src.state_machine.*import" tests/ | wc -l

# 运行测试
pytest tests/ -v
```

## Risks / Trade-offs

### Risk 1: ID迁移遗漏引用点

**风险**：替换ID时遗漏断言或日志中的引用，导致测试失败。

**缓解**：
- 每个ID替换前运行 `grep -r "<ID>" tests/` 扫描
- 使用同一变量，所有地方引用该变量
- 字符串拼接改为f-string
- 替换后再次grep确认无残留

### Risk 2: 枚举导入路径变化

**风险**：源码重构导致枚举位置变化，测试导入失败。

**缓解**：
- 枚举路径作为稳定接口承诺
- 如需移动，同步更新测试导入
- IDE自动重构支持

### Risk 3: 过度抽象

**风险**：强制使用常量/工厂降低测试代码灵活性。

**缓解**：
- ScrollThreshold 为可选
- 坐标工厂支持任意值
- 保留直接使用魔法数字的能力

### Risk 4: 工时估算不准

**风险**：实际工时超过估算（30h）。

**缓解**：
- 任务细化（37个），可并行开发
- 每任务独立验证，易于调整
- H阶段为可选，可延后

### Trade-off: 可读性 vs 灵活性

**权衡**：使用常量提高可读性，但降低灵活性。

**选择**：平衡策略
- 框架配置常量化（Timeout、Retry）
- 测试数据工厂化（CoordinateFactory）
- 保留魔法数字能力（ScrollThreshold 可选）

## Open Questions

1. **Q**: ScrollThreshold 是否值得实施？
   - **A**: H-01 评估后决定，优先级较低

2. **Q**: 是否需要 CI 检查硬编码？
   - **A**: 可在后版本添加 pre-commit hook 检测新的硬编码

3. **Q**: CoordinateFactory 和 DeviceFactory 是否合并？
   - **A**: 保持分离，职责清晰（坐标 vs 设备规格）

## Success Criteria

- [ ] 37个任务全部完成
- [ ] 所有测试通过（无回归）
- [ ] 代码覆盖率不降低
- [ ] grep 验证：无残留旧ID、无硬编码枚举字符串
- [ ] 新增模块可正常导入
- [ ] 工时偏差 < 20%
