#!/usr/bin/env python3
"""Verify backward compatibility of SimulationState and TraversalState alias.

This script checks that:
1. TraversalState alias points to SimulationState
2. Both can be used to create instances
3. All fields are accessible
4. JSON serialization uses aliases correctly
"""

import json
import sys


def verify_simulation_state_alias():
    """Verify SimulationState can be accessed via TraversalState alias."""
    from src.models import SimulationState, TraversalState

    # Verify alias points to the same class
    assert SimulationState is TraversalState, \
        "TraversalState should be an alias for SimulationState"

    # Verify both can instantiate
    state1 = SimulationState()
    state2 = TraversalState()

    assert type(state1) == type(state2), \
        "Both should create the same type"

    # Verify basic fields
    assert state1.current_path == []
    assert state1.visited == set()
    assert state1.current_phase == "initialized"

    print("✓ SimulationState alias verification passed")

    # Verify alias fields work correctly
    try:
        state = SimulationState(
            current_path=["test"],
            exception_history_records=[{"type": "test"}],
            node_stack=[{"node": "test"}]
        )
        print("  ✓ State created with alias fields")
    except Exception as e:
        print(f"  ❌ Failed to create state with alias fields: {e}")
        raise

    # Verify alias fields are accessible via their public names
    assert state.exception_history_records == [{"type": "test"}], \
        f"exception_history_records mismatch: {state.exception_history_records}"
    print("  ✓ exception_history_records accessible")
    assert state.node_stack == [{"node": "test"}], \
        f"node_stack mismatch: {state.node_stack}"
    print("  ✓ node_stack accessible")

    # Verify JSON serialization uses aliases
    state_dict = json.loads(state.model_dump_json(by_alias=True))
    assert "_exception_history_records" in state_dict, \
        f"_exception_history_records not in JSON: {state_dict.keys()}"
    print("  ✓ JSON serialization uses _exception_history_records alias")
    assert "_node_stack" in state_dict, \
        f"_node_stack not in JSON: {state_dict.keys()}"
    print("  ✓ JSON serialization uses _node_stack alias")

    print("✓ Alias field serialization verification passed")

    return True


def main():
    """Run all backward compatibility verifications."""
    print("=== Backward Compatibility Verification ===\n")

    checks = [
        ("SimulationState Alias", verify_simulation_state_alias),
    ]

    for name, check in checks:
        print(f"[{name}]")
        try:
            check()
        except AssertionError as e:
            print(f"❌ Verification failed: {e}")
            return 1
        except Exception as e:
            print(f"❌ Unexpected error: {e}")
            import traceback
            traceback.print_exc()
            return 1

    print("\n✓ All backward compatibility verifications passed!")
    return 0


if __name__ == "__main__":
    sys.exit(main())
