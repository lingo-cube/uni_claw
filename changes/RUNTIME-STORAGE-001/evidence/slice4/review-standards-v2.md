范围：v1→v2候选20路径；SHA全匹配，base=a05176f3c66e0a3206f49dde463f51df1335975c。

M1：硬违规0。`TraversalControl.History.cs:101-116`仅将无外调/交Work的configure→start两条原行共同确认；不支持批次逐条发布。`FileCommittedHistory.cs:91-123`限2条、整批16MiB、窗口0且一次frame/head提交；满足ADR-0055:7/9/11的批次及计数，不涉跨Owner；Plan逐阶段确认。

启发式（0）：神秘名0；重复码0；特征依恋0；数据泥团0；原始类型痴迷0；重复分支0；霰弹式修改0；发散修改0；推测性通用0；消息链0；中间人0；拒绝继承0。可选接口有实际双记录buyer，非通用平台。

并发：DSH popup仍按独立快照排除，未扩大结论。
