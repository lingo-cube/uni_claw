# PER-005 Evidence — Perception Acquisition Live（A1–A6 验证记录）

> Date: 2026-09-12 · level: A1–A3 DETERMINISTIC / A4–A5 ENVIRONMENT / A6 回归
> 环境: macOS arm64 · Python 3.11（venv，torch 2.2.2 / ort 1.23.2 / numpy
> 1.26.4 / rapidocr-onnxruntime 1.4.4 / fastapi 0.141.1 / uvicorn 0.52.1——
> runtime.txt pin 原样，arm64 wheel 全部可用，零版本修正）· 标准测试模拟器
> p26_pixel / emulator-5554（docs/agents/test-emulator.md 注册环境）。

## A1 — P-1 同源对拍（replay parity）

- method: golden-run-v1 `case-a-before.json`（真实服务响应，committed corpus）
  → `RawArtifact.Capture` → `LiveVisionStrategy.Observe`，与 corpus 导入路径
  （`tools/legacy-perception-import/import.cs` DIRECT 映射产物）集合对拍。
- expected: subject/value 集合语义一致（16 yolo + 11 ocr → 54 observations）。
- actual: `LiveVisionStrategyParityTests` 7/7 GREEN（含同 artifact 确定性、
  OK_EMPTY 零 observation、ocr 顺序索引约定、versioned seam 稳定）。
- 备注: 确定性锚 = 响应 JSON（D9 双 artifact）；PNG 不承担 replay 语义。

## A2 — 失败路径 + fail-loud（DETERMINISTIC）

- `VisionServiceClientTests` 11/11 GREEN：TCP 面 500→InfrastructureFailure /
  坏 JSON→MalformedResponse / 缺 envelope→SchemaFailure / diagnostics
  INVALID_GEOMETRY→InvalidGeometry / 不响应→Timeout / 连接拒绝→
  InfrastructureFailure / RGBA 长度不符 fail-closed / transport 结构性
  loopback-only（LoopbackTcp 无 host 字段）。
- UDS 确定性覆盖：连接拒绝分类 + endpoint 装配（`Uds_*` 2 用例）。
- `VisionServiceHostTests` 5/5 GREEN：进程非零退出 fail-loud（exit code 断言）/
  stderr 尾部捕获（paddle fail-loud 代理：ModuleNotFoundError 进 StderrTail）/
  探活超时 / Dispose kill 幂等 / 参数校验。
- 已知环境怪癖（留痕）：进程内手写 HTTP double × HttpClient-UDS-
  ConnectCallback 在 testhost 内会 wedge（裸 UDS socket 往返与真实 uvicorn
  服务均正常；同一 double 在独立 console 程序内亦正常）。UDS 全链 happy
  path 由 A4/A5 真实服务承担（默认 UDS transport），不在测试内复刻 double。
- 踩坑记录（均已修）：HTTP 请求头误放 content 头（服务端读 request.headers
  → 500）；shell 脚本 double 用默认 WriteText 带 BOM → ENOEXEC（改
  UTF8Encoding(false)）；HttpClient 在 UDS 面（BaseAddress 为主机名）需
  显式 UseProxy=false。

## A3 — 架构断言（DETERMINISTIC + grep 证明）

- 感知 live 六文件（PngImage / AdbScreenshotAcquisition / VisionServiceTransport
  / VisionServiceClient / LiveVisionStrategy / VisionServiceHost）grep 零引用
  WorldModel / EvidenceLedger / ContainerIdentity / AssociationDisposition；
  零 `using UniClaw.Kernel.World|Evidence`（唯一命中为既有 FastPerception.cs
  文档注释中的禁止性说明——P2 seam 自身职责文档）。
- confidence 不进 payload：LiveVisionStrategy 仅读 id/label/text/boundsPx
  （A1 parity 集合断言间接钉死——corpus 导入同样不含 confidence）。
- 同 artifact → 同 observations：确定性用例 GREEN（FCR-001 cache 前提）。

## A4 — 环境验收（ENVIRONMENT，DSH_TEST_PERCEPTION_LIVE=1）

- `A4_Environment_ServiceHealthyAndNonEmptyInference` GREEN：UDS host 拉起
  真实 uvicorn → /version 健康（lifespan 含 YOLO+OCR warmup）→ corpus 真机帧
  PNG 解码（1080×1920）→ `/v1/analyze_raw` 非空推理（yolo+ocr > 0）。
  安装成功 ≠ 运行证明——非空推理是必要条件（D7/Q10=A′）。
- `A4_PaddleConfiguredButAbsent_StartupFailsLoud` GREEN：
  `UNICLAW_OCR_BACKEND=paddle` → 启动失败 + stderr 现场（fail-loud，无静默
  降级）。

## A5 — P-2 现场全链（ENVIRONMENT）

- `A5_LiveFullChain_EmulatorScreenshotToAdmittedNonEmptyProposal` GREEN：
  emulator-5554（Wi-Fi Settings 注册入口）→ adb screencap → PNG capture
  artifact → 服务推理 → 响应 JSON derived artifact（CaptureScope =
  `derived:vision-service:{screenshot-artifact-id}`，D9 双 artifact lineage）→
  `FastPerception("perception.live.vision", LiveVisionStrategy)` → ≥1
  ObservationProposal → `EvidenceLedger.Admit` **全部 Accepted**（P2 绿，
  D6 非空要求满足）。
- ENVIRONMENT 面输出（本文件同批运行）：
  `已通过! - 失败: 0，通过: 3，总计: 3，持续时间: 8s - PerceptionLiveEnvironmentTests`

## A6 — 全量回归

- `dotnet test UniClaw.Kernel.slnx`：**Kernel 347/347 + Agent 17/17** GREEN
  （基线 311+17=328，+36 新感知测试，既有零改动——AdbLiveDriverTests 仅加
  新接口成员 stub）。

## provider 资产修复（对齐其自身注册记录——唯一触碰 provider 资产处）

- `ocr/models/en_PP-OCRv4_dict.txt`：uni-agent git 内 94 行（= 可打印 ASCII
  33..126），但其 governance manifest 注册为 **"95-char en rec dictionary
  (PP-OCRv4 structure)"**（manifestId ocrm:d834eeb9…）。95+blank+space = 97
  = rec ONNX 输出维度（实测 session output shape[-1]=97）；94 行时 rapidocr
  解码在 index 96 抛 IndexError（实测 A4 首败根因）。缺行 = 空格行。
- 处置：仅修复物化副本（`.perception/provider/`，gitignored）+ setup.sh 幂等
  步骤（wc -l = 94 时补 `printf ' \n'`）；**uni-agent 分支零改动**；D9 语义
  = 修复到其自身注册内容，非行为修改。

## 环境物化补充（setup.sh 内留痕）

- governance 存在跨包 import（evaluation.identity / persistence / reports），
  逐目录点名单漏项（实测先后 ModuleNotFoundError: evaluation / persistence）→
  改为全树物化、仅排除 training（21.6MB 训练产物，运行零依赖；净 ~16MB）。
- sandbox 时期遗留的缓存重定向（XDG_CACHE_HOME/MPLCONFIGDIR →
  .perception/cache）保留：fontconfig/matplotlib 缓存集中管理，无害。

## 遗留（本 change 不闭环项）

- CONTEXT.md「Perception Acquisition」词条：并发会话（UAR-001）仍持有
  CONTEXT.md 未提交改动——按 RVR-002 F1 clobber 避让先例延后，PER-005 停在
  verified 不 closed，词条落地即闭合（state Scope ⑦）。
