# ADR-0021: 感知 provider 是进程外黑盒；管道变体「可选择、不可携带」

## Context

感知服务（platforms/perception，Python）作为 Capability Plane 的 provider
进程存在（ADR-0020 的响应 JSON 即其输出）。随着管道可配置化（PER-008），
出现三种让调用方影响管道的方式：

1. 调用方在请求中**选择**一个预声明的管道变体；
2. 调用方在请求中**携带**管道配置内容；
3. 服务端运行期**热切换**配置（file-watch/信号/端点）。

同时存在框架化诱惑：引入 Ray/Triton/BentoML 等服务化框架以"降低编排
复杂度"。

## Decision

1. **Provider 边界 = 进程外黑盒**：Kernel C# 零引用 provider 树；交互只经
   传输契约（UDS/loopback TCP）；产品语义（identity/belief/decision）零
   进入 provider。
2. **变体可选择、不可携带**：请求 header `X-Pipeline-Variant` 只能命中
   启动时全量 lint 通过的预声明变体（未知 → 400）；请求体/header 永不
   携带配置内容。变体各持独立 configId/deploymentId（身份随选择走）。
3. **热切换 defer**：重开条件 = 长生命周期生产服务 + 零重启配置轮换的
   真实 buyer（watchfiles 已在 venv，机制上 identity epoch 已备）。
4. **不引入分布式/服务化框架**：Ray/Triton/BentoML 的触发条件（多节点/
   多副本/集群采样/队列积压）当前零满足；编排复杂度的正解是进程内 stage
   抽象（pipeline.py）+ 既有 ruleset 治理框架。

## Considered Options

1. **请求携带配置内容**（rejected）：provider 内部语义泄漏给消费方决策
   （黑盒边界倒置）；未 lint 配置进运行时破坏 fail-closed；身份按请求
   抖动，epoch 语义崩溃。
2. **热切换现在做**（rejected）：Host 掌控进程生命周期，重启 ~2-6s；实验
   场景由变体选择覆盖；零 buyer。
3. **Ray Serve 现在引入**（rejected）：单机单服务两模型秒级延迟，分布式
   收益零 buyer；代价 = 常驻 worker 集 + 足迹 + 生命周期纠缠。
4. **变体预声明 + 请求选择**（accepted）：A/B 对照（同一帧跑两配置）是
   真实 buyer（OPT 系列后端/模型对照实验），且选择语义天然携带身份。

## Consequences

- 配置的错误面收敛在 provider 启动期（fail-closed 单点），运行期请求面
  只有「合法选择」一种成功路径。
- 消费方（Kernel/评测脚本）对管道内部演进无感知——新增变体/换后端 impl
  （OPT-001）对契约零影响。
- 「动态配置」的完整形态（热切换）被显式推迟而非禁止：重开条件成文，
  届时决策有据可依。
- 拒绝框架引入的代价：若未来真出现多节点采样，需评估迁移成本——但届时
  buyer 明确、边界清晰（只迁 detect/recognize 推理段，fusion 纯 CPU 逻辑
  无分布式收益）。

（谱系：PER-005 D1/D3/D9 → PER-008 grill Q1/Q9/Q3/Q8 锤定。）
