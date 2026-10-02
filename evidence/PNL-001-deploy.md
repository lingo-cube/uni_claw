# PNL-001 部署记录（2026-09-30）

## 部署内容

- 四包经 `DSH_PROFILE_DIR=~/.dsh/profiles/web bash dsh/deploy.sh` 部署：
  `dsh-uniagent-restrict` / `dsh-decision-channel`（重建部署）、
  `dsh-task-workbench` / `dsh-task-scope`（首装）。
- Install markers：四包全 OK。
- Drift check：clean（22 文件 byte-identical）。
- `~/.dsh/profiles/web/cordis.patch.yml` 追加 `preset-uniclaw-task` 行
  （agent-preset，plugin `@uniclaw/dsh-task-scope`，config.allow =
  `['read','glob','grep','bash','skill']`）；YAML 解析 + 行形状断言通过。
  改前备份：`/tmp/cordis.patch.yml.bak-pnl001`。

## allow 名单依据（live catalog 逐名核对）

| 工具名 | 来源包（shared profile node_modules） |
|---|---|
| bash | @deepseek-ai/dsh-tool-bash |
| read / write / edit | @deepseek-ai/dsh-tool-fs |
| glob / grep | @deepseek-ai/dsh-tool-fs-search |
| skill | @deepseek-ai/dsh-tool-skill |

决策 6（读 + bash + skill 加载）→ 取 read/glob/grep/bash/skill；不含
write/edit/web_fetch/todo_write。

## 部署过程中发现并修复的两个 deploy.sh 缺陷

1. **首装 remove 失败**：`pnpm remove` 对尚不是依赖的新包报
   `ERR_PNPM_CANNOT_REMOVE_MISSING_DEPS` 而死。修复：仅该错误码容忍
   （视为首装跳过 remove），其余 remove 失败仍致命。
2. **pipefail × grep -q 假阴性**：`marker_present` 的
   `grep -v … | grep -Fq` 在 `set -o pipefail` 下，`-q` 命中即退出使上游
   `grep -v` 收 SIGPIPE(141)，把存在的 marker 判成缺失；decision-channel
   的 src/index.js 超过管道缓冲（64K）后必现（小文件如 restrict 不触发，
   属潜伏缺陷被本 change 撑大文件后显形）。修复：`-q` 改为完整读取 +
   `>/dev/null`。

## 环境备注

- 本机 `DSH_PROFILE_DIR` 默认指向 `~/.dsh/profiles/desktop`（无任何依赖）；
  @uniclaw 目标 profile 是 `web`，部署必须显式指定。
- 3080 实例正在运行（ego He 有活跃连接）；按 deploy.sh 硬规则，重启实例
  属 Owner 人工动作，未执行。

## 剩余 Owner 步骤（live 验证）

1. 重启 3080 DSH 实例（加载 preset 行 + 四包快照）。
2. 浏览器强刷（Ctrl/Cmd+Shift+R）。
3. 验收：侧边栏出现任务面板入口；新建任务（需求文本 + 项目路径）→
   实例化 → 会话出现在 GUI；对话/Trace/操作三 tab 可用；诊断按钮
   注入 prompt 生效。

## 平台 API 漂移发现与修复（2026-09-30 深夜轮）

技能 dsh-plugin-static-client-ui 的 recipe 出自旧构建；当前 dk-harness 构建的
typert API 有三处不兼容，均已实证并修复：

1. **strict codec 形状**：`result.schema:{parse}` → `result.create:()=>({parse})`
   （registry `validateCodec` 要求 codec 对象自带 create 工厂；TypertSchema 只需
   {parse}）。症状："strict codec has no create() factory"。
2. **参数 descriptor**：必须是 `{name, wire, source:'json', codec}` 对象数组
   （网关按 host 注册清单校验 wire 字段；字符串数组等于空声明）。
3. **host 方法按位置收参**（对齐 dsh-goal remoteExportCreate）：wrapper 需
   位置→对象打包后再进 panel 工厂函数。

## live 验证（shell 直连，绕开卡死浏览器）

- 认证：`GET /?token=<launchToken>` 铸签名 cookie（client/connection
  authorizeIndex），curl -c/-b cookie jar 即可通过 API 认证。
- `createTask` → success:true，task-8ef8db028b97645e 铸造 ✓
- `overview` → 任务列出（instanceCount:0）✓
- `~/.dsh/uniclaw-tasks/tasks.json` 磁盘持久化 ✓

## 未决

- GUI 面板视觉/点击流实测：ego 浏览器渲染进程整体 wedge（不能替用户重启），
  待用户自行强刷验证；client 半部 $mount 已按新 API 修复部署。
- uniagent-task preset 收窄机制：preset 作用域 name 级 allow-list 在本平台
  不可行（核心工具不在 preset 可见全局层，restrict 逐名校验 mount 即抛），
  preset 行已回滚，机制替代方案待 Owner 裁决。
- 建议：dsh-plugin-static-client-ui 技能按上述三处漂移更新 recipe。

## Q16 方向修正后的 live 验证（2026-09-30 第四轮）

- sessions()：真实 SessionRecord（header.id/createdAt）+ readTitleSnapshots 标题
  + uniclaw 实例标记，live 返回真实会话（含当日 worker 会话标题）✓
- trace({sessionId})：readSession 全量事件 → 序列化文本正则提取 UniFlow 语义
  条目；live 从真实会话提取到 COMPLETE outcome（含真实时间戳）✓
- listEvents 无载荷（SessionEventRecord 只有 seq/type/time/surface）——改用
  readSession 是最小真实数据源。
- 首次 sessions() 调用冷读较慢（>20s，sqlite 冷启动），后续快；GUI 使用可接受。
- 测试 25/25；四包部署 drift clean；launchd 实例自动重生验证 ×4。
