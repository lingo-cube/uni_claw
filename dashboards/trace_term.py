#!/usr/bin/env python3
"""
Trace Observatory - 终端可视化工具
"""

import sys
import json
from pathlib import Path

# 添加项目根目录到路径
sys.path.insert(0, str(Path(__file__).resolve().parent.parent))

from src.trace.storage import FileStorage
from src.trace.analyzer import TraceAnalyzer, build_tree


def print_header(title):
    """打印标题"""
    print(f"\n{'='*60}")
    print(f"  {title}")
    print(f"{'='*60}\n")


def print_trace_list(storage):
    """打印追踪列表"""
    traces = []
    base = storage._base_dir

    if not base.exists():
        print("❌ 追踪目录不存在")
        return

    for d in sorted(base.iterdir(), reverse=True):
        if d.is_dir() and not d.name.startswith(".") and d.name != "archive":
            try:
                session = storage.read_session(d.name)
                nodes = storage.read(d.name)
                traces.append({
                    "trace_id": d.name,
                    "node_count": len(nodes),
                    "session": session,
                })
            except Exception as e:
                print(f"⚠️  读取 {d.name} 失败: {e}")

    if not traces:
        print("❌ 没有找到追踪数据")
        return

    print(f"📋 找到 {len(traces)} 个追踪:\n")
    for i, trace in enumerate(traces, 1):
        status = "✅" if trace["session"] else "❌"
        session_info = ""
        if trace["session"]:
            device = trace["session"].get("device_model", "Unknown")
            app = trace["session"].get("app_package", "Unknown")
            session_info = f" | 设备: {device} | 应用: {app}"

        print(f"{i}. {status} {trace['trace_id'][:16]}... ({trace['node_count']} nodes){session_info}")


def print_trace_overview(storage, trace_id):
    """打印追踪概览"""
    nodes = storage.read(trace_id)
    session = storage.read_session(trace_id)

    if not nodes:
        print(f"❌ 追踪 {trace_id} 没有数据")
        return

    print_header(f"追踪概览: {trace_id}")

    # 会话信息
    if session:
        print(f"📱 设备: {session.get('device_model', 'Unknown')}")
        print(f"📦 应用: {session.get('app_package', 'Unknown')}")
        print(f"🔄 模式: {session.get('traversal_mode', 'Unknown')}")
        print(f"⏱️  开始: {session.get('start_time', 'Unknown')}")
        print(f"⏱️  结束: {session.get('end_time', 'Unknown')}")
        print(f"✅ 状态: {session.get('status', 'Unknown')}")
        print()

    # 节点统计
    node_types = {}
    span_types = {}

    for node in nodes:
        nt = node.node_type if hasattr(node, 'node_type') else 'unknown'
        node_types[nt] = node_types.get(nt, 0) + 1

        if hasattr(node, 'span_type'):
            st = node.span_type
            span_types[st] = span_types.get(st, 0) + 1

    print("📊 节点类型统计:")
    for ntype, count in sorted(node_types.items()):
        icon = {'session': '📱', 'step': '📍', 'span': '📄'}.get(ntype, '📋')
        print(f"  {icon} {ntype}: {count}")

    if span_types:
        print("\n📊 Span 类型统计:")
        for stype, count in sorted(span_types.items()):
            icon = {'ai_call': '🤖', 'execution': '⚡', 'state_transition': '🔄',
                   'step_end': '🏁', 'error': '❌'}.get(stype, '📄')
            print(f"  {icon} {stype}: {count}")


def print_trace_tree(storage, trace_id):
    """打印追踪树"""
    nodes = storage.read(trace_id)
    if not nodes:
        print(f"❌ 追踪 {trace_id} 没有数据")
        return

    root = build_tree(nodes)
    if not root:
        print(f"❌ 无法构建追踪树")
        return

    print_header(f"追踪树: {trace_id}")

    def print_node(node, level=0):
        """递归打印节点"""
        indent = "  " * level

        # 节点信息
        ntype = node.node_type if hasattr(node, 'node_type') else 'unknown'
        span_id = node.span_id if hasattr(node, 'span_id') else '?'
        span_id_short = span_id[:8] if span_id else '...'

        # 图标
        if ntype == 'session':
            icon = '📱'
            label = f"SESSION {span_id_short}"
        elif ntype == 'step':
            icon = '📍'
            step_type = node.step_type if hasattr(node, 'step_type') else ''
            label = f"STEP {step_type}"
            if hasattr(node, 'page_path') and node.page_path:
                path = ' → '.join(node.page_path)
                label += f" [{path}]"
        elif ntype == 'span':
            icon = '📄'
            span_type = node.span_type if hasattr(node, 'span_type') else ''
            label = f"{span_type.upper()}" if span_type else 'SPAN'
            if hasattr(node, 'action') and node.action:
                target = getattr(node, 'target', '')
                label = f"EXEC: {node.action} → {target}"
        else:
            icon = '📄'
            label = ntype.upper()

        print(f"{indent}{icon} {label}")

        # 递归打印子节点
        if hasattr(node, 'children') and node.children:
            for child in node.children:
                print_node(child, level + 1)

    print_node(root)


def main():
    """主函数"""
    if len(sys.argv) < 2:
        print("用法: python trace_term.py <command> [trace_id]")
        print("")
        print("命令:")
        print("  list              - 列出所有追踪")
        print("  overview <id>     - 显示追踪概览")
        print("  tree <id>         - 显示追踪树")
        print("")
        print("示例:")
        print("  python trace_term.py list")
        print("  python trace_term.py overview 01KTECP2THSY5B2DHF9ZM0TXA4")
        print("  python trace_term.py tree 01KTECP2THSY5B2DHF9ZM0TXA4")
        return

    storage = FileStorage('traces')
    command = sys.argv[1].lower()

    if command == 'list':
        print_trace_list(storage)
    elif command == 'overview':
        if len(sys.argv) < 3:
            print("❌ 请提供追踪 ID")
            return
        print_trace_overview(storage, sys.argv[2])
    elif command == 'tree':
        if len(sys.argv) < 3:
            print("❌ 请提供追踪 ID")
            return
        print_trace_tree(storage, sys.argv[2])
    else:
        print(f"❌ 未知命令: {command}")


if __name__ == '__main__':
    main()
