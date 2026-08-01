# Screenshots fixture — 视觉 golden 测试资产

供 `VisionGoldenIntegrationTests` / `RealVisionIntegrationTests` 使用（均为显式集成测试，默认跳过）。

## 资产约定

- 截图文件：`*.png` / `*.jpg` / `*.jpeg`，放在本目录。
- 预期 golden：与截图同目录、同名、后缀 `.expected.json`（如 `screen.png` → `screen.expected.json`）。
- 实际结果：每次运行自动生成同名 `.actual.json`，用于人工 diff。

## 校准流程（首次 / 换设备 / 换界面）

1. 放入截图（如从真机 ADB 截取）。
2. 校准生成 golden：

   ```bash
   UNICLAW_INTEGRATION_SCOPES=vision-golden \
   UNICLAW_VISION_UPDATE_EXPECTED=1 \
   dotnet test tests/UniClaw.Core.Tests --filter "IntegrationScope=vision-golden"
   ```

3. **人工审阅** `screen.expected.json`：
   - 确认 `items` 名称/坐标与截图一致；
   - 对模型方差大的字段可删除 `type`/`action`（null = 不校验）；
   - 坐标 `tolerance` 默认 0.08（归一化），可逐项调整。
4. 审阅通过后提交 golden 文件，日常回归：

   ```bash
   UNICLAW_INTEGRATION_SCOPES=vision-golden \
   dotnet test tests/UniClaw.Core.Tests --filter "IntegrationScope=vision-golden"
   ```

## 说明

- 真实模型存在合理方差，golden 采用容差匹配（名称或坐标命中其一即可，额外项允许）。
- 本目录现有截图来自真实 Android 设备（PKJ110，1440x3168），
  其 `.analysis.json` 为历史空结果，不代表当前模型能力。
