"""Custom assertions for model testing.

This module provides custom assertion functions for testing business models,
with more descriptive error messages than standard pytest assertions.
"""

from typing import Any, Dict, List, Optional
from datetime import datetime


def assert_enum_values(enum_class: type, expected_values: List[str]) -> None:
    """Assert that an enum has exactly the expected values.

    Args:
        enum_class: Enum class to check
        expected_values: List of expected enum values

    Raises:
        AssertionError: If enum values don't match expected
    """
    actual_values = [e.value for e in enum_class]
    if set(actual_values) != set(expected_values):
        raise AssertionError(
            f"Enum {enum_class.__name__} values mismatch.\n"
            f"Expected: {sorted(expected_values)}\n"
            f"Actual: {sorted(actual_values)}"
        )


def assert_enum_has_method(enum_class: type, method_name: str) -> None:
    """Assert that an enum class has a specific class method.

    Args:
        enum_class: Enum class to check
        method_name: Name of the expected method

    Raises:
        AssertionError: If method doesn't exist or isn't a class method
    """
    if not hasattr(enum_class, method_name):
        raise AssertionError(f"Enum {enum_class.__name__} missing method: {method_name}")

    method = getattr(enum_class, method_name)
    if not callable(method):
        raise AssertionError(f"Enum {enum_class.__name__} attribute '{method_name}' is not a method")


def assert_model_fields(model_instance: Any, expected_fields: Dict[str, Any]) -> None:
    """Assert that a model instance has expected field values.

    Args:
        model_instance: Model instance to check
        expected_fields: Dictionary of expected field values

    Raises:
        AssertionError: If any field value doesn't match expected
    """
    for field_name, expected_value in expected_fields.items():
        actual_value = getattr(model_instance, field_name, None)
        if actual_value != expected_value:
            raise AssertionError(
                f"Field '{field_name}' value mismatch.\n"
                f"Expected: {expected_value}\n"
                f"Actual: {actual_value}"
            )


def assert_model_has_field(model_class: type, field_name: str) -> None:
    """Assert that a model class has a specific field.

    Args:
        model_class: Model class to check
        field_name: Name of the expected field

    Raises:
        AssertionError: If field doesn't exist
    """
    if not hasattr(model_class, field_name):
        # Try to get from __dataclass_fields__ for dataclasses
        if hasattr(model_class, "__dataclass_fields__"):
            fields = [f.name for f in model_class.__dataclass_fields__]
            raise AssertionError(
                f"Model {model_class.__name__} missing field: {field_name}. "
                f"Available fields: {fields}"
            )
        raise AssertionError(f"Model {model_class.__name__} missing field: {field_name}")


def assert_validation_error_raised(model_class: type, **kwargs) -> None:
    """Assert that creating a model instance raises a validation error.

    Args:
        model_class: Model class to instantiate
        **kwargs: Arguments to pass to model constructor

    Raises:
        AssertionError: If no error is raised
    """
    try:
        model_class(**kwargs)
        raise AssertionError(
            f"Expected validation error for {model_class.__name__} "
            f"with arguments: {kwargs}"
        )
    except (ValueError, TypeError) as e:
        # Expected error type
            pass
    except Exception as e:
        raise AssertionError(
            f"Expected ValueError/TypeError for {model_class.__name__}, "
            f"but got {type(e).__name__}: {e}"
        )


def assert_serialization_roundtrip(model_instance: Any, serializer_method: str = "to_dict") -> None:
    """Assert that a model can be serialized and deserialized correctly.

    Args:
        model_instance: Model instance to test
        serializer_method: Name of serialization method (default: to_dict)

    Raises:
        AssertionError: If roundtrip serialization fails
    """
    # Serialize
    serialize_func = getattr(model_instance, serializer_method, None)
    if serialize_func is None or not callable(serialize_func):
        raise AssertionError(
            f"Model {type(model_instance).__name__} has no {serializer_method} method"
        )

    serialized = serialize_func()

    # Check if deserializer exists
    if serializer_method == "to_dict":
        deserializer_method = "from_dict"
    elif serializer_method == "to_json":
        deserializer_method = "from_json"
    else:
        raise AssertionError(f"Unknown serializer method: {serializer_method}")

    model_class = type(model_instance)
    if not hasattr(model_class, deserializer_method):
        raise AssertionError(
            f"Model {model_class.__name__} has no {deserializer_method} method"
        )

    deserializer = getattr(model_class, deserializer_method)
    deserialized = deserializer(serialized)

    # Compare field values
    for field_name in dir(model_instance):
        if not field_name.startswith("_"):
            original_value = getattr(model_instance, field_name, None)
            deserialized_value = getattr(deserialized, field_name, None)

            # Skip methods and special attributes
            if callable(original_value) or field_name in ("model_config", "model_fields"):
                continue

            if original_value != deserialized_value:
                raise AssertionError(
                    f"Roundtrip serialization failed for field '{field_name}'.\n"
                    f"Original: {original_value}\n"
                    f"Deserialized: {deserialized_value}"
                )


def assert_frozen_dataclass_immutable(model_instance: Any) -> None:
    """Assert that a frozen dataclass instance is truly immutable.

    Args:
        model_instance: Frozen dataclass instance to test

    Raises:
        AssertionError: If instance can be modified
    """
    import dataclasses

    # Get all dataclass fields
    if not hasattr(model_instance, "__dataclass_fields__"):
        raise AssertionError(f"{type(model_instance).__name__} is not a dataclass")

    fields = model_instance.__dataclass_fields__

    # Try to modify each field
    for field in fields:
        original_value = getattr(model_instance, field.name)
        test_value = None if original_value is not None else "test_value"

        try:
            setattr(model_instance, field.name, test_value)
            # If we get here, the dataclass is not frozen
            raise AssertionError(
                f"Dataclass {type(model_instance).__name__} is not frozen - "
                f"was able to modify field '{field.name}'"
            )
        except (dataclasses.FrozenInstanceError, AttributeError):
            # Expected for frozen dataclass
            pass


def assert_list_elements_unique(items: List[Any], key_func: Optional[callable] = None) -> None:
    """Assert that all elements in a list are unique.

    Args:
        items: List of items to check
        key_func: Optional function to extract comparison key from items

    Raises:
        AssertionError: If duplicates are found
    """
    if key_func:
        keys = [key_func(item) for item in items]
    else:
        keys = items

    duplicates = [item for item in set(keys) if keys.count(item) > 1]
    if duplicates:
        raise AssertionError(f"Found duplicate elements: {duplicates}")


def assert_timestamp_recent(timestamp: datetime, max_seconds: int = 3600) -> None:
    """Assert that a timestamp is recent (within max_seconds).

    Args:
        timestamp: Timestamp to check
        max_seconds: Maximum age in seconds (default: 1 hour)

    Raises:
        AssertionError: If timestamp is too old or in the future
    """
    if not timestamp:
        raise AssertionError("Timestamp is None")

    time_diff = datetime.now() - timestamp
    if time_diff.total_seconds() < 0:
        raise AssertionError("Timestamp is in the future")
    if time_diff.total_seconds() > max_seconds:
        raise AssertionError(
            f"Timestamp is too old: {time_diff.total_seconds()}s "
            f"(max: {max_seconds}s)"
        )
