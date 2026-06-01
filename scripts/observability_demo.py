#!/usr/bin/env python3
"""Demo script showing full observability capabilities."""

import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent.parent))

# Import only core modules, not web server
from src.analysis.trace_analyzer import TraceAnalyzer
from src.analysis.metrics import MetricsCollector, get_metrics_collector
from src.analysis.tree import TraversalTreeBuilder
from src.analysis.results import ResultManager, TraversalResult, ResultStatus
from src.analysis.structured_logging import TraversalLogger, LoggerFactory
import time


def demo_trace_analysis():
    """Demonstrate trace analysis capabilities."""
    print("\n" + "=" * 60)
    print("🔍 Trace Analysis Demo")
    print("=" * 60)

    analyzer = TraceAnalyzer()
    sessions = analyzer.load_all_traces()

    print(f"\nFound {len(sessions)} trace sessions")

    if sessions:
        # Show component performance
        print("\n📊 Component Performance:")
        perf = analyzer.analyze_component_performance()
        for component, stats in perf.items():
            print(f"  {component}:")
            print(f"    Calls: {stats['call_count']}")
            print(f"    Avg: {stats['avg_duration_ms']:.0f}ms")
            print(f"    Max: {stats['max_duration_ms']:.0f}ms")

        # Show slowest operations
        print("\n🐌 Slowest Operations:")
        slowest = analyzer.get_slowest_operations(5)
        for op in slowest:
            print(f"  {op['component']}.{op['operation']}: {op['duration_ms']:.0f}ms")


def demo_metrics_collection():
    """Demonstrate metrics collection."""
    print("\n" + "=" * 60)
    print("📈 Metrics Collection Demo")
    print("=" * 60)

    collector = MetricsCollector()

    # Simulate some AI calls
    print("\nRecording AI calls...")
    collector.record_ai_call("TraversalPlan", "execute", 150, True, 0.95)
    collector.record_ai_call("vision", "analyze_screenshot", 2500, True, None)
    collector.record_ai_call("TraversalPlan", "execute", 120, True, 0.90)

    # Simulate traversal steps
    print("Recording traversal steps...")
    collector.record_traversal_step("session_1", screens_count=2, duration_ms=3000, visited_count=2)

    # Show summary
    print("\n📊 AI Metrics Summary:")
    ai_metrics = collector.get_ai_metrics_summary()
    for key, metrics in ai_metrics.items():
        print(f"  {key}:")
        print(f"    Calls: {metrics['total_calls']}")
        print(f"    Success Rate: {metrics['success_rate']:.1f}%")
        print(f"    Avg Duration: {metrics['avg_duration_ms']:.0f}ms")


def demo_tree_builder():
    """Demonstrate tree building."""
    print("\n" + "=" * 60)
    print("🌳 Tree Builder Demo")
    print("=" * 60)

    builder = TraversalTreeBuilder()

    # Sample visited items
    visited_items = [
        {"name": "Network & Internet", "type": "menu_item", "path": ["Settings"], "coordinate": {"x": 0.5, "y": 0.3}},
        {"name": "WiFi", "type": "menu_item", "path": ["Settings", "Network & Internet"], "coordinate": {"x": 0.5, "y": 0.4}},
        {"name": "Bluetooth", "type": "menu_item", "path": ["Settings", "Network & Internet"], "coordinate": {"x": 0.5, "y": 0.5}},
    ]

    tree = builder.build_from_visited_items(visited_items)

    print("\n📄 Markdown Tree:")
    print(tree.to_markdown())

    print("\n💾 JSON Tree:")
    print(tree.to_json()[:200] + "...")


def demo_result_manager():
    """Demonstrate result management."""
    print("\n" + "=" * 60)
    print("💾 Result Manager Demo")
    print("=" * 60)

    manager = ResultManager()

    # Create sample result
    result = TraversalResult(
        session_id="demo_session",
        trace_id="abcd1234",
        status=ResultStatus.SUCCESS,
        start_time=time.time() - 3600,
        end_time=time.time(),
        instruction="遍历设置选项",
        entry_app="设置",
        max_steps=50,
        visited_items=[
            {"name": "WiFi", "type": "menu_item", "path": ["Settings"]},
            {"name": "Bluetooth", "type": "menu_item", "path": ["Settings"]},
        ],
        skipped_items=[
            {"name": "Factory Reset", "reason": "safety_check"},
        ],
        screens_analyzed=15,
        total_duration_ms=45000,
        final_path=["Settings"],
    )

    print("\n📋 Result Summary:")
    print(result.to_summary())

    # Save result
    filepath = manager.save_result(result)
    print(f"\n💾 Saved to: {filepath}")

    # Generate reports
    html_report = manager.generate_report(result, "html")
    md_report = manager.generate_report(result, "markdown")

    print(f"\n📄 HTML Report: {html_report}")
    print(f"📝 Markdown Report: {md_report}")


def demo_structured_logging():
    """Demonstrate structured logging."""
    print("\n" + "=" * 60)
    print("📝 Structured Logging Demo")
    print("=" * 60)

    logger = LoggerFactory.get_logger("demo_session")

    print("\nSimulating traversal session...")

    logger.log_session_start(
        instruction="遍历所有系统设置的选项",
        max_steps=50,
        entry_app="设置"
    )

    time.sleep(0.1)

    logger.log_step(
        action="tap",
        target="Network & Internet",
        coordinate={"x": 0.5, "y": 0.3},
        success=True,
        duration_ms=150
    )

    logger.log_visited_item(
        item_name="Network & Internet",
        item_type="menu_item",
        path=["Settings"],
        coordinate={"x": 0.5, "y": 0.3}
    )

    logger.log_screen_analysis(
        items_count=8,
        path=["Settings", "Network & Internet"],
        duration_ms=2500
    )

    logger.log_ai_call(
        service="vision",
        operation="analyze_screenshot",
        duration_ms=2500,
        success=True,
        confidence=None
    )

    logger.log_skipped_item("Factory Reset", "safety_check")

    logger.log_session_end(
        status="success",
        steps=5,
        visited=3,
        duration_ms=15000
    )

    print(f"\n✅ Logs written to: {logger.log_file}")


def main():
    """Run all demos."""
    print("\n🚀 Traversal Observability Demo")
    print("=" * 60)

    demo_trace_analysis()
    demo_metrics_collection()
    demo_tree_builder()
    demo_result_manager()
    demo_structured_logging()

    print("\n" + "=" * 60)
    print("✅ Demo Complete!")
    print("=" * 60)
    print("\n📊 View results:")
    print("  - Traces: .traces/*.jsonl")
    print("  - Results: .results/sessions/*.json")
    print("  - Logs: .logs/*.jsonl")
    print("  - Reports: .results/reports/*")


if __name__ == "__main__":
    main()
