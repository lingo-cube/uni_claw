# Per-step visual evidence persistence — AGT-008 验证运行（2026-10-02）

## 这次做了什么

修复 AGT-007 复盘暴露的追溯缺口：真实运行的截图与 hierarchy XML 此前
仅内存消费、不留实体文件，事后无法回答"点击发生时屏幕上是什么"。
现在每个观察周期把两者落盘为 `run-*/evidence/{captureId}.png` 与
`{captureId}.xml`，与步骤记录的 `postCaptureId` 一一对应。

## 验证结果（真机完整任务，专用 3081 + emulator-5556）

- 任务行为零变化：19/19 步、覆盖率 100%、digest 与既往成功运行一致
  （9A8BC…）——证据持久化不改变遍历语义。
- **证据链闭合**：40 个证据文件（20 周期 × PNG+XML）、零空文件、
  **全部 19 步的 postCaptureId 都能命中同名 .png 与 .xml**。
- facts.json 新增 `evidenceDir` 与 `evidenceFiles` 计数（40）。
- PNG 74–131KB/张；XML 为完整 uiautomator 层级（抽查含 Settings
  FrameLayout 树）。

## 配置

`.dsh/profiles/settings-coverage.yaml` 新增可选段（缺省全开）：

```yaml
evidence:
  persistScreenshots: true
  persistHierarchies: true
```

显式 `false` 关闭对应工件；非法值 fail closed（`config-invalid:...`）。

## 测试

- 新增 4 个：缺省段默认值、显式 false、非法值 fail closed、写盘器单测。
- 全量测试通过（Host 覆盖测试 48 项）；场景认证 PASS（Kernel/Agent 源
  未变）。

## 证据文件

- `run-final/evidence/{captureId}.png|.xml`：20 周期的视觉证据（本次核心）。
- `run-final/coverage-steps.json`：19 步 trace（postCaptureId 关联键）。
- `run-final/{coverage-report,facts,settings-trace,exec.journal}.json`：
  报告、终态（含 evidenceFiles=40）、观察周期、回执。
- `run-final/console.log`：进程输出。
- 注：trace.json（16MB，与 AGT-005 归档的运行同构）留在
  /tmp/real-settings-coverage-evidence/ 未入库，digest 可证同任务。

## 现在能回答此前无法回答的问题

以 AGT-007 的两个悬案为例，同款情形再发生时：
- "点击 Security & privacy 时屏幕到底是什么" → 打开该步骤的
  `{postCaptureId}.png` 与 `.xml` 即可逐像素/逐节点复盘；
- "Dismiss 按钮属于哪张卡片" → 该周期的 XML 里直接可见卡片全文与归属。
