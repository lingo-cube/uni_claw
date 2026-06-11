"""Test match_result.menu_item type."""

import sys
sys.path.insert(0, '.')

from src.graph.matcher import DynamicMatcher, TemplateRegistry, MatchAction
from src.graph.node import TraversalNode, NodeType, Operation, ChildrenStrategy, ChildrenStrategyType, DynamicRule
from src.simulation.stateful_mock_vision import StatefulMockVisionService
from src.simulation.state_fixture import StateFixture, PageState, PageElement
from tests.factories.device_factory import CoordinateFactory

# Create a simple fixture
pages = {
    'home': PageState(
        id='home',
        page_name='Home',
        elements=[
            PageElement(id='wifi', type='menu_item', text='Wi-Fi', coordinate=CoordinateFactory.center().to_dict(), action_target=None),
        ],
        is_complete=False,
    )
}

fixture = StateFixture(
    pages=pages,
    transitions=[],
    initial_page_id='home',
    history_depth=10,
)

# Create vision service
vision = StatefulMockVisionService(fixture)
page_analysis = vision.analyze_screenshot(b"fake")

print("PageAnalysis items:")
for item in page_analysis.items:
    print(f"  - {item.name}: type={item.type}")

# Create matcher
registry = TemplateRegistry()
matcher = DynamicMatcher(registry)

# Load rules
matcher.load_rules({
    "menu_rule": {
        "match_condition": {"type": "button"},
        "child_template": "menu_container",
        "action": "generate_child"
    }
})

# Build items as graph_engine does
items = []
for idx, item in enumerate(page_analysis.items):
    item_type = item.type.value if hasattr(item.type, "value") else str(item.type)
    items.append({
        "type": item_type,
        "text": getattr(item, "name", ""),
        "index": idx,
        "coordinate_x": CoordinateFactory.center().x,
        "coordinate_y": CoordinateFactory.center().y,
    })

print("\nItems to match:")
for item in items:
    print(f"  - {item}")

# Match
results = matcher.match_all(items, parent_node=None)

print("\nMatch results:")
for r in results:
    print(f"  - matched={r.matched}, action={r.action}, menu_item={r.menu_item}, menu_item type={type(r.menu_item)}")
