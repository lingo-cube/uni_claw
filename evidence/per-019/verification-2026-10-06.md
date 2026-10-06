# PER-019 验证证据 — UniPerception 异步组件（2026-10-06）

> 环境：emulator-5556 + 专用 3081 + 7890 代理。命名裁决：UniPerception（id `uni.perception`）。

## 确定性（全绿）

- 桥（DshSlowConsult）Fetch/Project 分离后 8/8（Agent.Dsh 全量 140/140）。
- 流水（UniPerceptionPipeline）6/6：发射即时返回（周期不等）、晚到 IsLate 投影、
  路由变更诚实丢弃（unaligned-route）、超时零投影、预算发射时计、kernel 落地
  producer=`uni.perception`。
- Host.Tests 165/165、Kernel.Tests 798/798、注册表测试（uni.perception）5/5。

## 真实回合（glm-5.3-flash；rounds/async-verify2）

- cycle 17：`SemanticUnclear|Dispatched|in-flight=1`（发射即返回，该周期
  fast=0.81s/hier=1.93s 正常——**零阻塞**）。
- cycle 18：`SemanticUnclear|Dropped|unaligned-route:android.settings→android.settings|rk1:Settings|…`
  ——模型往返完成后路由已解歧，**晚到结果诚实丢弃**（丢弃即正确：问题已过时）。
- 首轮（async-verify）暴露两个真缺陷并修复：发射时漏 basis SessionCorrelation 对齐
  （门控 Misaligned 误拒）；Program facts 落盘位置错（NRE）。修复后 facts OK。

## 组合根注册（run dir capability-facts.json）

`uni.perception`（CompositeProductPerception，Registered，seq1）+ `slow.visual`
（IndependentProductPerception，Registered）——描述零 provider/model 名。

## 模型默认切换（所有者指令：deepseek-flash）——如实记录的不兼容

- 配置已切：uniagent-prod `selected: deepseekFlash`（opencode-go/deepseek-flash），
  运行确认 `agent.model=deepseek-flash` 生效。
- 真机两轮（async-deepseek3/4）：**每次咨询都以 `no-submit-decision: model finished
  the turn without calling submit_decision` 失败**（链路诚实 fail-closed：零 effect、
  BoundedStop 17%、rc=1）。与 2026-10-02 记录的 deepseek 系模型行为不稳同款
  （model-bindings.yaml 注释）。两轮同败=确定性不兼容，非偶发。
- 处置：默认保持所有者指令值；链路当前不可用 deepseek-flash 驱动，待所有者裁决
  （prompt/tool-call 加固、换 provider 配置、或回退 glm53Flash）。
