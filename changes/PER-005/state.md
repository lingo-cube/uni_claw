# PER-005 — Perception Acquisition Live（三件套 adapter：截屏 + Live Vision Strategy + 服务 host）
lifecycle_state: closed · disposition: implemented · depth: decision-heavy · base: 9521e0e

## Intent（WHAT/WHY）
ADB-001/002 后「手」（效果驱动）已真机 live，「眼」仍是 corpus/replay——v0.1
端到端闭环的最大缺口。本 change 落地感知 acquisition 真实化：设备截屏 →
legacy Python 感知服务（黑盒复用）→ 双 artifact → ObservationProposal →
EvidenceLedger admitted。证据基础 = `docs/analysis/`
`uni-agent-perception-implementation-analysis.md`（§7 方案，2026-09-11 经
grill-with-docs 两轮锤定 D1–D10，Human 确认）。

## Scope
- ① Screenshot Acquisition：ADAPT `AdbScreenshotSource`（49 行；
  `adb exec-out screencap -p` → PNG `RawArtifact.Capture`；CaptureTime =
  host clock，只作 temporal provenance，ADR-0010）。
- ② Live Vision Strategy：`IFastPerceptionStrategy` 实现，调服务（JPEG q92
  → `/v1/analyze`），**payload = 服务响应 JSON**（derived artifact，
  `TransformationLineage` 记 `screenshot → vision-service:v1/analyze`），
  subject 命名沿用 corpus 导入约定（`ui.text.*` / `ui.detect.{id}.class` /
  `spatial.artifact.bounds.*` / `perception.page.signature`）。
- ③ Vision Service Host：服务进程拉起/健康/关闭（REFERENCE
  `VisionServiceHost` 状态机思想，大幅简化；**不进 Kernel**，属 Capability
  Plane provider 进程管理）。
- ④ 环境拉起：Python 3.11 venv + RapidOCR 最小集 + 模型物化（uni-agent
  git blob：YOLO best.pt 6.2MB / en_PP-OCRv4 7.7MB）+ `/version` 健康。
- ⑤ transport：`uds | tcp` 双态配置；TCP 显式启用且仅 loopback。
- ⑥ P-1 同源对拍（DETERMINISTIC）+ P-2 现场全链（ENVIRONMENT）验收。
- ⑦ ~~CONTEXT.md 新增「Perception Acquisition」词条~~ **【2026-09-12 收尾修订】
  剥离至后续独立 change PER-006**：并发会话 UAR-001 处于活跃返工状态（SOL
  Review CHANGES_REQUIRED，正编辑 CONTEXT.md glossary），文件持续被持有；
  代码/验收面已全部完成并 verified，词条是纯文档项且 PER-004 先例表明
  词汇锁定本身是独立 change 单元——按 A7 修订留痕，范围手术出本 change。

## Out of Scope（禁止）
- §7.2 不开启清单：`X-Known-Rows` 跨帧行稳定（continuity 权威在 World
  Model，ADR-0014/0015）、switch state 读取、uiautomator 结构化 producer
  （另立 change）、semantic embedding 管线（等 buyer）。
- TCP 非 loopback 地址（认证/加密/远程部署治理）。
- provider 内部任何改动（fusion 启发式/模型/服务代码原样物化）；
  confidence / row_id 无 buyer 不进 payload。
- 不改 WorldModel / Control / Assurance / EffectBoundary 语义；不迁移
  legacy Observation 模型（两套观察模型并存 = 第二真相）。
- 前台 app 假设不迁移（legacy 世界真相字段，A0 REJECT 同族）。

## Decisions
| # | 决策 | 来源 |
|---|---|---|
| D1 | 部署配置驱动，默认宿主本机 macOS + UDS | Q1 |
| D2 | 单 change 三件套（acquisition / strategy / host 拆开则中间态不可验收） | Q2 |
| D3 | transport `uds \| tcp` 双态：默认 UDS；TCP 显式启用仅 loopback；**transport 只改连接方式，不改响应解析 / Artifact / ObservationProposal 语义** | Q8=B |
| D4 | 验收双腿：DETERMINISTIC 必绿 + ENVIRONMENT P-2；环境失败走 BLOCKED 不砍验收 | Q3 |
| D5 | parity 两级：P-1 同源对拍（corpus golden-run-v1 响应 JSON，零环境依赖）+ P-2 现场帧 | Q4 |
| D6 | P-2 锚 = 标准测试模拟器 `p26_pixel / emulator-5554`（真机附加证据）；全链须 ≥1 合法**非空** proposal，`OK_EMPTY` 单独不算通过 | Q9=A′ |
| D7 | 环境 = Python 3.11 + RapidOCR 最小集（torch 2.2.2 / torchvision 0.17.2 / ultralytics 8.4.115 / rapidocr-onnxruntime / onnxruntime / pillow / numpy 1.26.4 / fastapi / uvicorn；**跳过 paddle**）；paddle 被配置必须启动失败 + 明确诊断；版本保真 uni-agent runtime.txt 实测 pin（无 CI lockfile，runtime.txt + canonical venv 即其测试情况），最终闭包以实际环境 lock + 启动验证为准；环境验收 = `/version` + 模型/OCR warmup + 一次非空推理，安装成功 ≠ 运行证明 | Q10=A′ |
| D8 | 环境拉起并行于 adapter/P-1，失败只阻塞 ENVIRONMENT 腿 | Q7=B |
| D9 | 不重开：provider 黑盒；**双 artifact**（确定性锚 = 响应 JSON，PNG 留原始证据/slow path 输入；锚 PNG 会被 JPEG/编码器跨版本漂移破坏）；无 buyer 不加字段 | grill 既定 |
| D10 | 语义免疫声明：parity 对 env 漂移免疫（P-1 不跑模型、P-2 自我对拍）；保持 torch pin 是保真选择而非验收依赖 | 事实核查推导 |

## Assumptions
- torch 2.2.2 / torchvision 0.17.2 / onnxruntime 1.23.2 在 arm64 macOS 有
  wheel（本机 arm64 + python3.11/3.12 已确认在位；若个别 pin 装不上，按
  D7 以实际 lock 修正并在本 state 留痕，不静默换版本）。
- `tests/.../Corpus/legacy-direct/golden-run-v1/case-a-before.json`（22.6KB
  真实服务响应）+ 同名 PNG 可作 P-1 锚（已 committed）。
- 标准测试模拟器可按 ADB-002 注册方式拉起（`docs/agents/test-emulator.md`）。
- 服务 `OK_EMPTY` → 零 observation（与「missing detection 不产 observation」
  现状一致，无需新语义）。

## Alternatives（被拒）
1. 拆 2–3 个 change（acquisition 无独立 buyer；strategy 无 acquisition
   喂不进真帧）——拒。
2. 仅 DETERMINISTIC 或仅 ENVIRONMENT 验收——拒（分别自欺于环境/语义）。
3. 仅 UDS 单态配置（先固化 `socketPath` 再立即迁移协议形状）——拒（Q1 已
   定配置驱动，双态现在改最小）。
4. 完整安装 runtime.txt 含 paddle 全家桶——拒（高风险零收益，rapidocr 是
   D-198 默认）。
5. 环境先行作为阻塞前置（Q7=A/C）——拒（环境是部署面不是语义面）。
6. PNG 作确定性锚——拒（编码器跨版本不确定；见 D9）。
7. 设备侧部署——拒（无先例，需重解决 Python/模型部署）。
8. semantic 管线 / X-Known-Rows / switch state 随行——拒（§7.2 各带重开条件）。

## Owner-Authority impact
- Perception 仍非 Authority（PER-004 词条不变）：acquisition provider 只产
  RawArtifact 与 observations；identity / continuity 权威仍在 World Model。
- 服务失败 → 零 proposal fail-closed + 诊断码（absence 逐边显式，协议通则 6）。
- Host 专有语义零进入共享层；服务 host 是 Capability Plane provider 组合件。

## ADR refs
- 无新 ADR（双 artifact / transport 边界等记本 change state；重踩按
  WMP-004 先例升格）。
- 既有相关：ADR-0010（freshness 非 wall-clock 权威）、ADR-0011（consumer
  views）、ADR-0014/0015（continuity——X-Known-Rows 不开启的依据）。

## Residual risks
- 个别 pin 的 arm64 wheel 可用性未证实（D7 兜底：实际 lock 修正留痕）。
- 模拟器现场帧与 corpus 帧分布差异 → P-2 proposal 数量不可预知（验收只要求
  ≥1 非空，不设上限断言）。
- CONTEXT.md 词条提交时点受并发会话制约（Scope ⑦）。

## Acceptance
- A1（DETERMINISTIC·P-1）golden-run-v1 `case-a-before.json` 分别走 live
  strategy 解析与 corpus 导入路径，subject/value 集合语义一致（replay parity）。
- A2（DETERMINISTIC·失败路径）服务进程死亡 / 超时 / 坏 JSON /
  `INVALID_GEOMETRY` → 零 proposal + 可观察诊断码，无部分产出；paddle 被配置
  → 启动失败 + 明确诊断，不静默降级。
- A3（DETERMINISTIC·架构断言）adapter 不依赖 WorldModel / EvidenceLedger /
  identity 语义；confidence 不进 payload；同 artifact → 同 observations。
- A4（ENVIRONMENT·环境）`/version` 健康 + 模型/OCR warmup + 一次非空推理
  通过（安装成功 ≠ 运行证明）。
- A5（ENVIRONMENT·P-2）标准模拟器全链：截屏 → 服务推理 → 响应 JSON
  artifact → 确定性解析 → ObservationProposal → `EvidenceLedger.Admit`
  admitted，≥1 合法非空 proposal（`OK_EMPTY` 单独不算通过）；真机在手则
  附加证据。
- A6 全量回归零破坏（基线 328 = Kernel 311 + Agent 17；届时以最新 HEAD 为准）。

## Verification
```yaml
verification:
  level: SCENARIO（A4/A5 ENVIRONMENT 全链；A1–A3 DETERMINISTIC；A6 回归）
  method: >-
    A1 同源对拍（golden-run-v1 响应 JSON → live strategy vs corpus 导入）+
    A2 失败分类矩阵（TCP double 全族 + UDS 连接拒绝 + host fail-loud 脚本
    double）+ A3 grep 证明（六文件零 WorldModel/EvidenceLedger/identity 引用）
    + A4 真实服务（UDS /version + warmup + 非空推理 + paddle fail-loud）+
    A5 模拟器现场全链（截屏→推理→derived artifact→FastPerception→admitted
    非空）+ A6 全解决方案回归。DSH_TEST_PERCEPTION_LIVE=1 门控（默认跳过，
    启用后 fail-closed，test-emulator 注册约定）。
  expected: A1–A6 全满足
  actual: >-
    全绿：parity 7/7、client 11/11、host 5/5、png/acquisition 10/10、
    env 3/3（A4 两例 + A5）、全量 Kernel 347/347 + Agent 17/17（基线 328
    + 36 新增，既有零改动）。环境一次拉起成功（torch 2.2.2 等 pin 在
    arm64 全可用，D7 假设证实零修正）。唯一 provider 资产触碰 =
    en_PP-OCRv4_dict.txt 94→95 行修复（对齐其自身 manifest 注册的
    "95-char"——94 行时 rec 解码 IndexError，A4 首败根因；仅修物化副本，
    uni-agent 零改动，setup.sh 幂等复现）。
  evidence: evidence/2026-09-12-per-005-perception-live.md
```

## Status log
2026-09-11 · enter→understanding→resolving · 证据基础 = 两份 CANDIDATE 分析
  文档 + PER-002/003/004 已锁边界；subagent 事实核查（服务 303 文件/37MB、
  模型 git blob、本机 arm64 + py3.11/3.12、corpus P-1 锚在库、纯 UDS、
  C# 源 49/271 行）。
2026-09-11 · resolving→persisted · grill-with-docs 两轮锤定 D1–D10（Q1 配置
  驱动默认本机 / Q2 单 change / Q3 双腿 / Q4 两级 parity / Q5 文档独立入库 /
  Q6 PER-005 / Q7=B / Q8=B uds|tcp loopback / Q9=A′ 模拟器锚+非空收紧 /
  Q10=A′ RapidOCR 最小集+paddle fail-loud）；Human 确认共识后执行：分析
  文档独立提交（9521e0e）+ 本 state。CONTEXT.md 词条因并发会话 in-flight
  延后（Scope ⑦）。
2026-09-12 · persisted→planned→implementing · 环境拉起后台并行（一次成功）；
  六产品文件落地（PngImage 零依赖解码 / AdbScreenshotAcquisition 复用
  IAdbProcessRunner 新增 RunCaptureAsync / VisionServiceTransport uds|tcp
  loopback-only / VisionServiceClient analyze_raw 全失败分类 /
  LiveVisionStrategy = import.cs DIRECT 映射镜像 / VisionServiceHost 简化
  生命周期）；JPEG 路线改 analyze_raw（Kernel 零 NuGet 依赖 + 无编码器
  漂移，D9/D10 语义不变）。
2026-09-12 · implementing→reviewed · 自查修复四坑：请求头误放 content 头、
  脚本 double BOM→ENOEXEC、HttpClient-UDS 面须 UseProxy=false、testhost 内
  手写 double×UDS wedge（改真实服务承担 UDS 全链 + double 只做确定性子集，
  evidence 留痕）；governance 跨包 import（evaluation/persistence）→ 物化
  改全树−training；dict 94→95 行修复到自身注册。
2026-09-12 · reviewed→verified（暂不 closed）· A1–A6 全绿（见 Verification 与
  evidence 文件）；唯一未闭环 = Scope ⑦ CONTEXT.md 词条——并发会话
  （UAR-001）仍持有 CONTEXT.md 未提交改动，按 RVR-002 F1 clobber 避让先例
  延后，词条落地后即 closed。
2026-09-12 · verified→closed · Human 裁决收尾：UAR-001 转入活跃返工
  （CHANGES_REQUIRED，正改 CONTEXT.md），CONTEXT.md 释放时点不可预期；
  Scope ⑦ 词条剥离为独立 PER-006（A7 范围手术留痕于 Scope）——代码、
  验收、证据、分析文档全部同步，无未授权改动，PER-005 本体 CLOSED。
