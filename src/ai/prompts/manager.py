"""Prompt manager for AI capability templates.

This module provides centralized prompt template management with:
- Loading prompts from markdown files
- Variable injection
- Version control
- Hot reload capability
"""

import yaml
import logging
from pathlib import Path
from typing import Dict, List, Optional, Any
from dataclasses import dataclass, field

logger = logging.getLogger(__name__)


@dataclass
class PromptTemplate:
    """Prompt template with variable injection support.

    Attributes:
        capability: Name of the capability this prompt is for
        version: Version identifier (e.g., "latest", "v1", "v2")
        system_prompt: System-level instructions
        user_template: User prompt template with {variable} placeholders
        variables: List of required variable names
        metadata: Additional metadata from front matter
    """

    capability: str
    version: str
    system_prompt: str
    user_template: str
    variables: List[str]
    metadata: Dict[str, Any] = field(default_factory=dict)

    def format(self, **kwargs) -> str:
        """Format the prompt with provided variables.

        Args:
            **kwargs: Variable values to inject

        Returns:
            str: Formatted prompt

        Raises:
            ValueError: If required variables are missing
        """
        missing_vars = set(self.variables) - set(kwargs.keys())
        if missing_vars:
            raise ValueError(
                f"Missing required variables for {self.capability}: {missing_vars}"
            )

        # Replace variables in user template
        user_prompt = self.user_template
        for var_name in self.variables:
            if var_name in kwargs:
                user_prompt = user_prompt.replace(f"{{{var_name}}}", str(kwargs[var_name]))

        # Combine system and user prompts
        if self.system_prompt:
            return f"{self.system_prompt}\n\n{user_prompt}"
        return user_prompt

    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary for serialization."""
        return {
            "capability": self.capability,
            "version": self.version,
            "system_prompt": self.system_prompt,
            "user_template": self.user_template,
            "variables": self.variables,
            "metadata": self.metadata,
        }

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> "PromptTemplate":
        """Create from dictionary."""
        return cls(
            capability=data["capability"],
            version=data["version"],
            system_prompt=data["system_prompt"],
            user_template=data["user_template"],
            variables=data["variables"],
            metadata=data.get("metadata", {}),
        )


class PromptManager:
    """Centralized prompt template management.

    Loads prompts from markdown files with YAML front matter,
    supports variable injection, version control, and hot reload.
    """

    def __init__(self, prompt_dir: str = "src/ai/prompts"):
        """Initialize the prompt manager.

        Args:
            prompt_dir: Directory containing prompt markdown files
        """
        self.prompt_dir = Path(prompt_dir)
        self._prompts: Dict[str, Dict] = {}
        self._load_prompts()
        logger.info(
            f"PromptManager initialized with {len(self._prompts)} capabilities from {self.prompt_dir}"
        )

    def _load_prompts(self) -> None:
        """Load all prompt files from the prompts directory."""
        if not self.prompt_dir.exists():
            logger.warning(f"Prompt directory not found: {self.prompt_dir}")
            return

        for prompt_file in self.prompt_dir.glob("*.md"):
            try:
                capability_name = prompt_file.stem
                self._prompts[capability_name] = self._parse_prompt_file(prompt_file)
                logger.debug(f"Loaded prompt for capability: {capability_name}")
            except Exception as e:
                logger.error(f"Failed to load prompt file {prompt_file}: {e}")

    def _parse_prompt_file(self, file_path: Path) -> Dict[str, Any]:
        """Parse a prompt markdown file with YAML front matter.

        Supports two formats:
        1. ---\nYAML\n---\nuser body (traditional front matter format)
        2. ---\nYAML with system/user keys\n (self-contained format)

        Args:
            file_path: Path to the markdown file

        Returns:
            Dict: Parsed prompt data with metadata, system, user, variables
        """
        content = file_path.read_text(encoding="utf-8")

        metadata = {}
        prompt_body = content

        if content.startswith("---"):
            try:
                # Split by "---" - format is: ---\nYAML\n---\ncontent
                parts = content.split("---")
                if len(parts) >= 3:
                    # Traditional format: ---\nYAML\n---\nuser body
                    # parts[0] is empty (before first ---)
                    # parts[1] is the YAML front matter
                    # parts[2] is the content after closing ---
                    front_matter = parts[1].strip()
                    prompt_body = "---".join(parts[2:]).strip()  # Join remaining parts
                    metadata = yaml.safe_load(front_matter) or {}
                else:
                    # Self-contained format: entire content after --- is YAML
                    # Parse everything after first --- as YAML
                    yaml_content = "---".join(parts[1:]).strip()
                    metadata = yaml.safe_load(yaml_content) or {}
                    # In this format, system and user are in the YAML
                    prompt_body = metadata.get("user", "")
            except yaml.YAMLError as e:
                logger.warning(f"Invalid YAML in {file_path}: {e}, using empty metadata")
                metadata = {}
                prompt_body = content
        else:
            metadata = {}
            prompt_body = content

        # Extract system prompt from metadata (handle both string and dict formats)
        system_prompt = ""
        if "system" in metadata:
            system_value = metadata["system"]
            if isinstance(system_value, str):
                system_prompt = system_value
            elif isinstance(system_value, dict) and "value" in system_value:
                system_prompt = system_value["value"]

        # If user was already extracted from YAML (self-contained format), don't override
        if "user" in metadata and isinstance(metadata["user"], str) and not prompt_body:
            prompt_body = metadata["user"]

        # Ensure variables is a list
        variables = metadata.get("variables", [])
        if not isinstance(variables, list):
            variables = []

        return {
            "metadata": metadata,
            "system": system_prompt,
            "user": prompt_body,
            "variables": variables,
            "versions": metadata.get("versions", {}),
            "file_path": str(file_path),
        }

    def get_prompt(self, capability: str, version: str = "latest") -> PromptTemplate:
        """Get a prompt template for a capability.

        Args:
            capability: Name of the capability
            version: Version identifier (default: "latest")

        Returns:
            PromptTemplate: The requested prompt template

        Raises:
            ValueError: If capability or version not found
        """
        if capability not in self._prompts:
            available = list(self._prompts.keys())
            raise ValueError(
                f"Prompt not found for capability: {capability}. "
                f"Available capabilities: {available}"
            )

        prompt_data = self._prompts[capability]

        # Handle version selection
        if version != "latest":
            versions = prompt_data.get("versions", {})
            if version not in versions:
                available_versions = ["latest"] + list(versions.keys())
                raise ValueError(
                    f"Version {version} not found for capability {capability}. "
                    f"Available versions: {available_versions}"
                )
            # Use version-specific data
            version_data = versions[version]
            return PromptTemplate(
                capability=capability,
                version=version,
                system_prompt=version_data.get("system", prompt_data.get("system", "")),
                user_template=version_data.get("user", prompt_data.get("user", "")),
                variables=version_data.get("variables", prompt_data.get("variables", [])),
                metadata=version_data.get("metadata", {}),
            )

        # Return latest version
        metadata = prompt_data.get("metadata", {})
        return PromptTemplate(
            capability=capability,
            version=version,
            system_prompt=prompt_data.get("system", ""),
            user_template=prompt_data.get("user", ""),
            variables=prompt_data.get("variables", []),
            metadata=metadata,
        )

    def inject_variables(
        self, template: PromptTemplate, **kwargs
    ) -> str:
        """Inject variables into a prompt template.

        This is a convenience method that calls template.format().

        Args:
            template: The prompt template
            **kwargs: Variable values to inject

        Returns:
            str: Formatted prompt

        Raises:
            ValueError: If required variables are missing
        """
        return template.format(**kwargs)

    def list_capabilities(self) -> List[str]:
        """List all available capabilities.

        Returns:
            List[str]: Names of available capabilities
        """
        return list(self._prompts.keys())

    def list_versions(self, capability: str) -> List[str]:
        """List available versions for a capability.

        Args:
            capability: Name of the capability

        Returns:
            List[str]: Available version identifiers
        """
        if capability not in self._prompts:
            return []

        versions = self._prompts[capability].get("versions", {})
        return ["latest"] + list(versions.keys())

    def reload_prompts(self) -> int:
        """Reload all prompts from disk.

        This enables hot-reload capability for prompt updates.

        Returns:
            int: Number of prompts loaded
        """
        old_count = len(self._prompts)
        self._prompts.clear()
        self._load_prompts()
        new_count = len(self._prompts)

        logger.info(
            f"Reloaded prompts: {old_count} -> {new_count} capabilities"
        )
        return new_count

    def validate_prompt(self, capability: str) -> bool:
        """Validate a prompt template.

        Checks that:
        - The capability exists
        - Variables are properly defined
        - Template syntax is valid

        Args:
            capability: Name of the capability to validate

        Returns:
            bool: True if valid, False otherwise
        """
        try:
            template = self.get_prompt(capability)

            # Check variables are properly defined
            if template.variables:
                # Verify all variables in template are in variables list
                import re
                template_vars = set(re.findall(r"\{(\w+)\}", template.user_template))
                defined_vars = set(template.variables)

                missing = template_vars - defined_vars
                if missing:
                    logger.warning(
                        f"Variables in template but not declared: {missing}"
                    )

            return True

        except Exception as e:
            logger.error(f"Validation failed for {capability}: {e}")
            return False

    def get_all_metadata(self) -> Dict[str, Dict[str, Any]]:
        """Get metadata for all capabilities.

        Returns:
            Dict: Capability name -> metadata
        """
        return {
            capability: data.get("metadata", {})
            for capability, data in self._prompts.items()
        }
