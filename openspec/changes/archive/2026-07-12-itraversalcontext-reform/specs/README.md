# Specs — ITraversalContext Reform

## Status: No Spec-Level Changes

本次变更 (itraversalcontext-reform) 是**纯架构健康度改进**，不引入新功能或修改 spec 级别行为。

## 变更范围

- **API 形状变更**: ITraversalContext 移除 3 个 setters，添加 SetXxx() 方法
- **实现细节变更**: TraversalFSM 和 PopupHandler 改用 SetXxx() 调用
- **行为不变**: 系统功能行为完全一致，617 tests 保持全绿

## 为什么没有 spec 文件

根据 OpenSpec 流程：
- **New Capabilities** — 需要创建 `specs/<name>/spec.md`
- **Modified Capabilities** — 需要创建 delta spec

本次变更的 Capabilities 部分为空（无新增或修改的 capabilities），因此没有对应的 spec 文件。

## 参考文档

详细设计见：[design.md](../../design.md)
