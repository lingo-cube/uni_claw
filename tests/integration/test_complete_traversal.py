#!/usr/bin/env python3
"""Complete traversal test with real screen data and generate test report."""

import json
import sys
import time
from datetime import datetime
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent.parent.parent))


class TraversalTestReport:
    """Test report generator."""

    def __init__(self):
        """Initialize report."""
        self.start_time = datetime.now()
        self.events = []
        self.clicks = []
        self.screenshots = []
        self.errors = []

    def add_event(self, event_type, details):
        """Add event to report."""
        self.events.append({
            'time': datetime.now().strftime("%H:%M:%S.%f")[:-3],
            'type': event_type,
            'details': details
        })

    def add_click(self, element_name, coord, result):
        """Add click to report."""
        self.clicks.append({
            'time': datetime.now().strftime("%H:%M:%S.%f")[:-3],
            'element': element_name,
            'coordinate': coord,
            'result': result
        })

    def add_error(self, error):
        """Add error to report."""
        self.errors.append({
            'time': datetime.now().strftime("%H:%M:%S.%f")[:-3],
            'error': str(error)
        })

    def generate(self):
        """Generate test report."""
        elapsed = (datetime.now() - self.start_time).total_seconds()

        report = []
        report.append("=" * 80)
        report.append("Uni-claw 遍历测试报告")
        report.append("=" * 80)
        report.append(f"\n📅 测试时间: {self.start_time.strftime('%Y-%m-%d %H:%M:%S')}")
        report.append(f"⏱️  总耗时: {elapsed:.2f}秒")
        report.append(f"📊 事件总数: {len(self.events)}")
        report.append(f"👆 点击总数: {len(self.clicks)}")
        report.append(f"❌ 错误总数: {len(self.errors)}")

        if self.events:
            report.append("\n" + "-" * 80)
            report.append("📋 事件日志")
            report.append("-" * 80)
            for event in self.events:
                report.append(f"[{event['time']}] {event['type']}: {event['details']}")

        if self.clicks:
            report.append("\n" + "-" * 80)
            report.append("👆 点击记录")
            report.append("-" * 80)
            for click in self.clicks:
                report.append(f"[{click['time']}] 点击 {click['element']} at {click['coordinate']} → {click['result']}")

        if self.errors:
            report.append("\n" + "-" * 80)
            report.append("❌ 错误记录")
            report.append("-" * 80)
            for error in self.errors:
                report.append(f"[{error['time']}] {error['error']}")

        report.append("\n" + "=" * 80)
        return "\n".join(report)


class MockVisionWithData:
    """Mock vision service using real test data."""

    def __init__(self, data_file: str):
        """Initialize with test data."""
        with open(data_file) as f:
            self.data = json.load(f)
        self.call_count = 0

    def analyze_screenshot(self, image_data):
        """Return analysis with real data."""
        self.call_count += 1
        from src.models.content_models import PageAnalysis
        return PageAnalysis(**self.data)

    def find_app_entry(self, image_data, target):
        """Mock find entry."""
        return {"x": 0.5, "y": 0.5, "name": target}


def run_complete_traversal_test():
    """Run complete traversal test with real data."""
    print("=" * 80)
    print("Uni-claw 完整遍历测试")
    print("=" * 80)
    print()

    # Initialize report
    report = TraversalTestReport()
    report.add_event("TEST_START", "开始完整遍历测试")

    # Load test data
    data_file = Path("test_data/sample_phone_screen.json")
    if not data_file.exists():
        print(f"❌ 数据文件不存在: {data_file}")
        return 1

    with open(data_file) as f:
        screen_data = json.load(f)

    report.add_event("DATA_LOADED", f"加载屏幕数据: {len(screen_data['items'])}个元素")

    # Import components
    from src.adb import RealADBClient
    from src.models import TraversalState, ContentTree, VisitFingerprint  # TraversalState is alias for SimulationState
    from src.traversal import TraversalConfig

    try:
        # Create ADB client
        adb = RealADBClient()
        screen_size = adb.get_screen_size()
        report.add_event("DEVICE_CONNECTED", f"设备连接成功: {screen_size.width}x{screen_size.height}")

        # Create state
        state = TraversalState()
        tree = ContentTree(root_title="手机桌面")

        print(f"📱 设备信息:")
        print(f"   分辨率: {screen_size.width}x{screen_size.height}")
        print(f"   归一化坐标: 0-1")
        print()

        # Parse screen data
        from src.models.content_models import PageAnalysis
        analysis = PageAnalysis(**screen_data)

        # Initialize structure
        report.add_event("INIT_START", "开始初始化结构")

        # Cache level1 menus
        for menu in analysis.level1_menus:
            state.add_level1_menu(menu)

        # Cache level2 menus
        if analysis.level1_menus:
            level1_name = analysis.level1_menus[0].name
            state.add_level2_menus(level1_name, analysis.level2_menus)

        # Cache items
        level1 = analysis.level1_menus[0].name if analysis.level1_menus else ""
        level2 = analysis.level2_menus[0].name if analysis.level2_menus else ""
        cache_key = f"{level1}|{level2}"
        state.add_items(cache_key, analysis.items)

        # Set current path
        state.current_path = [level1, level2]

        report.add_event("INIT_COMPLETE", f"初始化完成: {len(state.all_level1_menus)}个L1菜单, {len(analysis.level2_menus)}个L2菜单, {len(analysis.items)}个items")

        # Build content tree
        tree.add_node(title=level1, level=1, node_type="menu")
        l1_id = "1"
        tree.add_child_node(title=level2, parent_id=l1_id, node_type="tab")
        l2_id = "1.1"

        print(f"📋 初始结构:")
        print(f"   Level1菜单: {len(state.all_level1_menus)}个")
        for menu in state.all_level1_menus.values():
            status = "🟢" if menu.active else "⚪"
            print(f"      {status} {menu.name}")

        print(f"   Level2菜单: {len(state.get_level2_menus(level1))}个")
        for menu in state.get_level2_menus(level1):
            status = "🟢" if menu.active else "⚪"
            print(f"      {status} {menu.name}")

        print(f"   内容元素: {len(analysis.items)}个")
        print()

        # Traversal simulation
        report.add_event("TRAVERSAL_START", "开始遍历")
        print("🔄 开始遍历:")
        print("-" * 80)

        items_to_visit = state.get_items(cache_key)
        visited_count = 0

        for i, item in enumerate(items_to_visit[:10], 1):  # Limit to 10 for demo
            item_name = item.name
            item_type = item.type
            coord = item.coordinate

            # Calculate pixel coordinates
            px = screen_size.pixel_x(coord.x)
            py = screen_size.pixel_y(coord.y)

            print(f"\n[{i}] 元素: {item_name} ({item_type})")
            print(f"   归一化坐标: ({coord.x:.3f}, {coord.y:.3f})")
            print(f"   像素坐标: ({px}, {py})")

            # Generate fingerprint
            fp = item.get_fingerprint(level1, level2)
            print(f"   指纹: {fp}")

            # Check if visited
            fingerprint = VisitFingerprint(level1=level1, level2=level2, item_name=item_name)
            if state.is_visited(fingerprint):
                print(f"   ⏭️  已跳过 (已访问)")
                report.add_event("ITEM_SKIPPED", f"{item_name} - 已访问")
                continue

            # Simulate click
            print(f"   👆 模拟点击...")
            try:
                # Actually click on real device
                adb.tap(coord.x, coord.y)
                report.add_click(item_name, f"({coord.x:.3f}, {coord.y:.3f})", "SUCCESS")
                print(f"   ✅ 点击成功")

                # Add to tree
                node_id = tree.add_child_node(
                    title=item_name,
                    parent_id=l2_id,
                    node_type="item",
                    coordinate=coord
                )

                # Mark visited
                state.mark_visited(fingerprint)
                visited_count += 1

                # Wait
                time.sleep(0.3)

            except Exception as e:
                report.add_error(f"点击失败: {item_name} - {e}")
                print(f"   ❌ 点击失败: {e}")

        print()
        print("-" * 80)

        # Final summary
        report.add_event("TRAVERSAL_COMPLETE", f"遍历完成: 访问了{visited_count}个元素")

        print(f"\n📊 遍历总结:")
        print(f"   总元素数: {len(items_to_visit)}")
        print(f"   已访问: {visited_count}")
        print(f"   未访问: {len(items_to_visit) - visited_count}")

        # Display content tree
        print(f"\n🌳 内容树:")
        print(tree.to_markdown())

        # Generate report
        print()
        print(report.generate())

        # Save report
        report_file = Path("test_reports") / f"traversal_report_{datetime.now().strftime('%Y%m%d_%H%M%S')}.txt"
        report_file.parent.mkdir(exist_ok=True)
        with open(report_file, "w", encoding="utf-8") as f:
            f.write(report.generate())

        print(f"\n📄 报告已保存: {report_file}")

        return 0

    except Exception as e:
        report.add_error(f"测试异常: {e}")
        print(f"\n❌ 测试失败: {e}")
        import traceback
        traceback.print_exc()
        return 1


if __name__ == "__main__":
    sys.exit(run_complete_traversal_test())
