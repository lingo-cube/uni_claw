# PLAN — RUNTIME-TASK-001 · Task 组件一等化

> WHAT/WHY/ACCEPTANCE：`changes/RUNTIME-TASK-001/state.md`（D1–D17、A1–A6）。
> 方案视图与交接契约：`changes/RUNTIME-TASK-001/solution.md`（§1.7）。
> PLAN 基线（PLAN 内可调）：O2＝slug+ID 后缀；O3＝不迁移+双读；
> O4＝(a)→(b 含 d)→(c)；O5＝verify-only；重试＝两分法（提交重试/再执行）。
>
> 纪律：TDD（每片先 RED 后 GREEN，垂直切片：行为→模型→运行时→测试→证据）；
> 不改 `TaskInstanceStore` "预留 1:N"注释；不动基线 §22 未锁定语义；
> 不做多 Run / 通用 Catalog / 存量迁移 / provenance / 场景库改动。

## 切片 0 · 定形（无代码）

| # | 定形项（详见 state.md "PLAN 内定形项"） | 输出 |
|---|---|---|
| T1 | 声明块 schema：workbench 计算（manifest 解析结果）→ launch 请求携带 → Runtime 原样持久化 | 字段表 + 请求/记录形状 |
| T2 | 实际实现指纹：哈希路径集 + launch 路径性能预算（区分同提交不同工作区） | 指纹定义 |
| T3 | 验收记录形状：与钉扎同记录或伴生；只记录不判定（T6 边界） | 记录 schema |
| T4 | instance 查询端点：`GET /api/uniclaw-runtime/tasks/{taskInstanceId}` | 端点契约 |
| T5 | 声明指纹比较范围（P-A）：参与/排除清单 + 钉扎-执行一致性 | 比较规范 |
| T6 | 验收权威边界（P-B）措辞 | 已先行落入 solution.md §1.7 |

## 切片 1 · admission 闭环（评审指定先行；O4a+O4d 核心）

行为链：**创建实例 → 持久化声明输入 → 重复提交/冲突 → 重启查询**。

RED（先写测试，对应 A6-①②③④）：

1. 同 idempotencyKey + 同声明块 → 复用原记录（同 taskInstanceId，返回原声明）；
2. 同 key + 异声明块 → 显式拒绝 `input-conflict`（4xx，原记录不变，不覆盖）；
3. store 重建（模拟重启）→ 身份与钉扎不变；
4. 未产生 Run 的实例经查询端点可读（含声明投影）。

实现（最小）：

- `TaskInstanceRecord` 增声明块字段（形状＝T1/T3；append-only 语义不变，
  **不触碰"预留 1:N"注释**）；
- `CreateTaskInstanceAsync` 接收声明块 + 冲突分支（先持久化后返回成功）；
- 新增只读查询端点（T4）；
- workbench launch 组装声明块（消费既有 manifest 解析，index.js:186-196）。

验收映射：A6-①②③④。

## 切片 2 · 存储布局 + 执行投影（O4b + O4d 余项）

- 介质根改名（D1；`runDirName` 29 处调用点 + tool/HTTP/CLI 三层契约同步）；
- `<task>/<instance>/` 两层布局 + `task.json`（引用+digest，D7）+ `_untasked/`（D9，数量可统计）；
- metadata.json 投影：身份 + 两层指纹；CLI 与 HTTP 双路径都写（F4）；
- 实际实现指纹落 run 记录（F10，形状＝T2）；
- 旧布局双读 + 旧报告来源可读（O3 基线；required-reports 绝对路径 + fail-closed 不动）；
- 反向测试：篡改目录名，身份查询结果不变（A5）。

RED 关键：同提交不同工作区 → 指纹不同（A6-⑤）；旧 run/旧报告可读（A6-⑥）。
验收映射：A1/A2/A3/A4/A5/A6-⑤⑥。

## 切片 3 · workbench 接线（O4c）

- `psi-*` 兼容路径显式标注/退役（ADR-0045 known deviation 收口）；
- workbench 消费 instance 查询端点（工作台展示声明与钉扎）。
验收映射：切片 0 定形后补四元组。

## 显式不做

多 Run 语义、通用 Task Catalog 实现、存量数据迁移、场景 provenance 字段、
验收判定器实现（本 change 只做记录侧）、仿真链接入发起链（D15 留门）。
