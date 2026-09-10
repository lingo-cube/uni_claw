# DBG-001 — AI-Coder World Model Diagnostic Route Evidence

## 1. Base / scope

- Base：`672cc7a6e7291729716893f2fa8d95fda252524c`（WMP-002 CLOSED 提交）。
  主树 dirty 仅四项已知并发/无关（CONTEXT.md、changes/PER-004/、两 untracked
  文档）；DBG-001 目标面（.agents/skills/uniclaw-debug-evidence/、changes/、
  evidence/）零 dirty。`skills-lock.json` 与上游 `diagnosing-bugs` byte 级
  未动（双轴 review 经 `git diff` 复核）。
- 交付面（本地扩展 `source: LOCAL_UNICLAW`，可编辑）：SKILL.md 追加 §6 短
  路由入口（Composition Contract 未改）；references/world-model-consistency.md
  （稳定知识）；scripts/world-model-consistency.sh（唯一 agent-runnable 命令，
  executable）。

## 2. 命令行为（A2）

- 唯一命令：
  `bash .agents/skills/uniclaw-debug-evidence/scripts/world-model-consistency.sh`
- GREEN：exit 0，`通过数: 44`（oracle 10 + materialization probe 5 +
  performance 10 + benchmark probe 17 + 自检…合计 44）；从仓库根、/tmp、
  仓库子目录三种 cwd 运行均 exit 0（脚本自定位仓库根）。
- 仓库外副本：exit 2 + stderr 明确诊断（root walk-up 失败路径）。
- clean isolated checkout（clone @672cc7a + 三个 DBG-001 文件）：exit 0、
  44/44 可重复。
- detailed verbosity（评审修复后）：`WMP-MAT` 7 行、`WMP-STAGE` 10 行可见
  ——性能路由可执行；`WMP-DIVERGENCE` 为断言消息，任何 verbosity 都透出。

## 3. 定向破坏 RED→GREEN（A3）

- 主破坏（scratch）：删掉 oracle expected 扫描的 container 过滤维度 →
  命令 exit 1，输出首行 FDP：
  `WMP-DIVERGENCE schema=wmp-canonical-oracle/1 op=ResolveCurrent
  owner="WorldModel consumer-view derivation via WorldRevisionIndex" rev="rev-1"
  key="role=Button|desc=*|container=ctr-alpha" expectedCount=3 actualCount=2
  firstDivergentPosition=2 expected="occ-7b6cfd0a9902-2|Button|submit|ctr-beta|rev-1"
  actual="<none>"`；恢复后 exit 0。
- Spec 评审 subagent 独立执行的第二破坏（GoldenRealAssets golden 值）→
  exit 1 + FDP（`op=GoldenCanonicalHash … firstDivergentPosition=0`）→ 恢复
  exit 0。两组破坏互不相同、互不知情，均被同一命令捕获。

## 4. AI Coding dogfood（A4；仅 DSH Host，未验证 Codex）

- 方法：fresh disposable subagent（不继承本会话任何结论），仅给最小问题：
  「怀疑 WMP 索引导致 ResolveCurrent 与 canonical scan 不一致，请只诊断
  （不要修改任何文件）。仓库：…uni_claw（uni-harness 分支）。请给出你的
  诊断证据包。」
- 实测路由链：AGENTS/skills 目录 → `diagnosing-bugs`（Phase 1 tight loop）
  → `uniclaw-debug-evidence` → references/world-model-consistency.md →
  找到并运行唯一命令（exit 0, 44/44）。
- 返回的 evidence packet 覆盖全部要求字段：E-level（**E2**，stateful module，
  未达 E3 的理由：无帧/trace 证据指向跨组件链）、failure class（未触发，
  A-F 均不成立）、lifecycle stage（consumer-view derivation）、last correct
  state（N/A——无分歧点）、First Divergence（无；给出 FDP 行格式与 owner）、
  Owner（WorldRevisionIndex，带 WorldModel.cs:958-969 代码引用）、remaining
  uncertainty（按可疑度排序的 5 个 fixture 外表面，含 count-stable 快路径
  分析）、escalation（none）。
- 质量事实：agent 正确复述 GREEN 语义边界（只排除已覆盖 fixture）；核对了
  oracle 与 pre-WMP baseline 实现的同构性（真 baseline 非自证）；识别出
  count-stable container 索引分支是「不变式而非 bug」并列 watch；对补 fixture
  的建议**正确地未执行**（识别出属新 Change 范畴，遵守 Out of Scope）；
  `git status` 与诊断前一致，零文件修改、零 [DEBUG-*] 残留。
- Host 范围声明：以上仅 DSH Host 实测；Codex Host 未验证，不作宣称。

## 5. 边界证明（A5）

`grep -rniE "world-model-consistency|\.agents[/\\]|workitem|dsh-skill|codex-adapter|session.?id" src/ --include="*.cs"`
→ 无命中：Product Runtime src 不依赖 .agents、诊断脚本、WorkItem 或 Host
adapter。

## 6. REVIEW / VERIFY

- Standards 轴（subagent）：零文档标准违规（无第二 lifecycle/task system、
  共享层无 Host 语义、Product/Harness 分离、Composition Contract 未动）。
  judgement calls 已处置：`set -e` 缺省 → 补 rationale 注释（exec 透传退出码）；
  filter 与 state.md D1 的重复知识 → 脚本内加同改提示注释。保留 non-blocking：
  15 行 launcher 的 marker 硬编码（当前规模合理）、三跳指针（与既有
  canonical-cases.md 形状一致）。
- Spec 轴（subagent，实测执行）：无缺失/无 scope creep；两个「已实现但有误」
  已修复——(1) probe 证据行（WMP-MAT/WMP-STAGE）在默认 verbosity 不可见，
  性能路由不可执行 → 脚本改 detailed verbosity（实测 7+10 行可见）；
  (2) reference「不访问任何远程数据源」为过度声明（NuGet 漏洞索引 NU1900
  会尝试网络查询但离线可容忍）→ 措辞如实修正。
- VERIFY：修复后命令 GREEN（exit 0, 44/44，probe 行可见）；clean isolated
  checkout 全量 dotnet test 见 §7。

## 7. 最终验证（CLOSED）

- 精确提交树（HEAD + DBG-001 五文件）clean isolated checkout：
  `dotnet test UniClaw.Kernel.slnx` 全量 Kernel 276/276 + Agent 17/17 GREEN
  （见 state.md Verification）。
