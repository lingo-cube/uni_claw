"""Unit tests for PromptManager and PromptTemplate."""

import pytest
import tempfile
import shutil
from pathlib import Path

from src.ai.prompts.manager import PromptManager, PromptTemplate
from src.ai.prompts.validator import PromptValidator, validate_prompt_file


class TestPromptTemplate:
    """Test PromptTemplate dataclass."""

    def test_template_creation(self):
        """Test creating a prompt template."""
        template = PromptTemplate(
            capability="test",
            version="latest",
            system_prompt="You are a helpful assistant.",
            user_template="Analyze: {input}",
            variables=["input"],
        )

        assert template.capability == "test"
        assert template.version == "latest"
        assert template.system_prompt == "You are a helpful assistant."
        assert template.user_template == "Analyze: {input}"
        assert template.variables == ["input"]

    def test_format_with_variables(self):
        """Test formatting a template with variables."""
        template = PromptTemplate(
            capability="test",
            version="latest",
            system_prompt="System instructions",
            user_template="Process: {input} with {param}",
            variables=["input", "param"],
        )

        result = template.format(input="test data", param="value")

        assert "Process: test data with value" in result
        assert "System instructions" in result

    def test_format_missing_variable_raises_error(self):
        """Test that missing variables raise an error."""
        template = PromptTemplate(
            capability="test",
            version="latest",
            system_prompt="",
            user_template="Process: {input}",
            variables=["input"],
        )

        with pytest.raises(ValueError, match="Missing required variables"):
            template.format(wrong_var="value")

    def test_format_without_system_prompt(self):
        """Test formatting without a system prompt."""
        template = PromptTemplate(
            capability="test",
            version="latest",
            system_prompt="",
            user_template="Just user: {input}",
            variables=["input"],
        )

        result = template.format(input="test")

        assert result == "Just user: test"

    def test_to_dict(self):
        """Test serialization to dictionary."""
        template = PromptTemplate(
            capability="test",
            version="latest",
            system_prompt="System",
            user_template="User: {input}",
            variables=["input"],
            metadata={"key": "value"},
        )

        data = template.to_dict()

        assert data["capability"] == "test"
        assert data["version"] == "latest"
        assert data["system_prompt"] == "System"
        assert data["variables"] == ["input"]
        assert data["metadata"] == {"key": "value"}

    def test_from_dict(self):
        """Test deserialization from dictionary."""
        data = {
            "capability": "test",
            "version": "latest",
            "system_prompt": "System",
            "user_template": "User: {input}",
            "variables": ["input"],
            "metadata": {},
        }

        template = PromptTemplate.from_dict(data)

        assert template.capability == "test"
        assert template.variables == ["input"]


class TestPromptManager:
    """Test PromptManager class."""

    @pytest.fixture
    def temp_prompt_dir(self):
        """Create a temporary directory for prompt files."""
        temp_dir = tempfile.mkdtemp()
        yield temp_dir
        shutil.rmtree(temp_dir)

    @pytest.fixture
    def sample_prompt_file(self, temp_prompt_dir):
        """Create a sample prompt file."""
        content = """---
capability: test_capability
version: 1.0
variables:
  - input
  - context
system: |
  You are a test assistant.
---
Process this: {input}
Context: {context}
"""
        path = Path(temp_prompt_dir) / "test_capability.md"
        path.write_text(content, encoding="utf-8")
        return path

    @pytest.fixture
    def manager(self, temp_prompt_dir, sample_prompt_file):
        """Create a PromptManager with a sample prompt."""
        return PromptManager(prompt_dir=temp_prompt_dir)

    def test_manager_initialization(self, manager):
        """Test manager initializes correctly."""
        assert len(manager.list_capabilities()) == 1
        assert "test_capability" in manager.list_capabilities()

    def test_get_prompt(self, manager):
        """Test getting a prompt template."""
        template = manager.get_prompt("test_capability")

        assert template.capability == "test_capability"
        assert template.version == "latest"
        assert template.variables == ["input", "context"]

    def test_get_prompt_missing_capability(self, manager):
        """Test getting a non-existent capability raises error."""
        with pytest.raises(ValueError, match="Prompt not found"):
            manager.get_prompt("missing_capability")

    def test_inject_variables(self, manager):
        """Test injecting variables into a template."""
        template = manager.get_prompt("test_capability")
        result = manager.inject_variables(
            template, input="test data", context="test context"
        )

        assert "test data" in result
        assert "test context" in result

    def test_list_capabilities(self, manager):
        """Test listing capabilities."""
        capabilities = manager.list_capabilities()

        assert isinstance(capabilities, list)
        assert "test_capability" in capabilities

    def test_list_versions(self, manager):
        """Test listing versions."""
        versions = manager.list_versions("test_capability")

        assert "latest" in versions

    def test_list_versions_missing_capability(self, manager):
        """Test listing versions for non-existent capability."""
        versions = manager.list_versions("missing")
        assert versions == []

    def test_reload_prompts(self, temp_prompt_dir):
        """Test reloading prompts."""
        manager = PromptManager(prompt_dir=temp_prompt_dir)
        initial_count = len(manager.list_capabilities())

        # Add a new prompt file
        new_prompt = Path(temp_prompt_dir) / "new_capability.md"
        new_prompt.write_text("---\ncapability: new_capability\nuser: Test")
        new_prompt.write_text("---\ncapability: new_capability\nvariables: []\nuser: Test")

        manager.reload_prompts()

        assert len(manager.list_capabilities()) >= initial_count
        assert "new_capability" in manager.list_capabilities()

    def test_validate_prompt_valid(self, manager):
        """Test validating a valid prompt."""
        assert manager.validate_prompt("test_capability") is True

    def test_validate_prompt_invalid(self, manager):
        """Test validating an invalid prompt."""
        assert manager.validate_prompt("missing_capability") is False

    def test_get_all_metadata(self, manager):
        """Test getting all metadata."""
        metadata = manager.get_all_metadata()

        assert "test_capability" in metadata
        assert isinstance(metadata["test_capability"], dict)


class TestPromptValidator:
    """Test PromptValidator class."""

    def test_validate_yaml_valid(self):
        """Test validating valid YAML front matter."""
        content = """---
capability: test
version: 1.0
variables:
  - input
---
User prompt here
"""
        result = PromptValidator.validate_yaml_front_matter(content)

        assert result.is_valid
        assert len(result.errors) == 0

    def test_validate_yaml_missing_delimiter(self):
        """Test validating YAML without delimiter."""
        content = "Just a prompt without front matter"
        result = PromptValidator.validate_yaml_front_matter(content)

        assert not result.is_valid
        assert "Missing YAML front matter" in result.errors[0]

    def test_validate_yaml_missing_capability(self):
        """Test validating YAML without capability field."""
        content = """---
version: 1.0
---
User prompt
"""
        result = PromptValidator.validate_yaml_front_matter(content)

        assert not result.is_valid
        assert "Missing required field: capability" in result.errors[0]

    def test_validate_variables_correct(self):
        """Test validating correct variable declarations."""
        template = PromptTemplate(
            capability="test",
            version="latest",
            system_prompt="",
            user_template="Test {input} and {param}",
            variables=["input", "param"],
        )

        result = PromptValidator.validate_variables(template)

        assert result.is_valid

    def test_validate_variables_undeclared(self):
        """Test detecting undeclared variables."""
        template = PromptTemplate(
            capability="test",
            version="latest",
            system_prompt="",
            user_template="Test {input} and {undeclared}",
            variables=["input"],
        )

        result = PromptValidator.validate_variables(template)

        # Should have warning but still be valid
        assert "undeclared" in str(result.warnings)

    def test_validate_variables_invalid_name(self):
        """Test detecting invalid variable names."""
        template = PromptTemplate(
            capability="test",
            version="latest",
            system_prompt="",
            user_template="Test {123invalid}",
            variables=["123invalid"],
        )

        result = PromptValidator.validate_variables(template)

        assert not result.is_valid
        assert "Invalid variable name" in result.errors[0]

    def test_validate_template_syntax_valid(self):
        """Test validating correct template syntax."""
        template = PromptTemplate(
            capability="test",
            version="latest",
            system_prompt="",
            user_template="Test {valid} template",
            variables=["valid"],
        )

        result = PromptValidator.validate_template_syntax(template)

        assert result.is_valid

    def test_validate_template_syntax_mismatched_braces(self):
        """Test detecting mismatched braces."""
        template = PromptTemplate(
            capability="test",
            version="latest",
            system_prompt="",
            user_template="Test {valid template",
            variables=["valid"],
        )

        result = PromptValidator.validate_template_syntax(template)

        assert not result.is_valid

    def test_validate_capability_name_valid(self):
        """Test validating valid capability names."""
        result = PromptValidator.validate_capability_name("analyze_visual")

        assert result.is_valid

    def test_validate_capability_name_invalid(self):
        """Test validating invalid capability names."""
        result = PromptValidator.validate_capability_name("Invalid-Name")

        assert not result.is_valid
        assert len(result.errors) > 0

    def test_validate_capability_name_empty(self):
        """Test validating empty capability name."""
        result = PromptValidator.validate_capability_name("")

        assert not result.is_valid
        assert "cannot be empty" in result.errors[0]


class TestValidatePromptFile:
    """Test validate_prompt_file function."""

    @pytest.fixture
    def temp_dir(self):
        """Create a temporary directory."""
        temp_dir = tempfile.mkdtemp()
        yield temp_dir
        shutil.rmtree(temp_dir)

    def test_validate_valid_prompt_file(self, temp_dir):
        """Test validating a valid prompt file."""
        content = """---
capability: test_capability
version: 1.0
variables:
  - input
system: Test system
---
Test {input} template
"""
        path = Path(temp_dir) / "test_capability.md"
        path.write_text(content, encoding="utf-8")

        result = validate_prompt_file(str(path))

        assert result.is_valid

    def test_validate_missing_file(self, temp_dir):
        """Test validating a non-existent file."""
        result = validate_prompt_file(str(Path(temp_dir) / "missing.md"))

        assert not result.is_valid
        assert "File not found" in result.errors[0]
