# RUNTIME-PLAN-005实施计划
1. 独立检查Kernel遍历屏障、动态重读、返回栈、安全、异步handler与完成语义，同时核对Host真实来源/目录/测试。
2. 在既有ProjectTraversal/RunDriverInputs公共缝用可观察反例确认缺陷，逐项RED→GREEN修复；不修改无关World工作。
3. 通过注册脚本准备设备与专用3081，核对真实Profile/Prompt/Skill及当前模型参数，先固定有界范围与独立门槛后调用模型。
4. 实际UniAgent生成Plan交正式codec与Kernel执行，保留完整失败/修订、设备事实/Effect/返回/部分覆盖证据。
5. 独立Spec/Standards评审；必要修复后运行相关协议与仿真，核对四元组及状态。仅实际满足范围才关闭。

状态：1-5已完成。验收四元组见`evidence/runtime-plan-005/verification.json`；最终批次epoch007为3/3，Scope有界，全部失败保留。
