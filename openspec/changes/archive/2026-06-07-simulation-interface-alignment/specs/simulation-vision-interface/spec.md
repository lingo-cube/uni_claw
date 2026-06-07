## ADDED Requirements

### Requirement: MockVisionService 实现 VisionService 接口
系统 SHALL 提供继承 `VisionService` ABC 的 `MockVisionService`，与真实引擎 `GraphTraversalEngine` 完全兼容。

#### Scenario: 类型检查通过
- **WHEN** 创建 `MockVisionService` 实例
- **THEN** `isinstance(service, VisionService)` 返回 `True`

#### Scenario: analyze_screenshot 签名匹配
- **WHEN** 调用 `mock.analyze_screenshot(image_data: bytes)`
- **THEN** 返回 `PageAnalysis` 类型实例
- **AND** 方法接受 `bytes` 参数（忽略其内容）

#### Scenario: find_app_entry 签名匹配
- **WHEN** 调用 `mock.find_app_entry(image_data, target)`
- **THEN** 返回 `Optional[dict]` 包含 `x` 和 `y` 坐标

### Requirement: 虚拟页面查表机制
MockVisionService SHALL 通过 `virtual_pages` 字典和当前路径上下文返回对应页面分析。

#### Scenario: 按路径查表
- **WHEN** `set_path_context(["home", "settings"])` 已调用
- **AND** `virtual_pages` 包含键 `"home/settings"`
- **THEN** `analyze_screenshot(b"")` 返回该键对应的 `PageAnalysis`

#### Scenario: 路径不存在时回退
- **WHEN** `set_path_context(["unknown"])` 已调用
- **AND** `virtual_pages` 不包含键 `"unknown"`
- **THEN** 返回 `virtual_pages["home"]` 对应的 `PageAnalysis`

### Requirement: 路径上下文同步
MockVisionService SHALL 提供 `set_path_context` 方法供引擎更新当前路径。

#### Scenario: 更新当前路径
- **WHEN** 调用 `set_path_context(["home", "settings", "wifi"])`
- **THEN** 后续 `analyze_screenshot` 调用使用该路径查表
- **AND** 不影响之前的调用计数

### Requirement: MockVisionService 替换旧实现
系统 SHALL 删除 `src/simulation/mock_vision.py` 中旧的独立类实现，替换为继承 ABC 的新实现。

#### Scenario: 旧方法不存在
- **WHEN** 查看 `MockVisionService` 类
- **THEN** 存在 `analyze_screenshot(bytes) -> PageAnalysis` 方法
- **AND** 存在 `find_app_entry(bytes, str) -> Optional[dict]` 方法
- **AND** 不再有 `analyze_screenshot(screenshot_path: str) -> Dict` 旧签名
