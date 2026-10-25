# 诊断出口与双域失败接线交付

## 做了什么
接通验收测试和运行诊断的同一非权威失败信号，让诊断包能被后续演进循环引用。DBG-003与EVO-002范围已完成；本次为本地交付，没有提交推送。

## 原来的问题
诊断仅返回文字，后续无法核对来源或内容是否变化；测试出口没有统一分诊，旧TRX也可能被误当当次失败。原引用契约还要求LOCK后改case，破坏同一manifest的红绿证明。

## 这次修改
- `tools/verify-change`→`tools/evolution-signal.py`：每项目使用当次唯一TRX；直接核对composition pins，即使测试全绿也产独立资产信号。原验收范围结论与退出码保留。缺失/畸形材料记unknown，不丢先前已解析失败，不猜A–F或Owner。
- `RuntimeToolHost.InvokeProcedureAsync`→`RuntimeDiagnosisPacket`→清单声明的退出entry：模型只返回诊断字段；Host钉住实际模型、报告哈希和非权威标记，导出独立`evidence/diagnosis/.../diagnosis.packet.json`，返回path+sha256。Python出口再读facts产生live信号并引用该包。输入run和Runtime状态不写；真实落工件因此声明local-write。
- 预期行为红不产候选；重复信号去重，已有带failureKey的开放case或显式已知问题只引用。候选是证据记录，不自动创建Change或修复。
- Uniflow与模板接诊断path+sha256；LOCK后只在结果包引用新诊断，冻结case和历史cycle-01不改。

## 验证结果
- 工具全套36/36、Host.Dsh64/64、工作台103/103通过；快速验收PASS，41份场景认证0违规。范围没有扩大为八工程full。
- 真实归档red.trx首次→candidate、再次→duplicate；既有TerminalNotProven事实→failed/pending-reproduction。受控单pin过期明确指向manifest与Classifier路径，旧资料不改。
- 实际公开工具调用的导出包通过机械schema校验，producer是适配器实际配置，包/报告哈希匹配；非法包、伪权威、报告变化均拒绝。
- 独立Standards/Spec复审均通过；保留初次发现、编译失败及全部RED→GREEN日志。原6份foreign dirty哈希一致。

完整命令、预期/实际及证据见[验收四元组](evidence/verification.json)。Host模型传输使用受控替身；本次没有真实设备、模型或provider验证。

## 新发现
全绿漏扫资产、畸形manifest丢信号及诊断引用schema缺执法已在本次修正。旧case无failureKey时需显式已知问题映射，不能改写已锁manifest。运行产物尚不能自动导出为可重放case，live信号明确保留待复现状态。

## 下一步
沿既有录制提取缝实现runDir→不可变case导出，锁住原始输入、独立预期、来源与可执行复现命令，再验证能否从live信号进入LOCK。弹窗分类候选与SIM-017未决窗口语义保持原状态，不在本次采纳。
