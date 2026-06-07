"""Display visited nodes as a tree structure from trace data.

Shows both visited nodes and skipped elements for complete visibility.
"""

import json
import sys
from pathlib import Path
from collections import defaultdict

def parse_node_id(node_id):
    """Parse node ID to extract components.

    Pattern: type-name-index-parent
    Examples:
    - root -> ('root', 'root', 0, None)
    - menu_container-Wi-Fi-0-root -> ('menu_container', 'Wi-Fi', 0, 'root')
    - switch_leaf-ON-0-menu_container-Wi-Fi-0-root -> ('switch_leaf', 'ON', 0, 'menu_container-Wi-Fi-0-root')
    """
    if node_id == 'root':
        return ('root', 'root', 0, None)

    parts = node_id.split('-')

    if len(parts) < 3:
        return (node_id, node_id, 0, None)

    node_type = parts[0]

    # Find the FIRST occurrence of pattern: digit followed by non-digit(s)
    # This separates name-index-parent
    index = -1
    index_pos = -1

    for i, part in enumerate(parts[1:], start=1):
        if part.isdigit():
            index_pos = i
            index = int(part)
            break

    if index_pos == -1:
        return (node_id, node_id, 0, None)

    # Name is everything between type and index
    name_parts = parts[1:index_pos]
    name = '-'.join(name_parts) if name_parts else ''

    # Parent is everything after index
    if index_pos + 1 < len(parts):
        parent = '-'.join(parts[index_pos + 1:])
    else:
        parent = 'root'

    return (node_type, name, index, parent)

def build_tree(visited_nodes, skipped_elements=None):
    """Build tree structure from visited nodes and skipped elements."""
    tree = {}
    children = defaultdict(list)

    # First pass: parse all nodes and add to tree
    for node_id in visited_nodes:
        node_type, name, index, parent = parse_node_id(node_id)
        tree[node_id] = {
            'id': node_id,
            'type': node_type,
            'name': name,
            'index': index,
            'parent': parent,
            'children': [],
            'skipped': []  # Will be filled later
        }

    # Second pass: build parent-child relationships
    for node_id, node_data in tree.items():
        parent = node_data['parent']
        if parent and parent in tree:
            children[parent].append(node_id)

    # Third pass: sort children and build the tree
    for parent, child_ids in children.items():
        if parent in tree:
            # Sort by index
            sorted_children = sorted(child_ids, key=lambda x: tree[x]['index'])
            tree[parent]['children'] = [tree[x] for x in sorted_children]

    # Add skipped elements to their parents
    if skipped_elements:
        # Common menu items that appear as artifacts (duplicate names)
        common_artifacts = {'Wi-Fi', 'Bluetooth', 'Display', 'Storage', 'Battery', 'Apps'}

        for parent_id, items in skipped_elements.items():
            # Find the actual parent node
            # parent_id might be a node_id or 'unknown'
            matched_parent = None
            for node_id, node_data in tree.items():
                if node_id == parent_id or node_data['name'] == parent_id:
                    matched_parent = node_id
                    break

            if matched_parent and matched_parent in tree:
                # Add skipped items to this node, filtering out artifacts
                for item_name, item_type in items:
                    # Skip if it's a common artifact (not a real sub-item)
                    if item_name in common_artifacts:
                        continue
                    # Skip if already in the list (avoid duplicates)
                    if item_name not in [s['name'] for s in tree[matched_parent]['skipped']]:
                        tree[matched_parent]['skipped'].append({
                            'name': item_name,
                            'type': item_type
                        })

    return tree

def print_tree(node, indent=0, prefix=''):
    """Print tree structure with ASCII art, including skipped elements."""
    is_last = indent == 0  # Root is always last (only one)

    # Print node
    node_type = node['type']
    node_name = node['name']

    # Format display
    if node_type == 'root':
        display = f"📱 {node_name}"
    elif node_type == 'menu_container':
        display = f"📁 {node_name}"
    elif node_type == 'switch_leaf':
        display = f"🔘 {node_name}"
    else:
        display = f"📄 {node_name}"

    print(f"{prefix}{'└── ' if indent > 0 else ''}{display}")

    # Print skipped elements for this node
    if node['skipped']:
        for i, skipped in enumerate(node['skipped']):
            is_last_skipped = i == len(node['skipped']) - 1 and len(node['children']) == 0
            skipped_prefix = prefix + ('    ' if is_last_skipped else '│   ')
            # Different visual for skipped items
            print(f"{skipped_prefix}{'└── ' if indent > 0 else ''}⏭️  {skipped['name']} (skipped: {skipped['type']})")

    # Print children
    for i, child in enumerate(node['children']):
        is_last_child = i == len(node['children']) - 1
        new_prefix = prefix + ('    ' if is_last else '│   ')
        print_tree(child, indent + 1, new_prefix)

def main():
    """Main function."""
    trace_dir = Path('.traces')
    if not trace_dir.exists():
        print("Error: .traces directory not found")
        return

    # Find latest trace
    trace_dirs = sorted(trace_dir.iterdir(), key=lambda p: p.stat().st_mtime, reverse=True)
    if not trace_dirs:
        print("Error: No traces found")
        return

    latest_trace = trace_dirs[0]
    trace_file = latest_trace / 'trace.jsonl'

    if not trace_file.exists():
        print(f"Error: Trace file not found: {trace_file}")
        return

    print(f"Reading trace from: {trace_file.name}")
    print(f"Trace ID: {latest_trace.name}")

    # Read trace and extract visited nodes and skipped elements
    visited_nodes = set()
    skipped_elements = defaultdict(list)  # parent_id -> list of (name, type) tuples
    step_parent_map = {}  # span_id -> node_id (to track which node was processing)

    with open(trace_file) as f:
        for line in f:
            try:
                data = json.loads(line.strip())
                # Track which node is being processed (for parent context)
                if data.get('node_type') == 'step' and 'node_id' in data:
                    visited_nodes.add(data['node_id'])
                    step_parent_map[data.get('span_id', '')] = data['node_id']
                # Also check for visited_nodes in session_end (if available)
                elif data.get('span_type') == 'session_end':
                    visited = data.get('visited_nodes')
                    if isinstance(visited, list):
                        visited_nodes.update(visited)
                    elif isinstance(visited, set):
                        visited_nodes.update(visited)
                # Extract skipped elements from dynamic_matching spans
                elif data.get('span_type') == 'dynamic_matching':
                    metadata = data.get('metadata', {})
                    if metadata.get('reason') == 'no_match':
                        item = metadata.get('item', {})
                        if item:
                            elem_type = item.get('type', 'unknown')
                            elem_name = item.get('text', 'unknown')
                            # Find parent from parent_span_id
                            parent_span = data.get('parent_span_id', '')
                            parent_id = None
                            # Look for the node that was processing when this was skipped
                            if parent_span in step_parent_map:
                                parent_id = step_parent_map[parent_span]
                            skipped_elements[parent_id or 'unknown'].append((elem_name, elem_type))
            except json.JSONDecodeError:
                continue

    if not visited_nodes:
        print("No visited nodes found in trace")
        return

    # Build and print tree
    print(f"\n{'='*60}")
    print("Traversal Tree Structure (Visited + Skipped)")
    print(f"{'='*60}\n")
    print(f"Total nodes visited: {len(visited_nodes)}")
    print(f"Total skipped elements: {sum(len(items) for items in skipped_elements.values())}\n")

    tree = build_tree(visited_nodes, skipped_elements)

    # Find root
    if 'root' in tree:
        print_tree(tree['root'])
    else:
        # Find nodes without parent (roots)
        roots = [node for node in tree.values() if node['parent'] is None]
        for root in roots:
            print_tree(root)

    print(f"\n{'='*60}")

    # Print summary
    print("\nSummary:")
    print("-" * 60)
    print(f"  Visited nodes: {len(visited_nodes)}")

    # Count meaningful skipped elements from the tree
    meaningful_skipped = 0
    for node_id, node_data in tree.items():
        meaningful_skipped += len(node_data['skipped'])
    print(f"  Skipped elements: {meaningful_skipped}")

    # Count by type
    skipped_by_type = defaultdict(int)
    for node_id, node_data in tree.items():
        for skipped in node_data['skipped']:
            skipped_by_type[skipped['type']] += 1
    if skipped_by_type:
        print(f"  By type: {dict(skipped_by_type)}")

    print(f"\n{'='*60}")

if __name__ == '__main__':
    main()
