#!/usr/bin/env python3
"""Apply [JsonPropertyName] annotations to TraversalPlan.cs and other files for C-6 round-trip."""
import os

BASE = 'd:/space-x/uni_claw/src/UniClaw.Core'

def annotate_file(filepath, replacements):
    """Apply a list of (old, new) string replacements to a file."""
    content = open(filepath, 'r', encoding='utf-8').read()
    for old, new in replacements:
        if old not in content:
            print(f"  WARNING: pattern not found in {filepath}: {old[:60]}...")
            continue
        content = content.replace(old, new)
    with open(filepath, 'w', encoding='utf-8') as f:
        f.write(content)
    print(f"  Written: {filepath}")

def add_using(filepath, using_line):
    """Add a using directive if not already present."""
    content = open(filepath, 'r', encoding='utf-8').read()
    if using_line not in content:
        # Find first 'using' line and insert after it
        lines = content.split('\n')
        for i, line in enumerate(lines):
            if line.startswith('using '):
                lines.insert(i + 1, using_line)
                break
        with open(filepath, 'w', encoding='utf-8') as f:
            f.write('\n'.join(lines))
        print(f"  Added using to: {filepath}")

# === TraversalPlan.cs ===
print("Annotating TraversalPlan.cs...")
tp_path = os.path.join(BASE, 'Graph/Models/TraversalPlan.cs')
add_using(tp_path, 'using System.Text.Json.Serialization;')

annotate_file(tp_path, [
    # EntryPolicy properties
    ('    public EntryStrategy Strategy { get; init; }', '    [JsonPropertyName("strategy")]\n    public EntryStrategy Strategy { get; init; }'),
    ('    public string? Fallback { get; init; }', '    [JsonPropertyName("fallback")]\n    public string? Fallback { get; init; }'),
    ('    public Dictionary<string, object>? WaitCondition { get; init; }', '    [JsonPropertyName("waitCondition")]\n    public Dictionary<string, object>? WaitCondition { get; init; }'),
    ('    public double TimeoutSeconds { get; init; }', '    [JsonPropertyName("timeoutSeconds")]\n    public double TimeoutSeconds { get; init; }'),
    # EntryPolicy constructor
    ('        EntryStrategy Strategy,\n        string? Fallback = null,\n        Dictionary<string, object>? WaitCondition = null,\n        double TimeoutSeconds = 10.0)', '        [JsonPropertyName("strategy")] EntryStrategy Strategy,\n        [JsonPropertyName("fallback")] string? Fallback = null,\n        [JsonPropertyName("waitCondition")] Dictionary<string, object>? WaitCondition = null,\n        [JsonPropertyName("timeoutSeconds")] double TimeoutSeconds = 10.0)'),
    # CompletionPolicy properties
    ('    public CompletionPolicyType Type { get; init; }', '    [JsonPropertyName("type")]\n    public CompletionPolicyType Type { get; init; }'),
    ('    public string? TargetName { get; init; }', '    [JsonPropertyName("targetName")]\n    public string? TargetName { get; init; }'),
    ('    public MatchMode MatchMode { get; init; }', '    [JsonPropertyName("matchMode")]\n    public MatchMode MatchMode { get; init; }'),
    ('    public TargetFoundAction ActionOnFound { get; init; }', '    [JsonPropertyName("actionOnFound")]\n    public TargetFoundAction ActionOnFound { get; init; }'),
    ('    public double? TimeoutSeconds { get; init; }', '    [JsonPropertyName("timeoutSeconds")]\n    public double? TimeoutSeconds { get; init; }'),
    ('    public int? MaxSteps { get; init; }', '    [JsonPropertyName("maxSteps")]\n    public int? MaxSteps { get; init; }'),
    # CompletionPolicy constructor
    ('        CompletionPolicyType Type = CompletionPolicyType.Exhaustive,\n        string? TargetName = null,\n        MatchMode MatchMode = MatchMode.Exact,\n        TargetFoundAction ActionOnFound = TargetFoundAction.MarkAndStop,\n        double? TimeoutSeconds = null,\n        int? MaxSteps = null)', '        [JsonPropertyName("type")] CompletionPolicyType Type = CompletionPolicyType.Exhaustive,\n        [JsonPropertyName("targetName")] string? TargetName = null,\n        [JsonPropertyName("matchMode")] MatchMode MatchMode = MatchMode.Exact,\n        [JsonPropertyName("actionOnFound")] TargetFoundAction ActionOnFound = TargetFoundAction.MarkAndStop,\n        [JsonPropertyName("timeoutSeconds")] double? TimeoutSeconds = null,\n        [JsonPropertyName("maxSteps")] int? MaxSteps = null)'),
    # IntentSlots primary constructor
    ('public sealed record class IntentSlots(\n    string TargetApp,\n    string Scope,\n    string? Target = null,\n    int? Depth = null,\n    string? ElementHandling = null,\n    string? Navigation = null,\n    bool? Restore = null,\n    string? Completion = null,\n    string? Entry = null);', 'public sealed record class IntentSlots(\n    [property: JsonPropertyName("targetApp")] string TargetApp,\n    [property: JsonPropertyName("scope")] string Scope,\n    [property: JsonPropertyName("target")] string? Target = null,\n    [property: JsonPropertyName("depth")] int? Depth = null,\n    [property: JsonPropertyName("elementHandling")] string? ElementHandling = null,\n    [property: JsonPropertyName("navigation")] string? Navigation = null,\n    [property: JsonPropertyName("restore")] bool? Restore = null,\n    [property: JsonPropertyName("completion")] string? Completion = null,\n    [property: JsonPropertyName("entry")] string? Entry = null);'),
    # TraversalPlan properties
    ('    public string EntryApp { get; init; }', '    [JsonPropertyName("entryApp")]\n    public string EntryApp { get; init; }'),
    ('    public string PlanName { get; init; }', '    [JsonPropertyName("planName")]\n    public string PlanName { get; init; }'),
    ('    public string PlanId { get; init; }', '    [JsonPropertyName("planId")]\n    public string PlanId { get; init; }'),
    ('    public EntryPolicy EntryPolicy { get; init; }', '    [JsonPropertyName("entryPolicy")]\n    public EntryPolicy EntryPolicy { get; init; }'),
    ('    public EntryConfig? EntryConfig { get; init; }', '    [JsonPropertyName("entryConfig")]\n    public EntryConfig? EntryConfig { get; init; }'),
    ('    public TraversalNode? RootNode { get; init; }', '    [JsonPropertyName("rootNode")]\n    public TraversalNode? RootNode { get; init; }'),
    ('    public Dictionary<string, TraversalNode>? StaticNodes { get; init; }', '    [JsonPropertyName("staticNodes")]\n    public Dictionary<string, TraversalNode>? StaticNodes { get; init; }'),
    ('    public string? TemplateRegistry { get; init; }', '    [JsonPropertyName("templateRegistry")]\n    public string? TemplateRegistry { get; init; }'),
    ('    public TraversalMode Mode { get; init; }', '    [JsonPropertyName("mode")]\n    public TraversalMode Mode { get; init; }'),
    ('    public CompletionPolicy? CompletionPolicy { get; init; }', '    [JsonPropertyName("completionPolicy")]\n    public CompletionPolicy? CompletionPolicy { get; init; }'),
    ('    public IntentSlots? IntentSlots { get; init; }', '    [JsonPropertyName("intentSlots")]\n    public IntentSlots? IntentSlots { get; init; }'),
    ('    public Dictionary<string, object>? Meta { get; init; }', '    [JsonPropertyName("meta")]\n    public Dictionary<string, object>? Meta { get; init; }'),
    # TraversalPlan constructor
    ('        string EntryApp,\n        EntryPolicy EntryPolicy,\n        string PlanName = "",\n        string PlanId = "",\n        EntryConfig? EntryConfig = null,\n        TraversalNode? RootNode = null,\n        Dictionary<string, TraversalNode>? StaticNodes = null,\n        string? TemplateRegistry = null,\n        TraversalMode Mode = TraversalMode.Hybrid,\n        CompletionPolicy? CompletionPolicy = null,\n        IntentSlots? IntentSlots = null,\n        Dictionary<string, object>? Meta = null)', '        [JsonPropertyName("entryApp")] string EntryApp,\n        [JsonPropertyName("entryPolicy")] EntryPolicy EntryPolicy,\n        [JsonPropertyName("planName")] string PlanName = "",\n        [JsonPropertyName("planId")] string PlanId = "",\n        [JsonPropertyName("entryConfig")] EntryConfig? EntryConfig = null,\n        [JsonPropertyName("rootNode")] TraversalNode? RootNode = null,\n        [JsonPropertyName("staticNodes")] Dictionary<string, TraversalNode>? StaticNodes = null,\n        [JsonPropertyName("templateRegistry")] string? TemplateRegistry = null,\n        [JsonPropertyName("mode")] TraversalMode Mode = TraversalMode.Hybrid,\n        [JsonPropertyName("completionPolicy")] CompletionPolicy? CompletionPolicy = null,\n        [JsonPropertyName("intentSlots")] IntentSlots? IntentSlots = null,\n        [JsonPropertyName("meta")] Dictionary<string, object>? Meta = null)'),
])

# === TraversalNode.cs ===
print("Annotating TraversalNode.cs...")
tn_path = os.path.join(BASE, 'Graph/Models/TraversalNode.cs')
add_using(tn_path, 'using System.Text.Json.Serialization;')

annotate_file(tn_path, [
    # ChildrenStrategy properties
    ('    public ChildrenStrategyType Type { get; init; }', '    [JsonPropertyName("type")]\n    public ChildrenStrategyType Type { get; init; }'),
    ('    public List<string>? StaticChildren { get; init; }', '    [JsonPropertyName("staticChildren")]\n    public List<string>? StaticChildren { get; init; }'),
    ('    public Dictionary<string, DynamicRule>? DynamicRules { get; init; }', '    [JsonPropertyName("dynamicRules")]\n    public Dictionary<string, DynamicRule>? DynamicRules { get; init; }'),
    ('    public int MaxChildren { get; init; }', '    [JsonPropertyName("maxChildren")]\n    public int MaxChildren { get; init; }'),
    # ChildrenStrategy constructor
    ('        ChildrenStrategyType Type,\n        List<string>? StaticChildren = null,\n        Dictionary<string, DynamicRule>? DynamicRules = null,\n        int MaxChildren = 100)', '        [JsonPropertyName("type")] ChildrenStrategyType Type,\n        [JsonPropertyName("staticChildren")] List<string>? StaticChildren = null,\n        [JsonPropertyName("dynamicRules")] Dictionary<string, DynamicRule>? DynamicRules = null,\n        [JsonPropertyName("maxChildren")] int MaxChildren = 100)'),
    # DynamicRule properties
    ('    public string RuleId { get; init; }', '    [JsonPropertyName("ruleId")]\n    public string RuleId { get; init; }'),
    ('    public MatchCondition MatchCondition { get; init; }', '    [JsonPropertyName("matchCondition")]\n    public MatchCondition MatchCondition { get; init; }'),
    ('    public string ChildTemplate { get; init; }', '    [JsonPropertyName("childTemplate")]\n    public string ChildTemplate { get; init; }'),
    ('    public MatchAction Action { get; init; }', '    [JsonPropertyName("action")]\n    public MatchAction Action { get; init; }'),
    # DynamicRule constructor
    ('        string RuleId,\n        MatchCondition MatchCondition,\n        string ChildTemplate,\n        MatchAction Action)', '        [JsonPropertyName("ruleId")] string RuleId,\n        [JsonPropertyName("matchCondition")] MatchCondition MatchCondition,\n        [JsonPropertyName("childTemplate")] string ChildTemplate,\n        [JsonPropertyName("action")] MatchAction Action)'),
    # MatchCondition primary constructor
    ('public sealed record class MatchCondition(\n    string? Type = null,\n    string? ExpectedAction = null,\n    string? TextPattern = null,\n    TextMatchMode TextMatchMode = TextMatchMode.Contains,\n    int? MinIndex = null,\n    int? MaxIndex = null,\n    Dictionary<string, object>? Custom = null);', 'public sealed record class MatchCondition(\n    [property: JsonPropertyName("type")] string? Type = null,\n    [property: JsonPropertyName("expectedAction")] string? ExpectedAction = null,\n    [property: JsonPropertyName("textPattern")] string? TextPattern = null,\n    [property: JsonPropertyName("textMatchMode")] TextMatchMode TextMatchMode = TextMatchMode.Contains,\n    [property: JsonPropertyName("minIndex")] int? MinIndex = null,\n    [property: JsonPropertyName("maxIndex")] int? MaxIndex = null,\n    [property: JsonPropertyName("custom")] Dictionary<string, object>? Custom = null);'),
    # ErrorPolicy properties
    ('    public ErrorPolicyType OnError { get; init; }', '    [JsonPropertyName("onError")]\n    public ErrorPolicyType OnError { get; init; }'),
    ('    public int MaxRetries { get; init; }', '    [JsonPropertyName("maxRetries")]\n    public int MaxRetries { get; init; }'),
    ('    public string? FallbackTarget { get; init; }', '    [JsonPropertyName("fallbackTarget")]\n    public string? FallbackTarget { get; init; }'),
    ('    public bool ContinueOnError { get; init; }', '    [JsonPropertyName("continueOnError")]\n    public bool ContinueOnError { get; init; }'),
    # ErrorPolicy constructor
    ('        ErrorPolicyType OnError,\n        int MaxRetries = 1,\n        string? FallbackTarget = null,\n        bool ContinueOnError = false)', '        [JsonPropertyName("onError")] ErrorPolicyType OnError,\n        [JsonPropertyName("maxRetries")] int MaxRetries = 1,\n        [JsonPropertyName("fallbackTarget")] string? FallbackTarget = null,\n        [JsonPropertyName("continueOnError")] bool ContinueOnError = false)'),
    # Precondition properties
    ('    public string? PageName { get; init; }', '    [JsonPropertyName("pageName")]\n    public string? PageName { get; init; }'),
    ('    public List<string>? Path { get; init; }', '    [JsonPropertyName("path")]\n    public List<string>? Path { get; init; }'),
    ('    public string? UiCondition { get; init; }', '    [JsonPropertyName("uiCondition")]\n    public string? UiCondition { get; init; }'),
    ('    public double TimeoutSeconds { get; init; }', '    [JsonPropertyName("timeoutSeconds")]\n    public double TimeoutSeconds { get; init; }'),
    # Precondition constructor
    ('        string? PageName = null,\n        List<string>? Path = null,\n        string? UiCondition = null,\n        double TimeoutSeconds = 5.0)', '        [JsonPropertyName("pageName")] string? PageName = null,\n        [JsonPropertyName("path")] List<string>? Path = null,\n        [JsonPropertyName("uiCondition")] string? UiCondition = null,\n        [JsonPropertyName("timeoutSeconds")] double TimeoutSeconds = 5.0)'),
    # TraversalNode properties
    ('    public string NodeId { get; init; }', '    [JsonPropertyName("nodeId")]\n    public string NodeId { get; init; }'),
    ('    public string Name { get; init; }', '    [JsonPropertyName("name")]\n    public string Name { get; init; }'),
    ('    public NodeType NodeType { get; init; }', '    [JsonPropertyName("nodeType")]\n    public NodeType NodeType { get; init; }'),
    ('    public Operation Operation { get; init; }', '    [JsonPropertyName("operation")]\n    public Operation Operation { get; init; }'),
    ('    public ChildrenStrategy ChildrenStrategy { get; init; }', '    [JsonPropertyName("childrenStrategy")]\n    public ChildrenStrategy ChildrenStrategy { get; init; }'),
    ('    public Precondition? Precondition { get; init; }', '    [JsonPropertyName("precondition")]\n    public Precondition? Precondition { get; init; }'),
    ('    public ErrorPolicy? ErrorPolicy { get; init; }', '    [JsonPropertyName("errorPolicy")]\n    public ErrorPolicy? ErrorPolicy { get; init; }'),
    ('    public Dictionary<string, object>? Meta { get; init; }', '    [JsonPropertyName("meta")]\n    public Dictionary<string, object>? Meta { get; init; }'),
    # TraversalNode constructor
    ('        string NodeId,\n        string Name,\n        NodeType NodeType,\n        Operation Operation,\n        ChildrenStrategy ChildrenStrategy,\n        Precondition? Precondition = null,\n        ErrorPolicy? ErrorPolicy = null,\n        Dictionary<string, object>? Meta = null)', '        [JsonPropertyName("nodeId")] string NodeId,\n        [JsonPropertyName("name")] string Name,\n        [JsonPropertyName("nodeType")] NodeType NodeType,\n        [JsonPropertyName("operation")] Operation Operation,\n        [JsonPropertyName("childrenStrategy")] ChildrenStrategy ChildrenStrategy,\n        [JsonPropertyName("precondition")] Precondition? Precondition = null,\n        [JsonPropertyName("errorPolicy")] ErrorPolicy? ErrorPolicy = null,\n        [JsonPropertyName("meta")] Dictionary<string, object>? Meta = null)'),
    # JsonIgnore on computed properties
    ('    public bool IsContainer =>', '    [JsonIgnore]\n    public bool IsContainer =>'),
    ('    public bool IsLeaf =>', '    [JsonIgnore]\n    public bool IsLeaf =>'),
    ('    public List<string> StaticChildren =>', '    [JsonIgnore]\n    public List<string> StaticChildren =>'),
])

# === Operation.cs ===
print("Annotating Operation.cs...")
op_path = os.path.join(BASE, 'Domain/Models/Common/Operation.cs')
add_using(op_path, 'using System.Text.Json.Serialization;')

annotate_file(op_path, [
    ('    public OperationType Action { get; init; }', '    [JsonPropertyName("action")]\n    public OperationType Action { get; init; }'),
    ('    public Target? Target { get; init; }', '    [JsonPropertyName("target")]\n    public Target? Target { get; init; }'),
    ('    public ImmutableDictionary<string, object> Params { get; init; } = ImmutableDictionary<string, object>.Empty;', '    [JsonPropertyName("params")]\n    public ImmutableDictionary<string, object> Params { get; init; } = ImmutableDictionary<string, object>.Empty;'),
    ('    public RestoreAction? Restore { get; init; }', '    [JsonPropertyName("restore")]\n    public RestoreAction? Restore { get; init; }'),
    ('        OperationType Action,\n        Target? Target = null,\n        ImmutableDictionary<string, object>? Params = null,\n        RestoreAction? Restore = null)', '        [JsonPropertyName("action")] OperationType Action,\n        [JsonPropertyName("target")] Target? Target = null,\n        [JsonPropertyName("params")] ImmutableDictionary<string, object>? Params = null,\n        [JsonPropertyName("restore")] RestoreAction? Restore = null)'),
])

# === Target.cs ===
print("Annotating Target.cs...")
tgt_path = os.path.join(BASE, 'Domain/Models/Common/Target.cs')
add_using(tgt_path, 'using System.Text.Json.Serialization;')

annotate_file(tgt_path, [
    ('    public TargetType By { get; init; }', '    [JsonPropertyName("by")]\n    public TargetType By { get; init; }'),
    ('    public object Value { get; init; }', '    [JsonPropertyName("value")]\n    public object Value { get; init; }'),
    ('    public ImmutableDictionary<string, object> Meta { get; init; } = ImmutableDictionary<string, object>.Empty;', '    [JsonPropertyName("meta")]\n    public ImmutableDictionary<string, object> Meta { get; init; } = ImmutableDictionary<string, object>.Empty;'),
    ('        TargetType By,\n        object Value,\n        ImmutableDictionary<string, object>? Meta = null)', '        [JsonPropertyName("by")] TargetType By,\n        [JsonPropertyName("value")] object Value,\n        [JsonPropertyName("meta")] ImmutableDictionary<string, object>? Meta = null)'),
])

# === RestoreAction.cs ===
print("Annotating RestoreAction.cs...")
ra_path = os.path.join(BASE, 'Domain/Models/Common/RestoreAction.cs')
add_using(ra_path, 'using System.Text.Json.Serialization;')

annotate_file(ra_path, [
    ('    public OperationType Action { get; init; }', '    [JsonPropertyName("action")]\n    public OperationType Action { get; init; }'),
    ('    public Target? Target { get; init; }', '    [JsonPropertyName("target")]\n    public Target? Target { get; init; }'),
    ('    public ImmutableDictionary<string, object> Params { get; init; } = ImmutableDictionary<string, object>.Empty;', '    [JsonPropertyName("params")]\n    public ImmutableDictionary<string, object> Params { get; init; } = ImmutableDictionary<string, object>.Empty;'),
    ('        OperationType Action,\n        Target? Target = null,\n        ImmutableDictionary<string, object>? Params = null)', '        [JsonPropertyName("action")] OperationType Action,\n        [JsonPropertyName("target")] Target? Target = null,\n        [JsonPropertyName("params")] ImmutableDictionary<string, object>? Params = null)'),
])

# === EntryConfig.cs ===
print("Annotating EntryConfig.cs...")
ec_path = os.path.join(BASE, 'Graph/Models/EntryConfig.cs')
add_using(ec_path, 'using System.Text.Json.Serialization;')

annotate_file(ec_path, [
    ('    public WaitMode WaitMode { get; init; }', '    [JsonPropertyName("waitMode")]\n    public WaitMode WaitMode { get; init; }'),
    ('    public double WaitTimeoutSeconds { get; init; }', '    [JsonPropertyName("waitTimeoutSeconds")]\n    public double WaitTimeoutSeconds { get; init; }'),
    ('    public int WaitIntervalMs { get; init; }', '    [JsonPropertyName("waitIntervalMs")]\n    public int WaitIntervalMs { get; init; }'),
    ('    public int ActionDelayMs { get; init; }', '    [JsonPropertyName("actionDelayMs")]\n    public int ActionDelayMs { get; init; }'),
    ('    public TraceLevel TraceLevel { get; init; }', '    [JsonPropertyName("traceLevel")]\n    public TraceLevel TraceLevel { get; init; }'),
    ('        WaitMode WaitMode = WaitMode.Fast,\n        double WaitTimeoutSeconds = 10.0,\n        int WaitIntervalMs = 500,\n        int ActionDelayMs = 300,\n        TraceLevel TraceLevel = TraceLevel.None)', '        [JsonPropertyName("waitMode")] WaitMode WaitMode = WaitMode.Fast,\n        [JsonPropertyName("waitTimeoutSeconds")] double WaitTimeoutSeconds = 10.0,\n        [JsonPropertyName("waitIntervalMs")] int WaitIntervalMs = 500,\n        [JsonPropertyName("actionDelayMs")] int ActionDelayMs = 300,\n        [JsonPropertyName("traceLevel")] TraceLevel TraceLevel = TraceLevel.None)'),
])

# === DomainJsonOptions.cs ===
print("Annotating DomainJsonOptions.cs...")
djo_path = os.path.join(BASE, 'Domain/DomainJsonOptions.cs')
annotate_file(djo_path, [
    ('Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }', 'Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase),\n                       new ObjectDictionaryConverter(),\n                       new ImmutableObjectDictionaryConverter() }'),
])

print("\nAll annotations applied!")
