# Specs — Scroll Simulation Enhancement

## Status: New Capability

本次变更引入 `scroll-simulation` 新能力，为 C# 仿真测试基础设施添加滚动列表模拟支持。

## 变更范围

- **新增能力**: scroll-simulation — 滚动列表仿真测试
- **数据模型**: ScrollSegment + ScrollState
- **服务组件**: ScrollableMockVisionService
- **Builder 扩展**: StateFixtureBuilder 滚动段定义

## 规格文件

- [scroll-simulation/spec.md](scroll-simulation/spec.md) — 滚动模拟完整规格

## Python 对齐

本规格对齐 Python V7.0 `src/simulation/scroll/` 模块：
- ScrollSegment 累积模式逻辑一致
- ScrollState 追踪字段一致
- ScrollableMockVisionService 行为一致
