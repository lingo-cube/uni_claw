# RUNTIME-PLAN-003 实施顺序

1. 协议一致性垂直切片：正式生成接口增加null正确表达的回归，修生成器并再生TypeScript；文档示例省略非法null，校验正式输入。Worker负责明确文件，Leader验收。
2. 异步技术事实：核对typed语言能力/Host适配、driver阶段/等待/取消、Simulation与真实Host持续执行入口，冻结工程契约和可判定场景，保留原Owner。
3. 异步执行垂直切片：先通过真实Kernel/Simulation缝证明延迟调用的Running/可查询/有序接回，再逐项证明超时、取消、迟到、修订与预算；同步适配兼容同一计划。
4. 协议/装配闭包：状态模型、codec/Schema/TypeScript、Prompt/镜像及hash同源更新；Host买方真实接线。
5. 独立Review处理后VERIFY：相关套件、场景再认证、正式协议检查；四元组记录；003完成才进入004。

测试缝沿用Owner已要求的正式producer/codec/consumer及真实Kernel链SimulationHost，运行状态读取Run canonical视图。测试独立预期，不测试私有实现结构。
