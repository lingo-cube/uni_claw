# UniAgent 落独立 assembly，而非 Kernel 内 namespace

UniAgent 是 L1 canonical structure 中 Uni Kernel 之外的 peer 组件（baseline
§5/§6），Goal Evaluation 是它在产品代码中的第一个面。决定将其落为独立
assembly `src/UniClaw.Agent`（+ `tests/UniClaw.Agent.Tests`），对
UniClaw.Kernel 保持单向 ProjectReference（Agent → Kernel）：「Kernel 永不
引用 UniAgent 类型」（不变量 41 的结构面）由此从纪律约束升级为 build 层
强制。baseline 明说 L1 逻辑组件不要求对应 assembly，故「同 assembly 内
namespace」方案合法且更省事，被拒原因仅是它无法在编译层阻止反向依赖
生长。

## Considered Options

- Kernel 内 `UniClaw.Kernel.Agent` namespace：零 csproj 成本，但依赖方向
  只靠 review 纪律维持；一旦长出反向引用，边界即难以收回。
