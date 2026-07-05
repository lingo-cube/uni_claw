#!/bin/bash
# Claude Code pre-edit context routing hook
# Reads tool input JSON from stdin, extracts file_path,
# and prints doc reminders based on which layer the file belongs to.
# Hook stdout is injected into Claude's context before the edit proceeds.

INPUT=$(cat)
FILE_PATH=$(echo "$INPUT" | python3 -c "import sys,json; d=json.load(sys.stdin); print(d.get('file_path',''))" 2>/dev/null)

if [ -z "$FILE_PATH" ]; then
    exit 0
fi

# Match layer by directory path
case "$FILE_PATH" in
    */Domain/*)
        echo "📋 Context Routing: 编辑 Domain 层 → 必读 constitution/* + layers/domain.md"
        ;;
    */Graph/*)
        echo "📋 Context Routing: 编辑 Graph 层 → 必读 constitution/* + layers/graph.md | 按需: patterns/fsm-design"
        ;;
    */StateMachine/*)
        echo "📋 Context Routing: 编辑 StateMachine 层 → 必读 constitution/* + patterns/fsm-design + layers/state-machine.md | 按需: patterns/handler-pipeline, patterns/dispatch-table"
        ;;
    */Traversal/*)
        echo "📋 Context Routing: 编辑 Traversal 层 → 必读 constitution/* + patterns/dispatch-table + layers/traversal.md | 按需: patterns/fsm-design"
        ;;
    */AI/*)
        echo "📋 Context Routing: 编辑 AI 层 → 必读 constitution/* + layers/state-machine.md (TraversalContextSnapshot)"
        ;;
    */Observability/*)
        echo "📋 Context Routing: 编辑 Observability 层 → cross-cutting utility, 影响 StateMachine + Traversal"
        ;;
esac

exit 0
