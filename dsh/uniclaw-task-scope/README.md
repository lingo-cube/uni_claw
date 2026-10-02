# @uniclaw/dsh-task-scope

`uniagent-task` preset 的工具面收窄插件（PNL-001 决策 6，切片 S2）。与
`@uniclaw/dsh-uniagent-restrict`（AGT-002，冻结 `allow: []`）同构，但
allow-list 来自 preset 声明行的 `config.allow`，由每个部署实例自行点名任务
session 需要的工具（读 + bash + skill 加载；name 级白名单，晚注册的 MCP
工具默认不进——allow 语义由 DSH 的 live 过滤保证，理由见
`dsh/uniclaw-restrict/src/index.js` 头注释）。

## 安装

1. 把本包复制进部署实例的插件目录（与 `@uniclaw/dsh-uniagent-restrict`
   同一机制）。
2. 在部署实例的 `cordis.patch.yml` 中加入 preset 行，参考
   `dsh/agent-presets/uniagent-task/cordis.patch.yml`：

   ```yaml
   - insert:
       - id: preset-uniclaw-task
         name: '@deepseek-ai/dsh-agent-preset'
         config:
           id: uniclaw-task
           order: 4
           plugins:
             - id: uniclaw-task-scope
               name: '@uniclaw/dsh-task-scope'
               config:
                 allow: ['read', 'glob', 'grep', 'bash']
   ```

3. **`config.allow` 必须与该部署实例的 live catalog 核对**：`restrict()`
   会对名单逐项校验，任何一个未注册的名字都会在 mount 时抛错（fail-fast，
   避免名单与实例漂移后静默失效）。名单是 name 级的；命令级 bash 白名单
   out of scope（PNL-001 决策 6）。

## 回滚

- 从部署实例的 `cordis.patch.yml` 删除 `uniclaw-task-scope` 插件行（或整段
  `preset-uniclaw-task` insert），重启实例。任务 session 即恢复未收窄的
  默认工具面。
- 删除插件目录中的包文件即可彻底卸载；无持久状态、无 schema 迁移。

## 验证

```bash
cd dsh/uniclaw-task-scope && npm test   # mock ctx：合法 allow 恰好 restrict 一次；缺失/空/类型不符 mount 抛错
node --check src/index.js               # 语法
```

部署后冒烟：启动一个 uniagent-task session，确认目录内工具只剩 `allow`
点名的那几个，且 MCP 工具不可见。任何 `config.allow` 缺失、为空数组或含
非字符串元素都会让 mount 直接失败——这是刻意的 fail-closed，不要回退。
