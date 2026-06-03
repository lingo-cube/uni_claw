"""
Integration tests for SimulationRunner.

Tests complete simulation execution, result extraction,
context integration, and visualization methods.
"""

import pytest
import json
from unittest.mock import Mock, MagicMock
from src.simulation.runner import SimulationRunner, SimulationResult, StructuredResult
from src.graph.plan import TraversalPlan
from src.graph.node import CompletionPolicy, CompletionPolicyType, IntentSlots


class TestSimulationRunner:
    """Test suite for SimulationRunner integration."""

    @pytest.fixture
    def sample_virtual_pages(self):
        """Create sample virtual pages for testing."""
        return {
            "root": {
                "page_name": "HomeScreen",
                "elements": [
                    {
                        "id": "settings_btn",
                        "type": "button",
                        "text": "Settings",
                        "clickable": True
                    }
                ]
            },
            "settings": {
                "page_name": "SettingsPage",
                "elements": [
                    {
                        "id": "display_option",
                        "type": "menu_item",
                        "text": "Display",
                        "clickable": True
                    }
                ]
            }
        }

    @pytest.fixture
    def sample_plan(self):
        """Create sample traversal plan."""
        # Create a minimal plan for testing
        plan = Mock(spec=TraversalPlan)
        plan.plan_id = "test_plan"
        plan.intent_slots = IntentSlots(depth=2)
        plan.completion_policy = CompletionPolicy(
            type=CompletionPolicyType.EXHAUSTIVE,
            max_depth=2
        )
        return plan

    @pytest.fixture
    def runner(self, sample_virtual_pages, sample_plan):
        """Create SimulationRunner instance."""
        return SimulationRunner(sample_virtual_pages, sample_plan)

    def test_initialization(self, sample_virtual_pages, sample_plan):
        """Test SimulationRunner initialization."""
        runner = SimulationRunner(sample_virtual_pages, sample_plan)

        assert runner.virtual_pages == sample_virtual_pages
        assert runner.plan == sample_plan
        assert runner.vision is not None
        assert runner.action is not None
        assert runner.tracer is not None

    def test_initialization_with_config(self, sample_virtual_pages, sample_plan):
        """Test initialization with configuration."""
        config = {
            "action_delay": 0.1,
            "template_registry": Mock(),
            "exception_chain": Mock()
        }
        runner = SimulationRunner(sample_virtual_pages, sample_plan, config)

        assert runner.config == config
        assert runner.action.simulate_delay == 0.1

    def test_context_integration_setup(self, runner):
        """Test that context integration is properly setup."""
        # Check that vision and action have context set
        assert runner.vision._current_context is not None
        assert runner.action._operation_context is not None

    def test_run_simulation_success(self, runner):
        """Test successful simulation execution."""
        result = runner.run()

        assert isinstance(result, SimulationResult)
        assert result.elapsed_seconds >= 0
        assert isinstance(result.trace, list)
        assert isinstance(result.executed_actions, list)
        assert isinstance(result.visited_tree, dict)

    def test_run_simulation_result_structure(self, runner):
        """Test that simulation result has correct structure."""
        result = runner.run()

        # Check all required fields
        assert hasattr(result, 'engine_result')
        assert hasattr(result, 'trace')
        assert hasattr(result, 'executed_actions')
        assert hasattr(result, 'visited_tree')
        assert hasattr(result, 'elapsed_seconds')
        assert hasattr(result, 'completion_reason')
        assert hasattr(result, 'statistics')

    def test_trace_extraction(self, runner):
        """Test trace extraction from simulation."""
        result = runner.run()

        # Trace should be a list of step dictionaries
        assert isinstance(result.trace, list)
        # Each step should have basic structure
        for step in result.trace:
            assert isinstance(step, dict)
            assert "step_number" in step or "action_type" in step

    def test_executed_actions_extraction(self, runner):
        """Test executed actions extraction."""
        result = runner.run()

        # Actions should match what was recorded
        assert isinstance(result.executed_actions, list)
        # Should have the structure of OperationRecord
        for action in result.executed_actions:
            assert "action_type" in action
            assert "timestamp" in action
            assert "result" in action

    def test_visited_tree_extraction(self, runner):
        """Test visited tree extraction."""
        result = runner.run()

        assert isinstance(result.visited_tree, dict)
        # Each node should have visit information
        for node_id, node_info in result.visited_tree.items():
            assert isinstance(node_id, str)
            assert isinstance(node_info, dict)

    def test_statistics_computation(self, runner):
        """Test statistics computation."""
        result = runner.run()

        assert "total_steps" in result.statistics
        assert "unique_nodes" in result.statistics
        assert "action_count" in result.statistics
        assert result.statistics["total_steps"] >= 0
        assert result.statistics["unique_nodes"] >= 0
        assert result.statistics["action_count"] >= 0

    def test_completion_reason_extraction(self, runner):
        """Test completion reason extraction."""
        result = runner.run()

        assert isinstance(result.completion_reason, str)
        assert len(result.completion_reason) > 0

    def test_render_tree(self, runner):
        """Test ASCII tree rendering."""
        runner.run()
        tree_output = runner.render_tree()

        assert isinstance(tree_output, str)
        assert len(tree_output) > 0

    def test_render_mermaid(self, runner):
        """Test Mermaid diagram rendering."""
        runner.run()
        mermaid_output = runner.render_mermaid()

        assert isinstance(mermaid_output, str)
        assert len(mermaid_output) > 0
        # Mermaid format should contain expected keywords
        assert "graph" in mermaid_output.lower() or "flowchart" in mermaid_output.lower()

    def test_export_trace_jsonl(self, runner):
        """Test trace export in JSONL format."""
        runner.run()
        jsonl_output = runner.export_trace("jsonl")

        assert isinstance(jsonl_output, str)
        lines = jsonl_output.strip().split('\n')
        # Each line should be valid JSON
        for line in lines:
            if line.strip():
                json.loads(line)

    def test_export_trace_json(self, runner):
        """Test trace export in JSON format."""
        runner.run()
        json_output = runner.export_trace("json")

        assert isinstance(json_output, str)
        # Should be valid JSON
        data = json.loads(json_output)
        assert isinstance(data, list)

    def test_export_trace_html(self, runner):
        """Test trace export in HTML format."""
        runner.run()
        html_output = runner.export_trace("html")

        assert isinstance(html_output, str)
        assert len(html_output) > 0
        # Should contain HTML markers
        assert "<html" in html_output.lower() or "<div" in html_output.lower()

    def test_export_trace_unsupported_format(self, runner):
        """Test export trace with unsupported format."""
        runner.run()
        with pytest.raises(ValueError, match="Unsupported format"):
            runner.export_trace("xml")

    def test_get_statistics(self, runner):
        """Test getting statistics."""
        runner.run()
        stats = runner.get_statistics()

        assert isinstance(stats, dict)
        assert "total_steps" in stats
        assert "visited_nodes" in stats
        assert "action_count" in stats

    def test_get_result(self, runner):
        """Test getting stored result."""
        result1 = runner.run()
        result2 = runner.get_result()

        assert result2 is result1

    def test_error_handling_in_run(self, sample_virtual_pages, sample_plan):
        """Test error handling during simulation."""
        # Create a runner that will fail
        runner = SimulationRunner(sample_virtual_pages, sample_plan)

        # Mock the tracer to raise an exception
        def mock_start(*args, **kwargs):
            raise RuntimeError("Test error")

        runner.tracer.start_traversal = mock_start

        # Should handle error gracefully
        result = runner.run()
        assert result.completion_reason == "error"
        assert "error" in result.statistics

    def test_context_persistence_across_run(self, runner):
        """Test that context persists across simulation run."""
        # Set initial context
        runner.vision.inject_path("settings")
        runner.action.set_context(Mock(current_node="settings"))

        # Run simulation
        result = runner.run()

        # Context should be preserved in recorded actions
        if result.executed_actions:
            # At least one action should have context
            assert any("current_node" in action for action in result.executed_actions)

    def test_elapsed_time_tracking(self, runner):
        """Test elapsed time tracking."""
        import time
        start_time = time.time()
        result = runner.run()
        end_time = time.time()

        # Elapsed time should be reasonable
        assert result.elapsed_seconds >= 0
        assert result.elapsed_seconds < (end_time - start_time + 0.1)  # Allow some margin

    def test_reset_between_runs(self, sample_virtual_pages, sample_plan):
        """Test that runner can be reset between runs."""
        runner = SimulationRunner(sample_virtual_pages, sample_plan)

        # First run
        result1 = runner.run()
        count1 = runner.action.get_operation_count()

        # Reset and run again
        runner.action.reset()
        runner.tracer = Mock()  # Reset tracer
        runner.tracer.steps = []
        runner.tracer.start_traversal = Mock()
        runner.tracer.render_tree = Mock(return_value="tree")
        runner.tracer.render_mermaid = Mock(return_value="mermaid")
        runner.tracer.export_trace = Mock(return_value="trace")
        runner.tracer.visited_tree = {}

        # Setup for second run
        runner._setup_context_integration()

        result2 = runner.run()
        count2 = runner.action.get_operation_count()

        # Results should be independent
        assert result1.elapsed_seconds >= 0
        assert result2.elapsed_seconds >= 0

    def test_multiple_runs(self, runner):
        """Test multiple sequential runs."""
        results = []
        for i in range(3):
            runner.action.reset()
            runner.tracer.steps = []
            runner.tracer.visited_tree = {}
            runner.tracer.start_traversal = Mock()
            result = runner.run()
            results.append(result)

        # All runs should complete successfully
        assert all(r.elapsed_seconds >= 0 for r in results)
        assert all(r.completion_reason for r in results)

    def test_config_parameter_passing(self, sample_virtual_pages, sample_plan):
        """Test that config parameters are properly passed to components."""
        config = {
            "action_delay": 0.05,
            "template_registry": Mock(),
            "exception_chain": Mock()
        }

        runner = SimulationRunner(sample_virtual_pages, sample_plan, config)

        assert runner.action.simulate_delay == 0.05
        assert runner.config == config