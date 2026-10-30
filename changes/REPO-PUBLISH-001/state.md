# REPO-PUBLISH-001 — Settings遍历前的仓库整理与发布
lifecycle_state: resolving · disposition: none · depth: standard · base: 600e74df0

## Intent / Scope
用户授权先整理、提交、推送当前仓库，再进行Settings一级至三级安全遍历。本Change核对并发、资产归属、敏感内容和发布限制，保存必要证据的可读来源；不改变产品权限/协议，不自动选择有配额成本的远端存储，不删除他人活动材料。

## Acceptance
1. 现存源码与记录形成可审查提交并推送origin/uni-harness，必要证据可定位、可读取、可核对摘要；不以ignore遗漏未保存原件制造干净。
2. 95项已有tracked evidence删除与原Owner清单及HEAD字节大小/SHA256逐项匹配；合法退役有记录，非迁移。
3. 当前回归与协议检查如实记录；失败不写PASS，不用历史通过替代当前运行。
4. 资产处置留实际回执；完成前不启动后续设备遍历。

## Plan / Decisions
先核对并发和资产→提交独立已有退役记录→确定存量大材料发布载体并逐任务校验读取→集成其余源码/记录、验证并推送→Settings单独按逐级可判定范围续行。已询问压缩归档或LFS选择；答复前不迁移/删除存量原件。

## Verification
level: CONTRACT + DETERMINISTIC
method: git实况、Owner清单和HEAD字节核对；候选敏感模式扫描；全量solution与孤立失败用例；协议生成check。
expected: 发布引用可读，当前源码回归无未处置失败。
actual: 入口55843 untracked/6847905416 bytes、87项>10MiB/9项>100MiB；95项tracked evidence删除全部有清单且原HEAD大小/SHA256一致。协议check通过。当前solution 3342 passed/1 failed/1 gated skip；异步handler测试Expected1/Actual16，孤立同用例1/1通过、Simulation工程329/329通过；尚未证实根因，保留原失败。完整发布未完成。
evidence: inventory.json；evidence/verification.json及同目录压缩原始日志、current-source-sha256.json.gz、secret-scan.json。完整入口状态仍在仓库外本任务runs/audit-20261030保留，本机根/Users/fran/Documents/Code/uni-claw-task-work/REPO-PUBLISH-001仅作本地定位。

## Status log
- 2026-10-30→31 UNDERSTAND/RESOLVE：原树和原暂存保持，未见同仓其他active聊天/构建测试；两名用户指定Luna仅做只读核查。HEAD与origin/uni-harness均600e74df0。TRAJECTORY-001当前closed，未将旧日志的blocked当当前状态，重启续跑保持独立设计。
- 2026-10-31 RESOLVE：旧证据退役已逐项核实。大文件格式已向用户提出压缩归档（推荐）或LFS选择；原件均保留。本轮测试产物独占本任务仓库外目录，无批量清理。

- 2026-10-31 REVIEW/VERIFY（独立退役切片）：95项删除与原执行清单/HEAD字节全匹配，原已退役记录及轻量证据117文件提交ef8c7a45b并push origin/uni-harness成功；产品代码未纳入。指定敏感模式扫描61913文件零匹配，非任意秘密形式的保证。异常字面目录两文件与工具取消测试fixture内容完全一致，按复核SHA实际删除54字节，见finalization.json。完整发布仍未完成，不宣称仓库已干净。
