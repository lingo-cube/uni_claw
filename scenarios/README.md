# scenarios/ — 仿真场景库

> 独立的仿真基础设施。按职能定位（模拟/回放/验证），不依附于任何文档。
> 150 场景推理文档（ARCH-DOC-017）是来源之一——派生场景可选标注 srRef。

## 场景来源类型

| source           | 含义                     |
|------------------|--------------------------|
| recorded         | 真机/模拟器录制回放       |
| generated        | 参数化动态生成            |
| derived-from-doc | 从推理文档派生（带 srRef）|
| bug-repro        | 缺陷复现                 |
| component-test   | 组件级隔离测试            |

## 元数据字段（8 + 期望值）

见 `schema.json`。status 由覆盖率工具自动从测试结果更新。

## 使用

```bash
# 能力覆盖率报告
python3 tools/scenario-coverage.py
```
