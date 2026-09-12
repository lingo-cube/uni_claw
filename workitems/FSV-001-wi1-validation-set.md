# FSV-001 WI-1 — 验证集采集（transient WorkItem 载荷）

- status: dispatched → subagent 67eab802-751e-447c-b904-f17b3203fe7c
- objective: platforms/perception/evaluation/validation/fastscreen-v1/ 采集
  40±10 帧（8 分层覆盖 + 暗色 ≥3 + overlay ≥2）+ 8±2 交互序列；每帧
  PNG + uiautomator XML + meta；内容寻址命名；collect.py 可重放。
- acceptance: 分层/序列数达标；三件套齐全；不改既有文件；异常如实记录。
- result: 完成（2026-09-12）。39 帧（list 7/sidebar 3/settings 10/dialog 3/
  scrollable 3/dense 4/text-heavy 5/icon-heavy 4；暗色 4；overlay 4）+ 8 序列
  （scroll×2/click×2/toggle/transition/dialog/custom）。collect.py validate OK
  + replay 实测可重放。异常 6 项如实记录于 README（动画 idle 重试、dev opts
  使能、等效 dialog 替代、导航 force-stop 复位、QS keyevent 收起、4 帧重采）。
  仅新增目录，零既有文件修改。
