# UniClaw Core 提取：vNext 最小候选基线审阅版

> DocumentType: `CORE_EXTRACTION_TO_SPEC_REVIEW`
>
> Status: `CANDIDATE / REVIEWED`
>
> Authority: `NONE`
>
> Review Lock: `CORE-007`（锁定由 Change State 承载，不等于冻结源码字段）
>
> Scope: 基于 vNext.1 的最小候选 Core 与 UI realization 首版边界。

## 1. 参考与解释规则

`UniClaw_Core_Model_设计文档_vNext.1.md` 是本版的参考设计，负责提供语义、约束和字段
等级。vNext.1 的字段表不是“全部字段必须实现”的清单。当前审阅结合场景库、已有
审阅汇总、Kernel 测试和现有投影缝，选择第一版最小候选实现。

本版结论是“当前材料支持的最小候选基线”，不是严格最小性证明。后续反例可通过新的
Change 修订；旧字段、旧类签名和 UI 实现结构不构成架构裁决。

## 2. 最小候选模型

```text
Core = Specification + World + Effect

Specification
└─ Clause                         核心记录 1

World
├─ Segment                        核心记录 2
│  └─ Slice[]                     World 内部局部观察结构
├─ Evidence                       核心记录 3
└─ Claim                          核心记录 4
   └─ 发生、归属、因果语义按命题表达

Effect
└─ Effect                         核心记录 5
   └─ Attempt[]                   Effect 内部执行结构
      └─ TargetBinding[]          Attempt 内部绑定结构
```

Event 的发生语义不得删除，但暂不冻结独立 Event Core 记录。独立 Event 只有在具体
表达或演化反例证明 Claim + Evidence + 可靠引用无法保留发生归属、次数、时间、参与者、
依据和更正历史时才进入后续裁决。

## 3. 字段审阅规则

沿用 vNext.1 §4.3 的字段等级：

- **B 建档必填**：缺失则无法登记完整候选记录；不等于已授权或已证明。
- **C 用途条件必填**：只有依赖该用途时才必须具备，可由固定引用还原。
- **A 内部集合**：默认空，不从空推断不存在、未发送或失败。
- **H 发生后追加**：有真实过程或反馈后追加，不能预填伪造。
- **O 展示选填**：不参与权限、定位、安全和结果判断。

字段减法必须通过两种检查：表达保持（必要问题答案不变）和演化保持（加入新证据、
更正或迟到反馈后仍保持必要判断）。

## 4. 各记录的第一版字段边界

### 4.1 Clause

当前 UI 版本只投影显式 `Objective` 和 `ProofCriteria`，分别作为
`Requirement` 与 `Criterion`。`Permission` 只有在存在独立授权来源、范围、有效期和
时间规则时才接入；不能从 `AllowedEffects`/`ForbiddenEffects` 的短字符串自动制造权限。
其他来源、权威和版本关系在某个用途实际依赖时才是 C 条件必填，不能因为 vNext.1 列出
就全部塞入 Core。

### 4.2 Segment

建档只要求持续引用和语义类别/登记上下文。编号不证明现实身份；身份、存在、归属和
生命周期用 Evidence + Claim 表达。Slice 集合可为空。

### 4.3 Slice

Slice 是 Segment 在明确来源版本、观察时间、范围和解释规则下的局部观察或派生表示，
不限定视觉页面。机器人局部地图、文件目录子树、API 分页结果和数据库查询窗口都可以
使用 Slice。只有局部范围表达确实需要时才建立；必须能固定关联所属 Segment 快照和至少
一份 Evidence 依据。Representation、DerivationRef、ContextRef 在坐标系、单位、融合
或历史重建需要时才是 C。来源、被观察对象、局部范围、原始观察时间和处理版本分开表达。
Slice 可以重叠、包含和重复覆盖，新 Slice 不覆盖旧 Slice；重新处理旧数据不等于重新观察。

### 4.4 Evidence

建档必须保留来源类型、内容引用、观察时间/未知值和基本处理链。采集范围、设备、
标定、Schema、MethodRef 等只有在判断或定位依赖时才是 C。证据入库不等于命题被采纳。

### 4.5 Claim

建档必须能表达可修正命题及其来源。Basis、适用时间、冲突、更正、事件归属和因果
关系按用途进入 C；发生型 Claim 必须能指向所描述的发生，不把两条判断误当两次现实发生。

### 4.6 Effect

建档必须能表达逻辑操作和目标主体。操作参数/契约、授权条款、预期、补偿/重试关系和
其他关系在对应运行用途需要时才是 C；当前 UI 投影未提供参数/契约时保持未提供，并只
阻塞依赖它的用途。Effect 不表示已经执行成功。

### 4.7 Attempt 与 TargetBinding

Attempt 和 TargetBinding 是执行内部结构，不计入五种核心记录数量。当前 UI 版至少
保留实际尝试时间、投递状态、Effect 关联、当次 Binding 关联和固定历史 basis。Attempt
表达某个 Effect 的一次有产品含义的执行尝试，包括当次请求、绑定、投递关联及反馈依据；
它不是权限记录，也不证明外部目标完成。
请求快照、执行端、授权依据、三条执行状态轴、定位依赖和外部反馈在 UI Runtime 能提供
时接入；缺少时保持 Unknown/未提供，不能伪造。

TargetBinding 至少有目标引用、定位材料和一项可解析固定依据。Slice 是 UI 版的一种
basis，不是跨领域强制类型；当前 UI 投影入口具体接收 Slice basis；ResourceVersion、
Evidence 或 Claim basis 留给未来真实 realization。

### 4.8 Attempt 与 Trace

Attempt 可以直接保存，也可以从满足完整性、历史留存和恢复要求的执行源记录生成。普通
诊断 Trace 不自动具备这些保证，不得作为唯一尝试依据。发送前应可靠登记当次请求、目标
绑定和尝试关联；这里的登记表示准备信息已留存，不表示请求已经发送。发送过程、Receipt、
外部执行和结果判断分别追加或关联。Trace 可以引用
或展示同一执行关联，但不得与正式执行记录形成两套可独立改写的尝试事实。没有 Receipt
或 Trace 不得自动解释为没有尝试或没有外部影响。

## 5. UI realization v0.1

现有 `UniClaw.Kernel.Core.CoreSemanticProjection` 作为唯一 UI projection seam：

```text
Accepted ExecutionContract
        ↓ 只投影明确的 Objective / ProofCriteria
Core Clause (Requirement / Criterion)

UI Evidence / World / Slice / Claim
        ↓ 只读投影
Core Evidence / Segment / Slice / Claim

UI CanonicalBinding / EffectReceipt
        ↓ 只读投影
Core Effect / Attempt / TargetBinding
```

UI occurrence、DOM/ADB/native locator、设备驱动、Runtime receipt 生命周期和 World 写入
继续由 Kernel 维护。投影不得回写 World/Evidence/Binding，也不得从 receipt 直接制造
World result。AllowedEffects/ForbiddenEffects 在当前 UI 版仍是 Runtime contract 事实，
没有明确授权来源和范围时不投影为 Permission。UI 版本不决定机器人、文件/API 或仿真
版本的字段。当前 UI 尚未验证“关闭普通诊断 Trace 后，系统仍能否还原准备/尝试、请求、
绑定和未知结果”；该问题属于执行记录契约验证，不通过增加 Core 类型解决。

## 6. 不可外推

- 五种核心记录不是五个基类、五张表或最终公共 API。
- vNext.1 的 C 字段不能在没有用途的情况下变成 B 字段。
- Slice、Attempt、TargetBinding 不因“内部”而允许丢失历史语义。
- Event 暂无独立类型不等于可以删除发生历史。
- Attempt 不是 Trace 的改名；Trace 采样、清理或缺失不能被解释为没有 Attempt。
- Slice 不是视觉专用类型；处理版本变化不等于新的观察时间。
- UI 投影通过不等于 Core 已完成跨领域迁移。

## 7. 参考与证据

- vNext.1 §3–§12：三模型、字段等级、各语义职责、Slice/重叠、Effect/Attempt/Binding、
  使用关口和不变量。
- `/Users/fran/.codex/attachments/52556104-b098-472c-8962-d63ce40813bc/pasted-text.txt`：
  作为审阅辅助，支持“最小候选基线”而非“严格最小 Core”的表述。
- `evidence/2026-09-19-core-bidirectional-validation.md`：现有 UI 投影和下层 gate。
- `evidence/2026-09-19-vnext-field-minimal-review.md`：本版字段审阅矩阵与锁定结果。
- `docs/design/core-execution-record-contract-v0.1.md`：CORE-009 可靠执行记录契约设计稿；
  当前为 `CHANGES_REQUIRED`，不代表 Runtime/Trace 实现已完成。

本版只锁定语义裁剪和 UI 首版范围；源码迁移、第二 realization、最终字段和存储协议
仍未完成。
