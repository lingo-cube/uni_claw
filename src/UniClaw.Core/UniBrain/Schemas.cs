namespace UniClaw.Core.UniBrain;

/// <summary>
/// Schemas — UniBrain 子接口消费的结构化输出 JSON schema 常量。
/// 经 ModelRequest.Schema 透传；传输 provider 把非 null Schema 视为请求结构化（json_object）输出的信号。
/// </summary>
public static class Schemas
{
    /// <summary>
    /// TextUnderstanding parse_instruction 输出 schema：
    /// { category, confidence (0-1), entities[], summary }。
    /// </summary>
    public const string ParseInstruction = """
        {
          "type": "object",
          "properties": {
            "category": { "type": "string" },
            "confidence": { "type": "number", "minimum": 0, "maximum": 1 },
            "entities": { "type": "array", "items": { "type": "string" } },
            "summary": { "type": "string" }
          },
          "required": ["category", "confidence", "entities", "summary"]
        }
        """;

    /// <summary>
    /// TraversalAdvisor decide_next_action 输出 schema：
    /// { result (enum Success/Unsure/GiveUp), action, target, params (flat object), reasoning, confidence (0-1), safety_verified }。
    /// result/confidence 必填；DecisionResult 3 值锁定（仅引用，不新增 enum）。
    /// </summary>
    public const string DecideNextAction = """
        {
          "type": "object",
          "properties": {
            "result": { "type": "string", "enum": ["Success", "Unsure", "GiveUp"] },
            "action": { "type": "string" },
            "target": { "type": "string" },
            "params": { "type": "object" },
            "reasoning": { "type": "string" },
            "confidence": { "type": "number", "minimum": 0, "maximum": 1 },
            "safety_verified": { "type": "boolean" }
          },
          "required": ["result", "confidence"]
        }
        """;
}
