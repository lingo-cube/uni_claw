# 实施验收索引（核心；不含后续UI）

固定入口 f541a897；正式 Ledger/World/Control/Assurance/Effect/Driver 使用产品实现。外部布局、clock、HTTP provider、delivery adapter 使用受控来源；录制XML仅证明历史布局。未执行真机或真实模型，不统计模型优劣。最终总状态以 verify-final.md 和本目录最终日志为准，以下为可复现用例定位。

## K01–K12 四元组

| 族 | method | expected | actual/范围 | evidence |
|---|---|---|---|---|
| K01 | WorldSliceScopeTests；canonical oracle；Core | 不同scope/region不同引用，集合顺序归一、旧S不变、currency不放松 | 新范围回归及原路径已通过；全量结果另验 | slice-scope/red.txt；本目录kernel-owner-scope-final.txt；World/WorldSliceScopeTests.cs |
| K02 | WorldOperationReplayTests 的实际Driver导航cycle/ordinary recapture；历史/迟到回归 | A→B→A→C→A复用A且观察/已证访问分开；普通capture不借旧nav；设备不足Unknown | cycle及普通重采已通过；初次访问/同C自重入来源无证明时明确未知，不把capture计为visit | host-composition-cycle-first.txt（6项原/新增Replay通过）；WorldSemanticReuseTests设备反例 |
| K03 | WorldAccumulatedContentTests + Host实际scroll/receipt/verification + Simulation WorldReconstruction | 只沿verified/aligned同页链累计，固定栏不移、旧state非current、不补全页 | Owner正反例及实际投递链通过；嵌套/重排/不充分对应保留未知 | owner-integration-region-retry.txt；actual-scroll-layout-retry.txt；WorldOperationReplayTests |
| K04 | WorldPageCorrectionTests、WorldPageHistoryTests、WorldCorrectionConsumptionTests | 指定原capture/旧解释，新依据充分才原子纠正；独立C2/旧W/S不回写 | accepted/不足/重复/再纠正、历史当前保护及Host归属消费通过 | host-semantic-source-retry.txt；World/WorldPageCorrectionTests.cs；最终Kernel日志 |
| K05 | WorldSemanticReuseTests、PageSemanticReadSupplyTests、WorldSemanticHostCompositionTests | 历史先可读，本轮native对应充分后读当前语义，慢provider增量0 | World与Host读取正例通过；实际Host显式profile装配与无profileUnknown有受控正反例；无真实生产profile/live声明 | semantic-reuse-persistent-fix.txt；host-semantic-source-retry.txt；host-certified-retry.txt |
| K06 | profile/包/设备/原语义不符、wrongcapture/oldW/stale request；原grounding/freshness回归 | 稳定语义不携带旧locator/state/存在；错scope保留Unknown | 不匹配退出当前供给；当前Slice排除历史scoped语义；旧目标不复活 | WorldSemanticReuseTests；PageSemanticReadSupplyTests；Kernel全量 |
| K07 | WorldKnowledgeGapTests：source→Ledger→World→读摘要 | 四类各有产生/保持/充分解决；关系有buyer才提出，Partial不等全页 | 四类最小生产/消费/消除与Context合法正例已通过 | gaps-retry.txt；World/WorldKnowledgeGapTests.cs |
| K08 | WorldRecoveryReviewTests + WorldKnowledgeConsultationTests + 最终两轮正文 | 真实执行去重、结果更新不增尝试；null不是0；相同提醒不重复、新结果再提醒，不续额/期限 | Control Owner/Driver正例通过；截止时间另用可控Owner时钟证明；无跨capture连续性来源则新问题 | effects-attempts-source-retry.txt；legacy-owner-integration.txt；channel-final-prompts.json |
| K09 | WorldCorrectionImpactTests + 独立native semantic验证反例 | 实际消费与仅记录分开；独立C2保留；共同旧依据撤回需复核；缺边不能称零影响 | 旧计划/回执/验证消费有明确范围；legacy缺page血缘标DependenciesUnknown；不声称完整模型思考依赖 | effect-terminal-scope-retry.txt；legacy-owner-integration.txt；WorldSemanticReuseTests |
| K10 | 实际Driver receipt→等待StepVerify→纠正→事后观察；完成terminal后纠正；原guard/权限/typed验证回归 | 在途不重发/逆点，撤回依据不继续证明；terminal旧Outcome/receipt不变；恢复不凭旧信息 | 在途/终局/dispatch前失效已通过；最小影响信息交原Owner，无通用补偿或自动恢复。无法证明当前效果则明确未决，不报完整恢复 | WorldCorrectionImpactTests；legacy-owner-integration.txt；原PageObservationExecution/typed校验 |
| K11 | audit-decision-input.py；6个独立材料资格检查 | 缺最终封套/messages/tools/截断/版本/摘要不入分母；同源副本不升级 | 当前2份实际最终turn正文均diagnostic-only，分母0；受控完整材料正例可纳资格，内容/配置变化不能共享键 | input-eligibility-tests.txt；input-eligibility.json；tools/tests/test_audit_decision_input.py |
| K12 | 正式World→SliceContentPresentation；实际Driver→严格codec→DSH HTTP→真实plugin consultationPrompt | 两轮摘要实际正文可读，GT不改变actual；动态信息非首轮目录 | 已验证两轮内容，provider封套/真实模型行为未证；录制同例子经正式链通过（channel-recorded-import-retry.txt，最终full包含） | channel-consults.json；channel-final-prompts.json；channel-world-example.json；prompt-schema-final.txt |

## 20决定、38主题与早期要求映射

Q1/5→K07；Q2→ADR-0054/完整Runtime恢复另线；Q3/11→K11；Q4/6/7/8/9/10→K08；Q12/13→原guard/完成屏障保留、整体Goal延期；Q14/16→K03/K12（交互UI另Change）；Q15→K04；Q17→K01/K02/K04；Q18→本表与最终正式链；Q19→K05/K06；Q20→K09/K10。

MV01–03/05/17/18→K02；MV04/13→K03；MV06/14→K08（新capture缺口不销旧账）；MV07–10/12/15/16→K04；MV11→K09。RU01–03/07/08→K05；RU04/05→K06；RU06→K09。CI01/02/09/10/12→K09；CI03/04/08→K10的dispatch前/在途/terminal用例；CI05/06/07/11→K10原Owner屏障、当前授权/独立验证与明确未决边界，不宣称自动逆操作或全部可恢复。每族可合并用例但不合并语义；现场来源/完整补偿/通用生命周期按spec范围未购入。

早期四类完整链→K07；真实尝试而非requests/revisions→K08；每次相关正文→K08/K12；安全边界复核/再次提醒→K08/K10；材料资格→K11；scope-id回归→K01；正式模型输入内容→K12（当前证明构造/传送，最终provider封套/行为另标未证）。
