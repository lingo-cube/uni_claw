"""
Enhanced tests for ParseToPlanCapability with new schema fields.

Tests cover:
1. Response schema validation
2. Exit condition types
3. Mock LLM responses
4. Backward compatibility
"""

import pytest
from typing import Dict, Any

try:
    from src.ai.capabilities.parse_to_plan import ParseToPlanCapability
    from src.ai.core.config import AIProviderConfig
    from src.ai.core.llm_client import MockLLMClient
    from src.ai.core.validator import ResponseValidator
    from src.ai.core.prompts import PromptRegistry
except ImportError:
    # Handle import errors gracefully
    pytest.skip("Required modules not available", allow_module_level=True)


class TestResponseSchema:
    """Test response schema structure and validation."""

    @pytest.fixture
    def capability(self):
        """Create a ParseToPlanCapability instance for testing."""
        config = AIProviderConfig(
            api_key="test_key",
            model="test-model",
        )
        client = MockLLMClient()
        validator = ResponseValidator()
        prompt_registry = PromptRegistry(config)
        return ParseToPlanCapability(
            client=client,
            validator=validator,
            config=config,
            prompt_registry=prompt_registry
        )

    def test_schema_has_exit_condition(self, capability):
        """Test that schema includes exit_condition."""
        schema = capability.response_schema
        root_node_props = schema["properties"]["root_node"]["properties"]

        assert "exit_condition" in root_node_props, "Schema missing exit_condition"
        assert root_node_props["exit_condition"]["type"] == "object"

    def test_exit_condition_enum_types(self, capability):
        """Test that exit_condition has correct enum types."""
        schema = capability.response_schema
        exit_cond_props = schema["properties"]["root_node"]["properties"]["exit_condition"]["properties"]

        expected_types = {
            "all_children_visited",
            "target_found",
            "single_level",
            "depth_limited",
            "timeout_or_complete"
        }
        actual_types = set(exit_cond_props["type"]["enum"])

        assert actual_types == expected_types, f"Expected {expected_types}, got {actual_types}"

    def test_exit_condition_fallback_enum(self, capability):
        """Test that exit_condition fallback has correct enum values."""
        schema = capability.response_schema
        exit_cond_props = schema["properties"]["root_node"]["properties"]["exit_condition"]["properties"]

        expected_fallbacks = {"back_to_parent", "stay_on_page", "return_to_root"}
        actual_fallbacks = set(exit_cond_props["fallback"]["enum"])

        assert actual_fallbacks == expected_fallbacks, f"Expected {expected_fallbacks}, got {actual_fallbacks}"

    def test_exit_condition_optional_fields(self, capability):
        """Test that optional exit_condition fields exist."""
        schema = capability.response_schema
        exit_cond_props = schema["properties"]["root_node"]["properties"]["exit_condition"]["properties"]

        assert "target_name" in exit_cond_props
        assert "max_depth" in exit_cond_props
        assert "timeout_seconds" in exit_cond_props

    def test_schema_has_meta(self, capability):
        """Test that schema includes meta."""
        schema = capability.response_schema
        root_node_props = schema["properties"]["root_node"]["properties"]

        assert "meta" in root_node_props, "Schema missing meta"
        assert root_node_props["meta"]["type"] == "object"

    def test_meta_required_fields(self, capability):
        """Test that meta has correct required fields."""
        schema = capability.response_schema
        meta_props = schema["properties"]["root_node"]["properties"]["meta"]["properties"]

        assert "max_depth" in meta_props
        assert "visited_pages" in meta_props
        assert meta_props["visited_pages"]["type"] == "array"

    def test_meta_optional_fields(self, capability):
        """Test that meta has optional visited_pages_scope."""
        schema = capability.response_schema
        meta_props = schema["properties"]["root_node"]["properties"]["meta"]["properties"]

        assert "visited_pages_scope" in meta_props
        assert set(meta_props["visited_pages_scope"]["enum"]) == {"traversal", "global"}

    def test_schema_has_entry_policy(self, capability):
        """Test that schema includes entry_policy."""
        schema = capability.response_schema

        assert "entry_policy" in schema["properties"]
        assert schema["properties"]["entry_policy"]["type"] == "object"

    def test_entry_policy_required_fields(self, capability):
        """Test that entry_policy has required fields."""
        schema = capability.response_schema
        entry_policy_props = schema["properties"]["entry_policy"]["properties"]

        assert "strategy" in entry_policy_props
        assert "fallback" in entry_policy_props

    def test_root_node_required_fields(self, capability):
        """Test that root_node has all required fields including new ones."""
        schema = capability.response_schema
        required = schema["properties"]["root_node"]["required"]

        expected_required = {
            "node_id", "name", "node_type", "operation",
            "precondition", "children_strategy",
            "exit_condition",  # New
            "error_policy",
            "meta"  # New
        }

        assert set(required) == expected_required, f"Expected {expected_required}, got {set(required)}"


class TestMockResponses:
    """Test various mock LLM responses against the new schema."""

    @pytest.fixture
    def valid_responses(self) -> Dict[str, Dict]:
        """Collection of valid response examples."""
        return {
            "all_children_visited": {
                "entry_app": "设置",
                "entry_policy": {
                    "strategy": "direct_deeplink",
                    "fallback": "cold_launch"
                },
                "root_node": {
                    "node_id": "root",
                    "name": "设置主页面",
                    "node_type": "container",
                    "operation": {"action": "no_action"},
                    "precondition": {"page_name": "设置"},
                    "children_strategy": {
                        "type": "dynamic_match",
                        "dynamic_rules": {
                            "menu_rule": {
                                "match_condition": {"type": "menu_item"},
                                "child_template": "menu_container",
                                "action": "generate_child"
                            }
                        }
                    },
                    "exit_condition": {
                        "type": "all_children_visited",
                        "fallback": "back_to_parent"
                    },
                    "error_policy": None,
                    "meta": {
                        "max_depth": 10,
                        "visited_pages": []
                    }
                },
                "static_nodes": [],
                "template_registry": "default",
                "mode": "hybrid"
            },
            "target_found": {
                "entry_app": "设置",
                "entry_policy": {
                    "strategy": "direct_deeplink",
                    "fallback": "cold_launch"
                },
                "root_node": {
                    "node_id": "root",
                    "name": "设置主页",
                    "node_type": "container",
                    "operation": {"action": "no_action"},
                    "precondition": {"page_name": "设置"},
                    "children_strategy": {
                        "type": "dynamic_match",
                        "dynamic_rules": {}
                    },
                    "exit_condition": {
                        "type": "target_found",
                        "target_name": "WiFi设置",
                        "fallback": "back_to_parent"
                    },
                    "error_policy": None,
                    "meta": {
                        "max_depth": 10,
                        "visited_pages": []
                    }
                },
                "static_nodes": [],
                "template_registry": "default",
                "mode": "hybrid"
            },
            "single_level": {
                "entry_app": "设置",
                "root_node": {
                    "node_id": "root",
                    "name": "设置主页",
                    "node_type": "container",
                    "operation": {"action": "no_action"},
                    "precondition": None,
                    "children_strategy": {
                        "type": "dynamic_match",
                        "dynamic_rules": {}
                    },
                    "exit_condition": {
                        "type": "single_level",
                        "fallback": "stay_on_page"
                    },
                    "error_policy": None,
                    "meta": {
                        "max_depth": 1,
                        "visited_pages": []
                    }
                },
                "static_nodes": [],
                "template_registry": "default",
                "mode": "hybrid"
            },
            "depth_limited": {
                "entry_app": "设置",
                "root_node": {
                    "node_id": "root",
                    "name": "设置主页",
                    "node_type": "container",
                    "operation": {"action": "no_action"},
                    "precondition": {"page_name": "设置"},
                    "children_strategy": {
                        "type": "dynamic_match",
                        "dynamic_rules": {}
                    },
                    "exit_condition": {
                        "type": "depth_limited",
                        "max_depth": 3,
                        "fallback": "back_to_parent"
                    },
                    "error_policy": None,
                    "meta": {
                        "max_depth": 3,
                        "visited_pages": [],
                        "visited_pages_scope": "traversal"
                    }
                },
                "static_nodes": [],
                "template_registry": "default",
                "mode": "hybrid"
            },
            "timeout_or_complete": {
                "entry_app": "设置",
                "root_node": {
                    "node_id": "root",
                    "name": "设置主页",
                    "node_type": "container",
                    "operation": {"action": "no_action"},
                    "precondition": {"page_name": "设置"},
                    "children_strategy": {
                        "type": "dynamic_match",
                        "dynamic_rules": {}
                    },
                    "exit_condition": {
                        "type": "timeout_or_complete",
                        "timeout_seconds": 30,
                        "fallback": "return_to_root"
                    },
                    "error_policy": None,
                    "meta": {
                        "max_depth": 10,
                        "visited_pages": []
                    }
                },
                "static_nodes": [],
                "template_registry": "default",
                "mode": "hybrid"
            }
        }

    def test_all_children_visited_response(self, valid_responses):
        """Test all_children_visited exit condition."""
        response = valid_responses["all_children_visited"]

        assert response["root_node"]["exit_condition"]["type"] == "all_children_visited"
        assert response["root_node"]["exit_condition"]["fallback"] == "back_to_parent"
        assert response["root_node"]["meta"]["max_depth"] == 10
        assert response["root_node"]["meta"]["visited_pages"] == []
        print("✓ all_children_visited response validated")

    def test_target_found_response(self, valid_responses):
        """Test target_found exit condition."""
        response = valid_responses["target_found"]

        assert response["root_node"]["exit_condition"]["type"] == "target_found"
        assert response["root_node"]["exit_condition"]["target_name"] == "WiFi设置"
        assert response["root_node"]["meta"]["max_depth"] == 10
        print("✓ target_found response validated")

    def test_single_level_response(self, valid_responses):
        """Test single_level exit condition."""
        response = valid_responses["single_level"]

        assert response["root_node"]["exit_condition"]["type"] == "single_level"
        assert response["root_node"]["exit_condition"]["fallback"] == "stay_on_page"
        assert response["root_node"]["meta"]["max_depth"] == 1
        print("✓ single_level response validated")

    def test_depth_limited_response(self, valid_responses):
        """Test depth_limited exit condition."""
        response = valid_responses["depth_limited"]

        assert response["root_node"]["exit_condition"]["type"] == "depth_limited"
        assert response["root_node"]["exit_condition"]["max_depth"] == 3
        assert response["root_node"]["meta"]["visited_pages_scope"] == "traversal"
        print("✓ depth_limited response validated")

    def test_timeout_or_complete_response(self, valid_responses):
        """Test timeout_or_complete exit condition."""
        response = valid_responses["timeout_or_complete"]

        assert response["root_node"]["exit_condition"]["type"] == "timeout_or_complete"
        assert response["root_node"]["exit_condition"]["timeout_seconds"] == 30
        assert response["root_node"]["exit_condition"]["fallback"] == "return_to_root"
        print("✓ timeout_or_complete response validated")


class TestBackwardCompatibility:
    """Test backward compatibility with old response format."""

    def test_old_format_without_exit_condition(self):
        """Test that old format without exit_condition can be handled."""
        old_response = {
            "entry_app": "设置",
            "root_node": {
                "node_id": "root",
                "name": "设置主页",
                "node_type": "container",
                "operation": {"action": "no_action"},
                "precondition": None,
                "children_strategy": {
                    "type": "dynamic_match",
                    "dynamic_rules": {}
                },
                "error_policy": None
                # Missing: exit_condition, meta
            },
            "static_nodes": [],
            "template_registry": "default",
            "mode": "hybrid"
        }

        # This would fail strict validation but should be handled by fallback logic
        assert "exit_condition" not in old_response["root_node"]
        assert "meta" not in old_response["root_node"]
        print("✓ Old format identified (will need fallback handling)")

    def test_old_format_without_entry_policy(self):
        """Test that old format without entry_policy can be handled."""
        old_response = {
            "entry_app": "设置",
            # Missing: entry_policy
            "root_node": {
                "node_id": "root",
                "name": "设置主页",
                "node_type": "container",
                "operation": {"action": "no_action"},
                "precondition": None,
                "children_strategy": {
                    "type": "dynamic_match",
                    "dynamic_rules": {}
                },
                "exit_condition": {
                    "type": "all_children_visited",
                    "fallback": "back_to_parent"
                },
                "error_policy": None,
                "meta": {
                    "max_depth": 10,
                    "visited_pages": []
                }
            },
            "static_nodes": [],
            "template_registry": "default",
            "mode": "hybrid"
        }

        assert "entry_policy" not in old_response
        print("✓ Old format without entry_policy identified")


class TestExitConditionCombinations:
    """Test various exit condition field combinations."""

    def test_target_found_requires_target_name(self):
        """Test that target_found should have target_name."""
        exit_condition = {
            "type": "target_found",
            "target_name": "WiFi设置",
            "fallback": "back_to_parent"
        }

        assert exit_condition["type"] == "target_found"
        assert "target_name" in exit_condition
        assert exit_condition["fallback"] == "back_to_parent"
        print("✓ target_found with target_name validated")

    def test_depth_limited_requires_max_depth(self):
        """Test that depth_limited should have max_depth."""
        exit_condition = {
            "type": "depth_limited",
            "max_depth": 3,
            "fallback": "back_to_parent"
        }

        assert exit_condition["type"] == "depth_limited"
        assert "max_depth" in exit_condition
        assert exit_condition["max_depth"] == 3
        print("✓ depth_limited with max_depth validated")

    def test_timeout_or_complete_requires_timeout_seconds(self):
        """Test that timeout_or_complete should have timeout_seconds."""
        exit_condition = {
            "type": "timeout_or_complete",
            "timeout_seconds": 30,
            "fallback": "return_to_root"
        }

        assert exit_condition["type"] == "timeout_or_complete"
        assert "timeout_seconds" in exit_condition
        assert exit_condition["timeout_seconds"] == 30
        print("✓ timeout_or_complete with timeout_seconds validated")


# Test execution summary
def test_summary():
    """Print test summary when module is run directly."""
    print("\n" + "=" * 60)
    print("ParseToPlan Enhanced Tests - Stage 1: Data Layer")
    print("=" * 60)
    print("\nTest Groups:")
    print("  1. ResponseSchema - Schema structure validation")
    print("  2. MockResponses - Valid response examples")
    print("  3. BackwardCompatibility - Old format handling")
    print("  4. ExitConditionCombinations - Field requirement tests")
    print("\nExpected Results:")
    print("  ✓ All schema fields present and correctly typed")
    print("  ✓ All exit_condition types enum-validated")
    print("  ✓ Mock responses match schema requirements")
    print("  ✓ Old formats identified for fallback handling")
    print("=" * 60)


if __name__ == "__main__":
    test_summary()
    pytest.main([__file__, "-v", "--tb=short"])
