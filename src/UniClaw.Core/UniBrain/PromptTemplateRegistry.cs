using System.Collections.Immutable;

namespace UniClaw.Core.UniBrain;

/// <summary>
/// Prompt 模板单点真源 — 3 个模板集中定义，消除 5 个测试文件中的内联副本。
/// 设计决策: static properties（非 DI），测试和生产通过同一引用获取模板文本。
/// </summary>
public static class PromptTemplateRegistry
{
    public static PromptTemplate ParseInstruction { get; } = new(
        ModelCapabilities.ParseInstruction,
        "你是助手",
        "解析：{text} 上下文：{context}",
        ImmutableArray.Create("text", "context"));

    public static PromptTemplate DecideNextAction { get; } = new(
        ModelCapabilities.DecideNextAction,
        SystemPrompt: "You are a mobile UI traversal decision advisor. Given a goal, the current page state (JSON), the current node id, and traversal depth, decide the single next action that best advances the goal. Respond ONLY with a JSON object: result (one of Success/Unsure/GiveUp), action (verb such as tap/scroll/input/back/wait), target (element id or null), params (flat object of primitive values, optional), reasoning (one sentence), confidence (0-1), safety_verified (boolean).",
        UserPrompt: "Goal: {goal}\n\nCurrent page analysis (JSON):\n{page_analysis}\n\nCurrent node id: {current_node_id}\nTraversal depth: {depth}\n\nDecide the next action.",
        Variables: ImmutableArray.Create("goal", "page_analysis", "current_node_id", "depth"));

    public static PromptTemplate AnalyzeVisual { get; } = new(
        ModelCapabilities.AnalyzeVisual,
        SystemPrompt: "You are analyzing a mobile app screen for UI traversal. Analyze this screenshot and provide: " +
            "(1) menu structure (level 1 and level 2 menus with positions and active state), " +
            "(2) current path (which menus are active/highlighted), " +
            "(3) all clickable items in the content area each classified by `type`, " +
            "(4) any popups/dialogs/special UI elements. " +
            "Item `type` vocabulary (exactly one per item): `menu_item` (list items navigating to sub-pages), " +
            "`tab` (top-level view switch), `back_button` (back/return), `switch` (on/off toggle with sliding animation), " +
            "`toggle` (state-toggle buttons e.g. favorite), `button` (generic action), " +
            "`link` (navigation links/hypertext), `icon` (icon-only buttons), `text` (non-interactive text), " +
            "`readonly` (display-only). " +
            "Return ONLY JSON with this exact structure (coordinates normalized 0-1): " +
            "{ \"level1_dir\": \"left|right|top|bottom\", \"level1_menus\": [{\"name\",\"coordinate\":{\"x\",\"y\"},\"active\"}], " +
            "\"level2_dir\": \"left|right|top|bottom\", \"level2_menus\": [/* same shape */], " +
            "\"current_path\": [\"...\"], \"items\": [{\"name\",\"type\",\"coordinate\":{\"x\",\"y\"},\"parent\"}], " +
            "\"is_popup\": false, \"popup_info\": {\"title\",\"content\",\"close_button\":{\"x\",\"y\"}} or null, " +
            "\"close_button\": {\"x\",\"y\"} or null, \"back_button\": {\"x\",\"y\"} or null, " +
            "\"has_scroll\": false, \"is_end_of_list\": false }. " +
            "Important: include only currently visible interactive elements; omit off-screen, clipped, or non-interactive text/readonly elements. " +
            "Use each element's visible center point and omit the element if that center lies outside the screenshot. " +
            "Every coordinate x/y MUST be a JSON number in the closed interval [0,1], never a pixel value or a value outside that interval. " +
            "Mark parent-child via `parent`; `current_path` indicates active menus; name icons like \"[icon] description\"; " +
            "level1_dir/level2_dir MUST be a single value from left/right/top/bottom (NEVER pipe-separated; choose ONE).",
        UserPrompt: "Analyze the current app screenshot and return the PageAnalysis JSON above.",
        Variables: ImmutableArray<string>.Empty);

    public static PromptTemplate AnalyzeVisualLite { get; } = new(
        ModelCapabilities.AnalyzeVisualLite,
        SystemPrompt: "You are verifying whether a mobile app screen changed after a traversal action. " +
            "Compare against the described pre-action screen and answer ONLY with a compact JSON object: " +
            "{ \"changed\": true, \"page_identity\": \"<menu path or null>\", \"item_count\": 12 }. " +
            "`changed` is whether the screen differs from the pre-action state; `page_identity` is the " +
            "current page's menu path (or null when unclear); `item_count` is the number of visible " +
            "interactive elements (or null when unclear). No other text, no code fences.",
        UserPrompt: "Pre-action screen: {before}\n\nCheck the current screenshot for change.",
        Variables: ImmutableArray.Create("before"),
        MaxTokens: 1024);

    /// <summary>
    /// ExtractIntent — 从自然语言场景描述中提取结构化 IntentSlots。
    /// 输出必须是合法 JSON，字段词表对齐 PlanCompiler.ValidateSlots 的词表锁。
    /// scope ∈ {full, target_only}；element_handling ∈ TEMPLATE_SETS keys
    /// （full_interaction/menu_only/safe_mode/read_only），null 默认为 full_interaction；
    /// completion ∈ {max_steps, timeout}，null 表示由 scope 派生默认 Type。
    /// </summary>
    public static PromptTemplate ExtractIntent { get; } = new(
        ModelCapabilities.ExtractIntent,
        SystemPrompt: "You are a mobile UI traversal intent analyzer. " +
            "Given a scenario description for automating an Android app, extract the user's traversal intent " +
            "into structured slots. Reason about the traversal shape (exhaustive exploration vs. locate-and-stop), " +
            "interaction strategy (which element types to engage), navigation style, and whether state restoration " +
            "is needed.\n\n" +
            "Respond ONLY with a single JSON object — no markdown fences, no extra text. " +
            "Use this exact schema:\n" +
            "{\n" +
            "  \"scope\": \"full\" | \"target_only\",\n" +
            "  \"element_handling\": \"full_interaction\" | \"menu_only\" | \"safe_mode\" | \"read_only\" | null,\n" +
            "  \"navigation\": \"bounded_settings\" | \"free_navigation\" | \"deep_link\" | \"single_page\" | null,\n" +
            "  \"restore\": true | false,\n" +
            "  \"completion\": \"max_steps\" | \"timeout\" | null\n" +
            "}\n\n" +
            "Field semantics:\n" +
            "- scope: \"full\" means explore/exhaust everything reachable; \"target_only\" means find a specific item and stop.\n" +
            "- element_handling: \"full_interaction\" (click everything), \"menu_only\" (only navigation menus), " +
            "\"safe_mode\" (menus + safe toggles/switches), \"read_only\" (no interaction, just observe). " +
            "null means the engine default (full_interaction).\n" +
            "- navigation: \"bounded_settings\" (within a Settings-like app with back/up navigation), " +
            "\"free_navigation\" (any navigation pattern), \"deep_link\" (direct deep link to target), " +
            "\"single_page\" (stay on one page). null means no constraint.\n" +
            "- restore: true if the traversal should restore the app to its initial state after completion.\n" +
            "- completion: \"max_steps\" to bound by step count, \"timeout\" to bound by wall-clock time, " +
            "null to let the scope determine the default completion policy.",
        UserPrompt: "Scenario: {description}\n" +
            "Target app: {target_app}\n" +
            "Target item: {target}\n" +
            "Max depth: {depth}\n" +
            "Entry page: {entry}\n\n" +
            "Extract the traversal intent as JSON.",
        Variables: ImmutableArray.Create("description", "target_app", "target", "depth", "entry"),
        MaxTokens: 512);
}
