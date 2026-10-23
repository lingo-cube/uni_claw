# World Visualizer — Standards Review

**范围：** `tools/world-visualizer/` 的模型、浏览器呈现、静态服务与示例打包；只审 Standards，不代替 Spec 验收。基于当前工作树读取，未运行产品构建。

## 结论

没有发现违反产品 Owner / Authority 边界的实现：仓库职责基线明确 `tools/` 不拥有 Product semantic authority（`docs/architecture/repository-role-boundaries-v0.1.md:22`），模型文件也声明它只做呈现适配、不判断身份、配准、权限或效果（`tools/world-visualizer/model.mjs:1`）。静态服务仅绑定 `127.0.0.1` 并拒绝目录逃逸（`tools/world-visualizer/serve.py:14-20,26-28`）。以下是低优先级可维护性与可访问性问题，不阻塞当前只读交付。

## 发现

1. **P3，Fowler 判断：未使用的选中来源状态。** `app.mjs:17` 声明 `selectedSource`，`select()` 在 `app.mjs:73-79` 写入，但没有任何消费者；目前它既不影响展示也不保护来源归属。建议移除字段和参数，或在确有需求时通过可见来源视图消费，避免读者误以为选中对象已按来源版本绑定。
2. **P3，可访问性一致性：关系图按钮缺少 Space 激活。** 节点以 `role="button"` 暴露并同时处理 Enter/Space（`app.mjs:113-116`），关系边同样有 `role="button"` 与 `tabindex="0"`，却只处理 Enter（`app.mjs:117`）。键盘使用者无法用 Space 激活关系；建议与节点处理保持一致，或改用原生按钮语义。
3. **P3，Fowler 判断：呈现函数职责偏密。** `renderGraph()` 同时过滤数据、计算布局、组装 SVG、绑定键盘/鼠标行为（`app.mjs:82-119`）；`renderPage()` 也混合时点选择、Slice/历史模式解释和布局绘制（`app.mjs:121-172`）。当前仍位于单一只读页面适配器内，没有跨 Owner 伸手；若后续增加视图，可拆成纯投影选择与 DOM 绘制两段，现阶段不建议为此扩大重构。

## 验证

`python3 -m py_compile tools/world-visualizer/package_examples.py` 与 `node --test tools/world-visualizer/model.test.mjs` 均通过；模型测试 3 项。七个 family JSON 的 schema/provenance 复核：索引 `examples` 有 7 项；correction 保留 8 个实际 W，只有 rev-8 带对应 PageView、知识、影响和咨询；其它帧空页面并显式保持未知。scroll 使用新的精确 W 导出，5 帧均有同 W PageView 与 3 个 Slice。三个新包装使用 `artifacts` 顶层，无 `datasets.datasets`。录制帧 current、graph 与实际 HTTP consultation 中的 WorldKnowledge 均为 rev-2。此验证不证明 live device、最终 provider envelope 或 model 质量。
