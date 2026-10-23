# 核心 Verify（WORLD-UNCERTAINTY-001）

level: SCENARIO（含CONTRACT/DETERMINISTIC；不含ENVIRONMENT）
method: `python3 tools/verify-change WORLD-UNCERTAINTY-001 --scope full`；独立Spec/Standards Review与处置复核；K01–K12验收索引；实际DSH HTTP/最终turn正文与材料资格。
expected: 所有受影响组件/运行时/仿真、生成协议、认证、覆盖及边界检查通过；原immutable记录/独立来源保留；未知不当成功；并行改动保持。
actual: verify-full-final.txt FINAL_STATUS=PASS，八个项目2181项（314+36+214+14+9+436+56+1102）通过；架构专项43+26为重复抽检不累加；41份scenario认证0违规；源代码composition明确按本Change重认证，独立oracle/录制输入不改。原失败attempt全部保留。六个原有foreign tracked diff逐项相等，初始index为空。Node插件全套52项通过，材料资格6项通过；最终正文/材料资格单独见日志，不与真实provider行为混称。
evidence: verify-full-final.txt；coverage-ledger.md；review-spec.md/review-standards.md/review-followup.md；composition-recertification*.json；scenario-certification-check-final.txt；protected-diff-audit.json；channel-world-example.json/channel-consults.json/channel-final-prompts.json；input-eligibility.json；plugin-full-final.txt。

## 修复前后

Slice以前只用W+root定Id，不同scope会碰撞；现在scope/region规范结构编码。页面以前只有current景观，无法保持纠正旧观察与当前新观察的区别；现在按源读取和原子纠正，语义沿用需要原独立验证+本轮native对应。旧用途/locators不被新W自动复活。

Control按实际执行和结果版本记账，原期限/配额不续；安全边界同结果只提醒一次，新真实结果可再复核。Assurance追加实际消费影响审查，保留receipt/verification/terminal，缺旧page lineage标DependenciesUnknown。最新图正确不等于旧效果已恢复。

全量发现的新可选null字段编码改变旧EvidenceId已修：仅新增字段null不编码，旧capture和冻结semantic basis恢复兼容。OperationReplay的明确受控设备来源及Profile revision pin同步，原判断/独立Oracle未放松。

## 证据边界

外部模型、clock、native profile、投递adapter使用受控来源；历史录制XML只证明其布局。真实生产profile、真机、真实模型及最终完整provider封套均未证。当前材料两份只进diagnostic，same-input分母0；完备资格检查不认证源真实性。当前追踪是明确提供的页面/目标/决策/核验来源，非完整模型推理或全部外部副作用图；没有自动补偿/通用生命周期/完整Goal接通。UI是独立后继Change，不在核心验收内。

日志格式：prompt-first.txt两条纯空白行的尾随空格为差异卫生规范化；失败内容/行数保留，未改冻结输入或Oracle。
