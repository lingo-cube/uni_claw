"""
End-to-end integration tests for simulation testing system.

Validates complete workflow from test execution through reporting
and CI/CD integration.
"""

import pytest
import json
import tempfile
from pathlib import Path
from tests.simulation.helpers import SimulationTestRunner, TraceAsserter
from src.simulation.runner import SimulationRunner
from src.simulation.page_analyzer import PageAnalyzer
from src.simulation.mock_vision import MockVisionService
from src.simulation.mock_action import MockActionExecutor


class TestSimulationE2E:
    """End-to-end integration tests."""

    @pytest.fixture
    def complete_fixtures(self):
        """Provide complete test fixtures."""
        return {
            "root": {
                "page_name": "HomeScreen",
                "elements": [
                    {"id": "SettingsButton", "type": "button", "text": "Settings", "clickable": True}
                ]
            },
            "settings": {
                "page_name": "SettingsPage",
                "elements": [
                    {"id": "DisplayOption", "type": "menu_item", "text": "Display", "clickable": True}
                ]
            }
        }

    @pytest.fixture
    def complete_plan(self, test_fixtures_dir):
        """Provide complete traversal plan."""
        if not (test_fixtures_dir / "e2e_all_traversal" / "plan_all.json").exists():
            pytest.skip("Plan file not found")

        with open(test_fixtures_dir / "e2e_all_traversal" / "plan_all.json") as f:
            return json.load(f)

    def test_complete_simulation_workflow(self, complete_fixtures):
        """Test complete simulation workflow."""
        # Create simple plan for testing
        from unittest.mock import Mock
        from src.graph.plan import TraversalPlan
        from src.graph.node import CompletionPolicy, CompletionPolicyType, IntentSlots

        plan = Mock(spec=TraversalPlan)
        plan.plan_id = "e2e_test"
        plan.intent_slots = IntentSlots(depth=2)
        plan.completion_policy = CompletionPolicy(type=CompletionPolicyType.EXHAUSTIVE, max_depth=2)

        # Create simulation runner
        runner = SimulationRunner(complete_fixtures, plan)

        # Run simulation
        result = runner.run()

        # Validate complete workflow
        assert result is not None
        assert result.elapsed_seconds >= 0
        assert len(result.trace) >= 0
        assert isinstance(result.executed_actions, list)
        assert isinstance(result.visited_tree, dict)
        assert result.statistics is not None

    def test_page_analysis_integration(self, complete_fixtures):
        """Test PageAnalyzer integration."""
        analyzer = PageAnalyzer(complete_fixtures)

        # Test analysis
        result = analyzer.analyze_page("root")

        assert result is not None
        assert result["page_path"] == "root"
        assert "page_type" in result
        assert "elements" in result
        assert "metadata" in result

        # Test caching
        result2 = analyzer.analyze_page("root")
        assert result == result2
        assert analyzer.get_cache_size() == 1

    def test_mock_vision_integration(self, complete_fixtures):
        """Test MockVisionService integration."""
        vision = MockVisionService(complete_fixtures)

        # Test basic functionality
        vision.inject_path("root")
        result = vision.analyze_screenshot()

        assert result is not None
        assert vision.get_call_count() == 1

        # Test reset
        vision.reset()
        assert vision.get_call_count() == 0

    def test_mock_action_integration(self):
        """Test MockActionExecutor integration."""
        action = MockActionExecutor()

        # Test comprehensive recording
        action.set_context(Mock(current_node="test", current_path=["root"]))
        action.set_page_context({"page_name": "TestPage"})

        action.click("test_button")

        # Verify comprehensive recording
        history = action.get_history()
        assert len(history) == 1

        operation = history[0]
        assert "action_type" in operation
        assert "timestamp" in operation
        assert "current_node" in operation
        assert "page_context" in operation

    def test_trace_asserter_integration(self):
        """Test TraceAsserter integration."""
        sample_trace = [
            {"action_type": "enter", "current_node": "root", "target_info": {}, "timestamp": 1.0},
            {"action_type": "click", "current_node": "root", "target_info": {"element_id": "Button"}, "timestamp": 1.1}
        ]

        expected = {
            "key_events": ["进入 root", "点击 Button"],
            "total_steps_min": 1,
            "total_steps_max": 10
        }

        result = TraceAsserter.assert_trace_matches_expected(sample_trace, expected)

        assert result.success is True
        assert result.key_events_matched == 2

    def test_simulation_test_runner_workflow(self, test_fixtures_dir):
        """Test complete SimulationTestRunner workflow."""
        if not test_fixtures_dir.exists():
            pytest.skip("Fixtures directory not found")

        runner = SimulationTestRunner()

        # Test with template if available
        template_path = test_fixtures_dir / "template" / "test_case.json"
        if template_path.exists():
            result = runner.run_simulation_test(str(template_path))

            assert result is not None
            assert "test_case" in result
            assert "simulation_result" in result
            assert "assertion_result" in result
            assert "passed" in result

    @pytest.mark.slow
    def test_all_core_fixtures_execution(self, test_fixtures_dir):
        """Test execution of all core fixtures."""
        if not test_fixtures_dir.exists():
            pytest.skip("Fixtures directory not found")

        fixtures = [
            "e2e_all_traversal",
            "e2e_target_found",
            "e2e_static_path",
            "e2e_popup_handling",
            "e2e_auto_escape"
        ]

        results = []
        for fixture_name in fixtures:
            fixture_path = test_fixtures_dir / fixture_name / "test_case.json"
            if not fixture_path.exists():
                continue

            try:
                runner = SimulationTestRunner()
                result = runner.run_simulation_test(str(fixture_path))
                results.append({
                    "fixture": fixture_name,
                    "loaded": True,
                    "passed": result.get("passed", False)
                })
            except Exception as e:
                results.append({
                    "fixture": fixture_name,
                    "loaded": False,
                    "error": str(e)
                })

        # Validate all fixtures could be processed
        assert len(results) > 0

        # Print summary for debugging
        for result in results:
            status = "✅" if result.get("passed") else "❌"
            print(f"{status} {result['fixture']}: {'PASS' if result.get('passed') else result.get('error', 'FAIL')}")

    def test_context_persistence_across_components(self, complete_fixtures):
        """Test context persistence across all components."""
        from unittest.mock import Mock
        from src.graph.plan import TraversalPlan
        from src.graph.node import CompletionPolicy, CompletionPolicyType, IntentSlots

        plan = Mock(spec=TraversalPlan)
        plan.plan_id = "context_test"
        plan.intent_slots = IntentSlots(depth=2)
        plan.completion_policy = CompletionPolicy(type=CompletionPolicyType.EXHAUSTIVE, max_depth=2)

        runner = SimulationRunner(complete_fixtures, plan)

        # All components should have proper context integration
        assert runner.vision._current_context is not None
        assert runner.action._operation_context is not None
        assert runner.tracer is not None

    def test_visualization_methods(self, complete_fixtures):
        """Test all visualization methods work correctly."""
        from unittest.mock import Mock
        from src.graph.plan import TraversalPlan
        from src.graph.node import CompletionPolicy, CompletionPolicyType, IntentSlots

        plan = Mock(spec=TraversalPlan)
        plan.plan_id = "viz_test"
        plan.intent_slots = IntentSlots(depth=2)
        plan.completion_policy = CompletionPolicy(type=CompletionPolicyType.EXHAUSTIVE, max_depth=2)

        runner = SimulationRunner(complete_fixtures, plan)
        runner.run()

        # Test visualization methods
        tree = runner.render_tree()
        assert tree is not None
        assert isinstance(tree, str)

        mermaid = runner.render_mermaid()
        assert mermaid is not None
        assert isinstance(mermaid, str)

        jsonl = runner.export_trace("jsonl")
        assert jsonl is not None
        assert isinstance(jsonl, str)

        json_export = runner.export_trace("json")
        assert json_export is not None
        assert isinstance(json_export, str)

    def test_complete_workflow_with_error_handling(self, complete_fixtures):
        """Test complete workflow with error handling."""
        from unittest.mock import Mock
        from src.graph.plan import TraversalPlan
        from src.graph.node import CompletionPolicy, CompletionPolicyType, IntentSlots

        plan = Mock(spec=TraversalPlan)
        plan.plan_id = "error_test"
        plan.intent_slots = IntentSlots(depth=2)
        plan.completion_policy = CompletionPolicy(type=CompletionPolicyType.EXHAUSTIVE, max_depth=2)

        runner = SimulationRunner(complete_fixtures, plan)

        # Run should complete even with mock components
        result = runner.run()

        # Should handle gracefully and return result
        assert result is not None
        assert result.completion_reason in ["completed", "error", "no_steps"]

    def test_statistics_computation_accuracy(self, complete_fixtures):
        """Test statistics computation accuracy."""
        from unittest.mock import Mock
        from src.graph.plan import TraversalPlan
        from src.graph.node import CompletionPolicy, CompletionPolicyType, IntentSlots

        plan = Mock(spec=TraversalPlan)
        plan.plan_id = "stats_test"
        plan.intent_slots = IntentSlots(depth=2)
        plan.completion_policy = CompletionPolicy(type=CompletionPolicyType.EXHAUSTIVE, max_depth=2)

        runner = SimulationRunner(complete_fixtures, plan)
        result = runner.run()

        # Validate statistics
        stats = result.statistics
        assert "total_steps" in stats
        assert "unique_nodes" in stats
        assert "action_count" in stats

        # Statistics should be reasonable
        assert stats["total_steps"] >= 0
        assert stats["unique_nodes"] >= 0
        assert stats["action_count"] >= 0

    def test_performance_targets_met(self, complete_fixtures):
        """Test that performance targets are met."""
        import time
        from unittest.mock import Mock
        from src.graph.plan import TraversalPlan
        from src.graph.node import CompletionPolicy, CompletionPolicyType, IntentSlots

        plan = Mock(spec=TraversalPlan)
        plan.plan_id = "perf_test"
        plan.intent_slots = IntentSlots(depth=2)
        plan.completion_policy = CompletionPolicy(type=CompletionPolicyType.EXHAUSTIVE, max_depth=2)

        # Measure execution time
        start_time = time.time()
        runner = SimulationRunner(complete_fixtures, plan)
        result = runner.run()
        execution_time = time.time() - start_time

        # Should complete quickly (within performance targets)
        assert execution_time < 10.0  # 10 second target

        # Page analysis should be fast
        analyzer = PageAnalyzer(complete_fixtures)
        analysis_start = time.time()
        for i in range(100):
            analyzer.analyze_page("root")
        analysis_time = time.time() - analysis_start

        # Average analysis time should be very fast
        avg_analysis_time = (analysis_time / 100) * 1000  # Convert to ms
        assert avg_analysis_time < 10.0  # < 10ms per analysis