"""Prompt validation utilities.

This module provides validation for prompt templates to ensure:
- YAML front matter is valid
- Variables are properly declared
- Template syntax is correct
"""

import logging
import re
from typing import Dict, List, Any, Optional
from dataclasses import dataclass

from .manager import PromptTemplate

logger = logging.getLogger(__name__)


@dataclass
class ValidationResult:
    """Result of prompt validation.

    Attributes:
        is_valid: Whether validation passed
        errors: List of error messages
        warnings: List of warning messages
    """

    is_valid: bool
    errors: List[str]
    warnings: List[str] = None

    def __post_init__(self):
        if self.warnings is None:
            self.warnings = []


class PromptValidator:
    """Validator for prompt templates."""

    @staticmethod
    def validate_yaml_front_matter(content: str) -> ValidationResult:
        """Validate YAML front matter in a prompt file.

        Args:
            content: File content to validate

        Returns:
            ValidationResult: Validation result with errors/warnings
        """
        errors = []
        warnings = []

        if not content.startswith("---"):
            return ValidationResult(
                is_valid=False,
                errors=["Missing YAML front matter delimiter '---'"]
            )

        # Split by "---" - format is: ---\nYAML\n---\ncontent
        parts = content.split("---")
        if len(parts) < 3:
            errors.append("Invalid YAML front matter format (expected closing ---)")
            return ValidationResult(is_valid=False, errors=errors)

        front_matter = parts[1].strip()

        # Try to parse YAML to validate structure
        try:
            import yaml
            metadata = yaml.safe_load(front_matter) or {}

            # Check for required fields in parsed metadata
            required_fields = ["capability"]
            for field in required_fields:
                if field not in metadata:
                    errors.append(f"Missing required field: {field}")

            # Check for recommended fields
            recommended_fields = ["variables", "version"]
            for field in recommended_fields:
                if field not in metadata:
                    warnings.append(f"Missing recommended field: {field}")

        except yaml.YAMLError as e:
            errors.append(f"Invalid YAML syntax: {e}")

        return ValidationResult(
            is_valid=len(errors) == 0,
            errors=errors,
            warnings=warnings,
        )

    @staticmethod
    def validate_variables(template: PromptTemplate) -> ValidationResult:
        """Validate variable declarations in a template.

        Args:
            template: Prompt template to validate

        Returns:
            ValidationResult: Validation result
        """
        errors = []
        warnings = []

        # Extract variables from template using regex
        template_vars = set(re.findall(r"\{(\w+)\}", template.user_template))
        declared_vars = set(template.variables)

        # Check for undeclared variables
        undeclared = template_vars - declared_vars
        if undeclared:
            warnings.append(f"Variables used but not declared: {undeclared}")

        # Check for declared but unused variables
        unused = declared_vars - template_vars
        if unused:
            warnings.append(f"Variables declared but not used: {unused}")

        # Check that variables are valid Python identifiers
        for var in declared_vars:
            if not re.match(r"^[a-zA-Z_][a-zA-Z0-9_]*$", var):
                errors.append(f"Invalid variable name: {var}")

        return ValidationResult(
            is_valid=len(errors) == 0,
            errors=errors,
            warnings=warnings,
        )

    @staticmethod
    def validate_template_syntax(template: PromptTemplate) -> ValidationResult:
        """Validate template syntax.

        Args:
            template: Prompt template to validate

        Returns:
            ValidationResult: Validation result
        """
        errors = []
        warnings = []

        # Check for malformed placeholders
        # Count opening and closing braces
        open_braces = template.user_template.count("{")
        close_braces = template.user_template.count("}")

        if open_braces != close_braces:
            errors.append(
                f"Mismatched braces: {open_braces} opening, {close_braces} closing"
            )

        # Check for empty placeholders
        empty_vars = re.findall(r"\{\}", template.user_template)
        if empty_vars:
            errors.append(f"Empty placeholders found: {len(empty_vars)}")

        # Check for unclosed placeholders
        unclosed = re.findall(r"\{[^\}]*$", template.user_template, re.MULTILINE)
        if unclosed:
            errors.append(f"Unclosed placeholders: {unclosed}")

        return ValidationResult(
            is_valid=len(errors) == 0,
            errors=errors,
            warnings=warnings,
        )

    @classmethod
    def validate_template(cls, template: PromptTemplate) -> ValidationResult:
        """Perform complete validation on a template.

        Args:
            template: Prompt template to validate

        Returns:
            ValidationResult: Combined validation result
        """
        all_errors = []
        all_warnings = []

        # Validate variables
        var_result = cls.validate_variables(template)
        all_errors.extend(var_result.errors)
        all_warnings.extend(var_result.warnings)

        # Validate syntax
        syntax_result = cls.validate_template_syntax(template)
        all_errors.extend(syntax_result.errors)
        all_warnings.extend(syntax_result.warnings)

        return ValidationResult(
            is_valid=len(all_errors) == 0,
            errors=all_errors,
            warnings=all_warnings,
        )

    @staticmethod
    def validate_capability_name(capability: str) -> ValidationResult:
        """Validate capability name format.

        Args:
            capability: Capability name to validate

        Returns:
            ValidationResult: Validation result
        """
        errors = []

        if not capability:
            errors.append("Capability name cannot be empty")
            return ValidationResult(is_valid=False, errors=errors)

        # Check format (should be snake_case)
        if not re.match(r"^[a-z][a-z0-9_]*$", capability):
            errors.append(
                f"Invalid capability name format: '{capability}'. "
                "Should use snake_case starting with a letter."
            )

        return ValidationResult(
            is_valid=len(errors) == 0,
            errors=errors,
        )


def validate_prompt_file(file_path: str) -> ValidationResult:
    """Validate a complete prompt file.

    Args:
        file_path: Path to the prompt markdown file

    Returns:
        ValidationResult: Combined validation result
    """
    from pathlib import Path
    from .manager import PromptManager

    all_errors = []
    all_warnings = []

    path = Path(file_path)

    if not path.exists():
        return ValidationResult(
            is_valid=False,
            errors=[f"File not found: {file_path}"]
        )

    content = path.read_text(encoding="utf-8")

    # Validate YAML front matter
    yaml_result = PromptValidator.validate_yaml_front_matter(content)
    all_errors.extend(yaml_result.errors)
    all_warnings.extend(yaml_result.warnings)

    # Try to parse the file
    try:
        manager = PromptManager(prompt_dir=str(path.parent))
        capability = path.stem
        template = manager.get_prompt(capability)

        # Validate the template
        template_result = PromptValidator.validate_template(template)
        all_errors.extend(template_result.errors)
        all_warnings.extend(template_result.warnings)

        # Validate capability name
        name_result = PromptValidator.validate_capability_name(capability)
        all_errors.extend(name_result.errors)

    except Exception as e:
        all_errors.append(f"Failed to parse prompt: {e}")

    return ValidationResult(
        is_valid=len(all_errors) == 0,
        errors=all_errors,
        warnings=all_warnings,
    )
