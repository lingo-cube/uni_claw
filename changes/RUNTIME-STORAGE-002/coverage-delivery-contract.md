# Coverage既有输出的可靠交付契约

## 责任与公开缝
World已通过154公开用例/双轴/旧源与资产收尾，按WI-STORAGE002-COVERAGE-CONTINUATION接续。SettingsCoverageLedger的实时事实、Agent/Effect与Run判定不改；既有coverage-report.json、coverage-steps.json接入RequiredReportDelivery，复用同一交付状态，不以run-report完成代替本片。Runner、RequiredReportDelivery、RuntimeToolHost的报告查询/重试及既有HTTP端点为已授权公开验收缝。

## 接受、唯一来源与结果隔离
- 开始执行前持久接受Coverage交付要求并绑定本次源目录；awaiting inputs仍是既有状态。接受失败不启动任务。执行后报告交付/投影失败单独返回，不改已发生的Run Status/Outcome、咨询结算或Effect次数。
- 原Ledger生成的CoverageReport和StepArtifact JSON冻结为独立coverage-source.json，绑定coverage-run目录身份、原RunId与run/evidence/world recording身份。它是交付输入，不成为Ledger或Runtime truth。报告无法由coverage events无损重建，不用事件缺省值补造。
- 原始截图/XML等按用途处理：已配置持久的实际capture源在原evidence子树保留用于追溯；显式要求的附件按路径/用途/必要性登记。固定来源清单及字节/SHA与归档包一并保全；缺失必要来源明确失败，不能重跑任务或补造。默认没有历史GC；交付完成也不自动删除任务保留材料。
- 在唯一报告源已保存、附件未固定的窗口可显式重试来源固定；只能接受已记录摘要或可信明确期望的原来源。已固定后生成只读取私有固定副本，原路径丢失不导致任务重跑；副本缺失/损坏明确报错。

## 复用机制与配置
- RequiredReportDelivery仅增加提前绑定源、用途保护查询和Coverage输入契约；原run-report与旧记录兼容。新字段可选，不另造交付状态或事件系统。
- Host与Dsh共用有界报告进程执行器；保留旧进程配额、取消/关闭和子进程OS租约语义。Coverage的生成器为Host中单一定义的独立Python源，接受时像原生成器一样固定字节/摘要；不依赖重启后工作树文件或调用Agent。
- 产品构造Options指定产品根与相对报告存储路径，统一路径解析贯通Runner写入、Host查询/重试及来源保护；拒绝上跳/越界/链接。产品不读取Harness测试资产JSON，不新建通用配置平台。缺省保持既有.required-reports位置。
- canonical发布并确认后，才将相同原JSON投影到既有Run路径。已有不同内容/链接不能静默覆盖；查询返回已校验发布物，投影失败与正式交付状态分开。

## 验收
1. 真实确定性Runner先接受要求后执行；原报告/步骤值及字节和Runtime结果不变。交付失败也返回原结果，公开查询可见pending/失败原因。
2. 接受后、唯一输入固定后、生成中、发布但未确认等真实子进程Kill窗口，重启可查同一请求并仅报告重试；源/生成器/产物摘要匹配，Agent/Effect/任务重执行为0。
3. 独立PNG/XML及步骤/报告来源的必要、配置留存、可选用途逐项区分；必要缺失/损坏显式失败，未交付或任务保留来源受保护且可查。封存旧源与其他Owner不变。
4. 原run-report、关闭/取消/并发/租约/端点相关回归保持；本片公开RED→GREEN、两轴独立审查、四元组与实际资产收尾通过后更新WorkItem done。父范围关闭和统一精确提交推送仍归原Leader。
