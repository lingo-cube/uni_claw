# FSV-001 Leader 校正报告（deterministic calibration）

方法：D6 修订——本会话无图像输入能力，采用确定性规则修正（R1 QS 合并串剥离 / R2 icon-image-slider 的 content-desc 文本置 null）+ baseline 交叉旗标（OCR 文本重叠 / YOLO-GT 覆盖）。模型输出仅用于旗标与排除，不用于书写 GT。

- 帧数：39
- R1 剥离：0；R2 置 null：0
- 排除：['5df0424f787c(rejected-stale-dump)', '9f5d4e04c7cf(rejected-stale-dump)', 'd8b2a487cdfa(rejected-stale-dump)']
- 旗标（未排除）：

  - 058f62426c1f:low-yolo-cov:0.19
  - 16068bc9a851:low-yolo-cov:0.00|suspect-partial-dump:gt_cov=0.00
  - 1e572c8f5092:low-yolo-cov:0.25
  - 206556c78e8e:low-yolo-cov:0.07
  - 29f34bdb9dac:low-yolo-cov:0.00
  - 3682353fffbf:low-yolo-cov:0.04
  - 3a28cc28b0b5:low-yolo-cov:0.00
  - 4909995071e6:low-yolo-cov:0.19
  - 4a1f3e1d4421:low-yolo-cov:0.24
  - 4e490bf99988:low-yolo-cov:0.09
  - 518f41240dd0:low-yolo-cov:0.20
  - 5df0424f787c:stale-dump:ocr_hit=0.07
  - 5ef64e4a384a:low-yolo-cov:0.16
  - 731aa1393639:low-yolo-cov:0.14
  - 7b3f715379b0:dialog-expected-low-ocrhit:0.27
  - 7e41f85e06e4:low-yolo-cov:0.08
  - 82565b48ca54:low-yolo-cov:0.05
  - 8576385f9b04:low-yolo-cov:0.05
  - 8db380e98151:low-yolo-cov:0.00
  - 9297d23b7c9f:low-yolo-cov:0.22
  - 9591de5f37de:low-yolo-cov:0.03
  - 9f5d4e04c7cf:stale-dump:ocr_hit=0.00
  - a5d983aa849b:low-yolo-cov:0.03
  - a632ca5c963b:low-yolo-cov:0.19
  - a86c6501da91:low-yolo-cov:0.21
  - acea3e4e4839:low-yolo-cov:0.33
  - afff0f4a8557:low-yolo-cov:0.20
  - be36cfe40647:low-yolo-cov:0.19
  - bf0eca5f9851:low-yolo-cov:0.00
  - c1667d8b209e:low-yolo-cov:0.07
  - c28ad3c4e752:low-yolo-cov:0.04
  - c8b2e65f2451:low-yolo-cov:0.18
  - d2a5c3eca70a:low-yolo-cov:0.09
  - d8b2a487cdfa:stale-dump:ocr_hit=0.10
  - f043962c67eb:low-yolo-cov:0.31
