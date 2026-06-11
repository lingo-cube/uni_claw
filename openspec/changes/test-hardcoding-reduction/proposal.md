## Why

测试代码中广泛存在硬编码值（魔法数字、硬编码字符串、配置常量分散在多处），导致测试可维护性差、难以统一修改、容易遗漏引用点。当前需要建立测试常量集中管理机制，消除不必要的硬编码，同时保留测试数据和业务生成值的灵活性。

## What Changes

- 新增测试配置目录 `tests/config/`，集中管理框架配置常量（timeout、retry、concurrent）
- 新增测试ID生成器 `tests/config/test_ids.py`，统一测试ID创建
- 新增设备/坐标工厂 `tests/factories/device_factory.py`，管理测试数据而非常量化
- 建立枚举值导入规范，从源码导入枚举而非硬编码字符串
- 迁移现有测试中的硬编码配置常量为语义化常量
- 保留业务生成值（坐标、尺寸）为测试数据/魔法数字，不过度抽象

## Capabilities

### New Capabilities

- `test-constants`: 测试框架配置常量管理（Timeout、Retry、Concurrency、ScrollThreshold）
- `test-id-generator`: 统一测试ID生成（node_id、span_id、trace_id、element_id）
- `test-data-factories`: 设备/坐标测试数据工厂（DeviceFactory、CoordinateFactory）
- `enum-import-standards`: 枚举值导入规范，从源码导入状态/决策枚举

### Modified Capabilities

无（本变更专注于测试代码实现，不修改产品级功能规范）

## Impact

**影响范围**:
- 新增文件：4个（tests/config/constants.py、tests/config/test_ids.py、tests/factories/device_factory.py、tests/config/__init__.py）
- 修改测试文件：29个
- 影响模块：测试套件整体

**不影响**:
- 产品代码（src/ 目录）
- API 接口
- 生产系统行为

**风险等级**: 低（仅测试代码，易于回滚）

**工时估算**: 30小时（37个任务，平均0.8h/任务）

**实施顺序**: 按影响范围从小到大
1. 基础设施创建（6h）
2. 枚举导入（4.5h）
3. 简单常量替换（2.5h）
4. 批量常量替换（2.5h）
5. 复杂常量替换（1.5h）
6. ID迁移（9h）
7. 工厂方法引入（4h）
8. 可选优化（1h+）

**关键原则**:
1. 有意义的类型才需要归类
2. 业务生成值使用魔法数字
3. 枚举应来自源代码
4. 常量形成设计规范
5. 测试数据保留灵活性
6. 源码修改需同步检查测试
7. 按影响范围从小到大排序
8. 任务细化（1-2h/任务，可独立验证）
