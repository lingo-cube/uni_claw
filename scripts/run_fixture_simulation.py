#!/usr/bin/env python
"""
简单的fixture仿真测试脚本

直接使用tests/v6/fixtures/中的真实资产数据进行仿真测试，
不使用复杂的测试框架，生成trace数据供dashboard可视化。
"""

import sys
import json
from pathlib import Path
sys.path.insert(0, str(Path(__file__).parent.parent))

from src.trace.storage import FileStorage
from src.trace.recorder import TraceRecorder
from src.traversal.graph_engine import GraphTraversalEngine
from src.graph.plan import TraversalPlan
from src.graph.node import (
    TraversalNode, NodeType, Operation, EntryPolicy,
    CompletionPolicy, CompletionPolicyType, ChildrenStrategy, ChildrenStrategyType
)
from src.simulation.state_fixture import StateFixture
from src.simulation.stateful_mock_vision import StatefulMockVisionService
from src.simulation.stateful_mock_action import StatefulMockActionExecutor


def create_test_plan(entry_app="test.app", max_steps=3):
    """创建一个简单的测试计划"""
    root = TraversalNode(
        node_id="root",
        node_type=NodeType.CONTAINER,
        name="Root",
        operation=Operation(action="no_action"),
        children_strategy=ChildrenStrategy(
            type=ChildrenStrategyType.STATIC,
            static_children=["child1"],
        ),
    )

    child1 = TraversalNode(
        node_id="child1",
        node_type=NodeType.LEAF_ACTION,
        name="Child1",
        operation=Operation(action="no_action"),
    )

    return TraversalPlan(
        entry_app=entry_app,
        root_node=root,
        static_nodes={"child1": child1},
        completion_policy=CompletionPolicy(
            type=CompletionPolicyType.MAX_STEPS,
            max_steps=max_steps,
        ),
    )


def run_simulation_with_yaml_fixture(yaml_path: str, test_name: str):
    """使用YAML fixture运行仿真"""
    print(f"\n{'='*60}")
    print(f"测试: {test_name}")
    print(f"Fixture: {yaml_path}")
    print(f"{'='*60}")

    # 加载fixture
    fixture = StateFixture.from_yaml(yaml_path)
    print(f"✓ 加载了 {len(fixture.pages)} 个页面")
    print(f"✓ 初始页面: {fixture.initial_page_id}")
    print(f"✓ 转换规则: {len(fixture.transitions)}")

    # 创建计划
    plan = create_test_plan()

    # 创建服务
    vision = StatefulMockVisionService(fixture)
    action = StatefulMockActionExecutor(vision)

    # 设置FileStorage
    storage = FileStorage(base_dir='.traces')
    recorder = TraceRecorder(storage=storage)

    # 创建引擎
    engine = GraphTraversalEngine(
        plan=plan,
        vision_service=vision,
        action_executor=action,
        trace_recorder=recorder,
    )

    # 运行仿真
    print(f"运行仿真...")
    result = engine.run()

    # 获取trace数据
    trace_nodes = storage.read(result.trace_id)

    print(f"✓ 完成 - Trace ID: {result.trace_id}")
    print(f"✓ 记录了 {len(trace_nodes)} 个节点")
    print(f"✓ 状态: {result.status}")

    return result.trace_id


def run_simulation_with_pages_all_fixture(json_path: str, test_name: str):
    """使用pages_all.json fixture运行仿真"""
    print(f"\n{'='*60}")
    print(f"测试: {test_name}")
    print(f"Fixture: {json_path}")
    print(f"{'='*60}")

    # 加载JSON
    with open(json_path, 'r') as f:
        data = json.load(f)

    print(f"✓ 加载了 {len(data)} 个页面场景")

    # 选择第一个页面作为示例
    first_page_key = list(data.keys())[0]
    first_page = data[first_page_key]

    print(f"✓ 使用页面: {first_page_key}")
    print(f"✓ 元素数量: {len(first_page.get('elements', []))}")

    # 创建简单的YAML fixture
    import tempfile
    import yaml

    yaml_content = {
        'pages': {
            'main': {
                'page_name': first_page.get('path', first_page_key).split('/')[-1],
                'elements': [
                    {
                        'id': elem.get('id', f"elem_{i}"),
                        'type': elem.get('element_type', elem.get('class', 'button')),
                        'text': elem.get('text', ''),
                        'coordinate': {
                            'x': 0.5,
                            'y': 0.3 + i * 0.1,
                        },
                    }
                    for i, elem in enumerate(first_page.get('elements', [])[:5])  # 限制5个元素
                ],
                'is_complete': True,
            }
        },
        'transitions': {},
        'initial_page': 'main',
        'history_depth': 10,
    }

    # 写入临时YAML文件
    with tempfile.NamedTemporaryFile(mode='w', suffix='.yaml', delete=False) as f:
        yaml.dump(yaml_content, f)
        temp_yaml = f.name

    try:
        # 使用临时YAML运行仿真
        return run_simulation_with_yaml_fixture(temp_yaml, test_name)
    finally:
        # 清理临时文件
        Path(temp_yaml).unlink()


def run_simulation_with_virtual_pages_fixture(json_path: str, test_name: str):
    """使用virtual_pages_simple.json fixture运行仿真"""
    print(f"\n{'='*60}")
    print(f"测试: {test_name}")
    print(f"Fixture: {json_path}")
    print(f"{'='*60}")

    # 加载JSON
    with open(json_path, 'r') as f:
        data = json.load(f)

    print(f"✓ 加载了 {len(data)} 个页面")

    # 选择第一个页面
    first_page_key = list(data.keys())[0]
    first_page = data[first_page_key]

    print(f"✓ 使用页面: {first_page_key}")
    print(f"✓ 元素数量: {len(first_page.get('elements', []))}")

    # 创建YAML fixture
    import tempfile
    import yaml

    yaml_content = {
        'pages': {
            'main': {
                'page_name': first_page_key,
                'elements': [
                    {
                        'id': elem.get('id', f"elem_{i}"),
                        'type': elem.get('element_type', 'button'),
                        'text': elem.get('text', ''),
                        'coordinate': {
                            'x': 0.5,
                            'y': 0.3 + i * 0.1,
                        },
                    }
                    for i, elem in enumerate(first_page.get('elements', [])[:5])
                ],
                'is_complete': True,
            }
        },
        'transitions': {},
        'initial_page': 'main',
        'history_depth': 10,
    }

    # 写入临时YAML文件
    with tempfile.NamedTemporaryFile(mode='w', suffix='.yaml', delete=False) as f:
        yaml.dump(yaml_content, f)
        temp_yaml = f.name

    try:
        # 使用临时YAML运行仿真
        return run_simulation_with_yaml_fixture(temp_yaml, test_name)
    finally:
        # 清理临时文件
        Path(temp_yaml).unlink()


def run_simulation_with_json_fixture(json_path: str, scenario_name: str, test_name: str):
    """使用JSON fixture场景运行仿真"""
    print(f"\n{'='*60}")
    print(f"测试: {test_name}")
    print(f"Fixture: {json_path}:{scenario_name}")
    print(f"{'='*60}")

    # 加载JSON
    with open(json_path, 'r') as f:
        data = json.load(f)

    # 获取场景
    scenarios = data.get('scenarios', {})
    if scenario_name not in scenarios:
        print(f"❌ 场景 '{scenario_name}' 不存在")
        return None

    scenario = scenarios[scenario_name]
    print(f"✓ 页面名称: {scenario.get('page_name')}")
    print(f"✓ 元素数量: {len(scenario.get('elements', []))}")

    # 创建简单的YAML fixture（临时）
    import tempfile
    import yaml

    yaml_content = {
        'pages': {
            'main': {
                'page_name': scenario.get('page_name', 'MainScreen'),
                'elements': [
                    {
                        'id': elem.get('id'),
                        'type': elem.get('type', 'button'),
                        'text': elem.get('text', ''),
                        'coordinate': elem.get('coordinate', {'x': 0.5, 'y': 0.5}),
                    }
                    for elem in scenario.get('elements', [])
                ],
                'is_complete': True,
            }
        },
        'transitions': {},
        'initial_page': 'main',
        'history_depth': 10,
    }

    # 写入临时YAML文件
    with tempfile.NamedTemporaryFile(mode='w', suffix='.yaml', delete=False) as f:
        yaml.dump(yaml_content, f)
        temp_yaml = f.name

    try:
        # 使用临时YAML运行仿真
        return run_simulation_with_yaml_fixture(temp_yaml, test_name)
    finally:
        # 清理临时文件
        Path(temp_yaml).unlink()


def main():
    """运行所有fixture仿真测试"""
    print("="*60)
    print("Fixture仿真测试")
    print("="*60)

    fixture_dirs = [
        Path("tests/v6/fixtures"),
        Path("tests/assets/fixtures"),
    ]

    trace_ids = []

    # 第一部分：测试 tests/v6/fixtures
    print("\n【第一部分】V6 Fixtures")
    print("-"*60)
    v6_fixture_dir = Path("tests/v6/fixtures")

    # 1. 测试YAML fixture
    if (v6_fixture_dir / "simple_two_page.yaml").exists():
        trace_id = run_simulation_with_yaml_fixture(
            str(v6_fixture_dir / "simple_two_page.yaml"),
            "简单双页面导航"
        )
        if trace_id:
            trace_ids.append(("simple_two_page", trace_id))

    # 第二部分：测试其他 YAML fixtures
    print("\n【第二部分】Additional YAML Fixtures")
    print("-"*60)

    # 简单测试：使用 simple_two_page.yaml 的不同场景
    v6_fixtures_dir = Path("tests/v6/fixtures")
    yaml_files = list(v6_fixtures_dir.glob("*.yaml"))

    for yaml_file in yaml_files:
        fixture_name = yaml_file.stem
        print(f"\n测试 fixture: {fixture_name}")
        try:
            trace_id = run_simulation_with_yaml_fixture(str(yaml_file), f"YAML Fixture: {fixture_name}")
            if trace_id:
                trace_ids.append((fixture_name, trace_id))
        except Exception as e:
            print(f"  ⚠ 失败: {e}")

    # 汇总
    print(f"\n{'='*60}")
    print("仿真测试汇总")
    print(f"{'='*60}")
    print(f"成功运行: {len(trace_ids)} 个 YAML fixture 测试")
    print(f"\n生成的 Trace IDs:")
    for name, tid in trace_ids:
        print(f"  - {name}: {tid}")

    print(f"\n所有trace保存在: .traces/")
    print(f"Dashboard: http://localhost:8080")
    print(f"{'='*60}")

    return trace_ids


if __name__ == "__main__":
    try:
        trace_ids = main()
        print(f"\n✅ 成功！生成了 {len(trace_ids)} 个仿真trace。")
        sys.exit(0)
    except Exception as e:
        print(f"\n❌ 失败: {e}")
        import traceback
        traceback.print_exc()
        sys.exit(1)
