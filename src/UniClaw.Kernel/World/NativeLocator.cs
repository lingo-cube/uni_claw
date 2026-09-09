namespace UniClaw.Kernel.World;

/// <summary>
/// NativeLocator — 平台原生定位锚（DSE-003 / P14 DeliveryTarget 的第二种
/// executable locator 素材；ego-browser buyer 兑现 DSE-002 defer）。
/// Kind 开放词汇 &lt;platform&gt;.&lt;key-kind&gt;（browser.backend-node-id /
/// android.resource-id / …，不锁枚举——协议通则 4）；Value 为该平台键值。
/// 语义纪律（P-UW-24）：execution anchor，evidence-derived——**不是
/// identity**（identity 在 WorldModel occurrence/LogicalItem 域）；driver
/// 用它定位执行对象是 adapter 行为，不得缓存为持久 handle。
/// 与 SpatialLocator 并存时 = 同一已授权 target 的不同 delivery
/// material；driver 固定消费自身支持集，永不挑选 / fallback（规则 B/C）。
/// </summary>
public sealed record NativeLocator(string Kind, string Value)
{
    public static NativeLocator BrowserNode(string value) => new("browser.backend-node-id", value);
}
