"""Page analysis assembler for converting flattened screens to hierarchical structure.

This module defines the interface and implementation for assembling
flattened screen representations into PageAnalysis structures using
text-based AI models.
"""

import json
import logging
import time
from abc import ABC, abstractmethod
from dataclasses import dataclass
from typing import Optional, Dict, Any

from src.models.vision.flattened_screen import FlattenedScreen
from src.state.content_tree import PageAnalysis


logger = logging.getLogger(__name__)


@dataclass
class AssemblyResult:
    """Result from page assembly process.

    Attributes:
        page_analysis: The assembled page analysis
        latency_ms: Assembly latency in milliseconds
        input_tokens: Input tokens consumed
        output_tokens: Output tokens consumed
        cached: Whether the result came from cache
        model: Model used for assembly
    """

    page_analysis: PageAnalysis
    latency_ms: float
    input_tokens: int
    output_tokens: int
    cached: bool = False
    model: str = ""


class PageAnalysisAssembler(ABC):
    """Abstract base class for page analysis assemblers.

    Assembles flattened screen representations into hierarchical
    PageAnalysis structures using text-based AI models.
    """

    @abstractmethod
    def assemble(
        self,
        flattened_screen: FlattenedScreen,
        context: Optional[Dict[str, Any]] = None,
    ) -> AssemblyResult:
        """Assemble flattened screen into PageAnalysis.

        Args:
            flattened_screen: Flattened screen representation
            context: Optional traversal context (current path, history, etc.)

        Returns:
            AssemblyResult containing the PageAnalysis and metrics

        Raises:
            ValueError: If flattened_screen is invalid
            RuntimeError: If assembly fails
        """
        pass


class DeepSeekPageAnalysisAssembler(PageAnalysisAssembler):
    """DeepSeek-based page analysis assembler implementation.

    Uses DeepSeek text models to assemble hierarchical page structures
    from flattened screen representations.
    """

    def __init__(
        self,
        ai_provider,
        model: str = "deepseek-v4-flash",
        prompt_template: Optional[str] = None,
    ):
        """Initialize the DeepSeek page assembler.

        Args:
            ai_provider: AI provider for making API calls
            model: Model identifier (default: deepseek-v4-flash)
            prompt_template: Optional custom prompt template
        """
        self.ai_provider = ai_provider
        self.model = model
        self._prompt_template = prompt_template or self._load_default_template()

    def _load_default_template(self) -> str:
        """Load the default assembler prompt template."""
        from src.ai.vision.prompts.assembler_prompt import ASSEMBLER_PROMPT_TEMPLATE
        return ASSEMBLER_PROMPT_TEMPLATE

    def assemble(
        self,
        flattened_screen: FlattenedScreen,
        context: Optional[Dict[str, Any]] = None,
    ) -> AssemblyResult:
        """Assemble flattened screen into PageAnalysis.

        Args:
            flattened_screen: Flattened screen representation
            context: Optional traversal context

        Returns:
            AssemblyResult containing the PageAnalysis and metrics

        Raises:
            ValueError: If flattened_screen is invalid
            RuntimeError: If assembly fails
        """
        if not flattened_screen:
            raise ValueError("flattened_screen cannot be None")

        start_time = time.time()

        try:
            # Build the prompt
            prompt = self._build_prompt(flattened_screen, context or {})

            # Call AI model
            response = self._call_ai_model(prompt)

            latency_ms = (time.time() - start_time) * 1000

            # Parse response into PageAnalysis
            page_analysis = self._parse_response(response)

            # Extract token usage if available
            input_tokens = getattr(response.usage, 'input_tokens', 0) if hasattr(response, 'usage') else 0
            output_tokens = getattr(response.usage, 'output_tokens', 0) if hasattr(response, 'usage') else 0

            return AssemblyResult(
                page_analysis=page_analysis,
                latency_ms=latency_ms,
                input_tokens=input_tokens,
                output_tokens=output_tokens,
                cached=False,
                model=self.model,
            )

        except Exception as e:
            logger.error(f"Page assembly failed: {e}")
            raise RuntimeError(f"Failed to assemble page analysis: {e}") from e

    def _build_prompt(
        self,
        flattened_screen: FlattenedScreen,
        context: Dict[str, Any],
    ) -> str:
        """Build the prompt for the AI model.

        Args:
            flattened_screen: Flattened screen representation
            context: Traversal context

        Returns:
            Formatted prompt string
        """
        # Convert flattened screen to JSON
        flattened_json = json.dumps(
            flattened_screen.to_dict(),
            ensure_ascii=False,
            indent=2,
        )

        # Convert context to JSON
        context_json = json.dumps(context, ensure_ascii=False, indent=2)

        # Format the prompt template
        return self._prompt_template.format(
            flattened_screen=flattened_json,
            context=context_json,
        )

    def _call_ai_model(self, prompt: str):
        """Call the AI model with the prompt.

        Args:
            prompt: Formatted prompt string

        Returns:
            AI model response
        """
        # Try to use the AI provider's complete method
        if hasattr(self.ai_provider, 'complete'):
            return self.ai_provider.complete(
                prompt=prompt,
                model=self.model,
                response_format={"type": "json_object"},
            )
        else:
            raise RuntimeError(
                "AI provider does not support text completion. "
                "Please use a provider with text capabilities."
            )

    def _parse_response(self, response) -> PageAnalysis:
        """Parse AI response into PageAnalysis.

        Args:
            response: AI model response

        Returns:
            PageAnalysis instance

        Raises:
            ValueError: If response cannot be parsed
        """
        try:
            # Extract content from response
            content = response.content if hasattr(response, 'content') else response
            data = json.loads(content)

        except (json.JSONDecodeError, TypeError) as e:
            raise ValueError(f"Failed to parse AI response as JSON: {e}") from e

        # Validate structure
        if not isinstance(data, dict):
            raise ValueError("Response must be a JSON object")

        # Use PageAnalysis.from_dict if available, otherwise build manually
        try:
            return PageAnalysis.from_dict(data)
        except Exception:
            # Fallback to manual construction
            return self._build_page_analysis(data)

    def _build_page_analysis(self, data: Dict[str, Any]) -> PageAnalysis:
        """Build PageAnalysis from parsed data (fallback method).

        Args:
            data: Parsed JSON data

        Returns:
            PageAnalysis instance
        """
        from src.state.content_tree import (
            MenuInfo,
            MenuItem,
            MenuItemType,
            ExpectedAction,
            Coordinate,
            PopupInfo,
            Direction,
        )

        # Build level1 menus
        level1_menus = []
        for menu_data in data.get('level1_menus', []):
            coord_data = menu_data.get('coordinate', {})
            level1_menus.append(MenuInfo(
                name=menu_data.get('name', ''),
                coordinate=Coordinate(
                    x=coord_data.get('x', 0.0),
                    y=coord_data.get('y', 0.0),
                ),
                active=menu_data.get('active', False),
            ))

        # Build level2 menus
        level2_menus = []
        for menu_data in data.get('level2_menus', []):
            coord_data = menu_data.get('coordinate', {})
            level2_menus.append(MenuInfo(
                name=menu_data.get('name', ''),
                coordinate=Coordinate(
                    x=coord_data.get('x', 0.0),
                    y=coord_data.get('y', 0.0),
                ),
                active=menu_data.get('active', False),
            ))

        # Build items
        items = []
        for item_data in data.get('items', []):
            coord_data = item_data.get('coordinate', {})
            items.append(MenuItem(
                name=item_data.get('name', ''),
                type=MenuItemType.from_value(item_data.get('type', 'item')),
                coordinate=Coordinate(
                    x=coord_data.get('x', 0.0),
                    y=coord_data.get('y', 0.0),
                ),
                parent=item_data.get('parent'),
                description=item_data.get('description'),
                expected_action=ExpectedAction.from_value(
                    item_data.get('expected_action', 'action')
                ),
                expects_page_change=item_data.get('expects_page_change', False),
                expects_state_change=item_data.get('expects_state_change', False),
            ))

        # Parse direction (handle None case)
        level1_dir_value = data.get('level1_dir', 'left')
        level1_dir = Direction.from_value(level1_dir_value) if level1_dir_value else Direction.LEFT

        level2_dir_value = data.get('level2_dir', 'top')
        level2_dir = Direction.from_value(level2_dir_value) if level2_dir_value else Direction.TOP

        # Parse popup info
        popup_info = None
        if data.get('popup_info'):
            popup_data = data['popup_info']
            close_coord = popup_data.get('close_button')
            popup_info = PopupInfo(
                title=popup_data.get('title'),
                content=popup_data.get('content'),
                close_button=Coordinate(
                    x=close_coord.get('x', 0.0) if close_coord else None,
                    y=close_coord.get('y', 0.0) if close_coord else None,
                ) if close_coord else None,
            )

        # Parse close/back button coordinates
        close_button = None
        if data.get('close_button'):
            cb = data['close_button']
            close_button = Coordinate(x=cb.get('x', 0.0), y=cb.get('y', 0.0))

        back_button = None
        if data.get('back_button'):
            bb = data['back_button']
            back_button = Coordinate(x=bb.get('x', 0.0), y=bb.get('y', 0.0))

        return PageAnalysis(
            level1_dir=level1_dir,
            level1_menus=level1_menus,
            level2_dir=level2_dir,
            level2_menus=level2_menus,
            current_path=data.get('current_path', []),
            items=items,
            is_popup=data.get('is_popup', False),
            popup_info=popup_info,
            close_button=close_button,
            back_button=back_button,
            has_scroll=data.get('has_scroll', False),
            is_end_of_list=data.get('is_end_of_list', False),
        )
