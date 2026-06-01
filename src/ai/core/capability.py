"""Base capability for all AI capabilities."""

import asyncio
import logging
from abc import ABC, abstractmethod
from datetime import datetime
from typing import Any, Dict, Generic, List, TypeVar, Optional

from .config import AIProviderConfig
from .llm_client import LLMClient
from .validator import ResponseValidator

logger = logging.getLogger(__name__)

T_IN = TypeVar("T_IN")
T_OUT = TypeVar("T_OUT")


class BaseCapability(ABC, Generic[T_IN, T_OUT]):
    """Generic base class for all AI capabilities.

    This class provides:
    - Unified execution flow for all capabilities
    - Async execution with sync wrapper
    - Automatic logging and error handling
    - Failure archiving
    - Internal validation hook

    Each capability must implement:
    - system_prompt_key: Key for system prompt template
    - user_prompt_key: Key for user prompt template
    - response_schema: JSON Schema for output validation
    - response_type: Type identifier for parser lookup
    - prepare_input: Convert raw input to prompt variables
    """

    def __init__(
        self,
        client: LLMClient,
        validator: ResponseValidator,
        config: AIProviderConfig,
        prompt_registry: Any = None,
        metrics: Optional[Any] = None,
        archiver: Optional[Any] = None,
    ):
        """Initialize the capability.

        Args:
            client: LLM client for API calls
            validator: Response validator for parsing
            config: AI provider configuration
            prompt_registry: Optional prompt registry for template access
            metrics: Optional AIMetrics collector
            archiver: Optional FailureArchiver
        """
        self.client = client
        self.validator = validator
        self.config = config
        self.prompt_registry = prompt_registry
        self.metrics = metrics
        self.archiver = archiver
        self._logger = logging.getLogger(f"ai.{self.__class__.__name__}")

        # Trace logging support
        try:
            from src.utils.trace import TraceLogger
            self._trace = TraceLogger(self.response_type)
        except ImportError:
            self._trace = None
            self._logger.debug("Trace logging not available")

    @property
    @abstractmethod
    def system_prompt_key(self) -> str:
        """Key for system prompt template in PromptRegistry."""
        pass

    @property
    @abstractmethod
    def user_prompt_key(self) -> str:
        """Key for user prompt template in PromptRegistry."""
        pass

    @property
    @abstractmethod
    def response_schema(self) -> Dict:
        """JSON Schema for output validation."""
        pass

    @property
    @abstractmethod
    def response_type(self) -> str:
        """Response type identifier for parser lookup."""
        pass

    @abstractmethod
    def prepare_input(self, raw_input: T_IN) -> Dict:
        """Prepare input variables for the prompt template.

        Args:
            raw_input: Raw input data

        Returns:
            Dictionary of variables to inject into the prompt
        """
        pass

    async def execute_async(self, input_data: T_IN) -> T_OUT:
        """Execute the capability asynchronously.

        Args:
            input_data: Input data for the capability

        Returns:
            Parsed output data

        Raises:
            ValidationError: If response validation fails
            APIError: If API call fails
        """
        start_time = None
        success = False
        confidence = None
        trace_context = None

        try:
            # Start trace span
            if self._trace:
                trace_context = self._trace.start_span(
                    operation=f"execute.{self.response_type}",
                    tags={"response_type": self.response_type}
                )

            # Prepare input
            variables = self.prepare_input(input_data)

            # Log input
            if self._trace and trace_context:
                self._trace.log_input(trace_context, input=self._sanitize_input(input_data))

            # Build messages (for now, simple user message)
            # Subclasses can override for more complex message building
            messages = self._build_messages(variables)

            # Call LLM
            self._logger.info(f"Calling {self.response_type}")
            import time
            start_time = time.time()

            response = await self.client.call(
                messages=messages,
                schema=self.response_schema,
            )

            duration = time.time() - start_time
            self._logger.info(f"Response received in {duration:.2f}s")

            # Validate and parse
            result = self.validator.validate_and_parse(
                response,
                self.response_type,
                schema=self.response_schema,
            )

            # Extract confidence if available
            confidence = getattr(result, 'confidence', None)

            # Internal validation (optional)
            if self.config.enable_internal_validation:
                self._validate_result(result)

            success = True

            # Log output
            if self._trace and trace_context:
                self._trace.log_output(trace_context, result=self._sanitize_output(result))
                self._trace.finish_span(trace_context, result=result)

            # Record metrics
            self._record_metrics(
                success=success,
                latency_ms=duration * 1000,
                confidence=confidence,
            )

            return result

        except Exception as e:
            self._logger.error(f"Execution failed: {e}")

            # Finish trace with error
            if self._trace and trace_context:
                self._trace.finish_span(trace_context, error=e)

            self._archive_failure(input_data, e)

            # Record failure metrics
            if start_time:
                duration = time.time() - start_time
                self._record_metrics(
                    success=False,
                    latency_ms=duration * 1000,
                )

            raise

    def _sanitize_input(self, input_data: Any) -> Dict:
        """Sanitize input data for logging.

        Args:
            input_data: Raw input data

        Returns:
            Safe dictionary for logging
        """
        if isinstance(input_data, dict):
            return {k: str(v)[:200] for k, v in input_data.items()}
        elif isinstance(input_data, str):
            return {"input": input_data[:500]}
        else:
            return {"input_type": type(input_data).__name__}

    def _sanitize_output(self, result: Any) -> Dict:
        """Sanitize output data for logging.

        Args:
            result: Parsed result

        Returns:
            Safe dictionary for logging
        """
        output = {"type": type(result).__name__}

        # Add common fields
        if hasattr(result, 'confidence'):
            output['confidence'] = result.confidence
        if hasattr(result, 'mode'):
            output['mode'] = result.mode
        if hasattr(result, 'entry_app'):
            output['entry_app'] = result.entry_app
        if hasattr(result, 'is_match'):
            output['is_match'] = result.is_match

        return output

    def _build_messages(self, variables: Dict) -> List[Dict]:
        """Build messages for the LLM call.

        Args:
            variables: Prepared input variables

        Returns:
            Message list for API call
        """
        # Use PromptRegistry if available
        if self.prompt_registry:
            system_prompt = self.prompt_registry.get(self.system_prompt_key)
            user_prompt = self.prompt_registry.get(self.user_prompt_key)
            formatted_user = self.prompt_registry.inject_variables(user_prompt, variables)

            messages = []
            if system_prompt:
                messages.append({"role": "system", "content": system_prompt})
            messages.append({"role": "user", "content": formatted_user})
            return messages

        # Fallback to simple user message
        prompt = self._format_prompt(variables)
        return [{"role": "user", "content": prompt}]

    def _format_prompt(self, variables: Dict) -> str:
        """Format prompt with variables.

        Args:
            variables: Variables to inject

        Returns:
            Formatted prompt string
        """
        # Default implementation - subclasses can override
        return f"Process: {variables}"

    def execute(self, input_data: T_IN) -> T_OUT:
        """Execute the capability synchronously.

        Args:
            input_data: Input data for the capability

        Returns:
            Parsed output data
        """
        loop = asyncio.get_event_loop()
        return loop.run_until_complete(self.execute_async(input_data))

    def _validate_result(self, result: T_OUT) -> None:
        """Internal AI validation (subclass can override).

        Args:
            result: Parsed result to validate

        Raises:
            ValidationError: If validation fails
        """
        pass  # Default: no validation

    def _archive_failure(self, input_data: T_IN, error: Exception) -> None:
        """Archive failure information.

        Args:
            input_data: Input that caused the failure
            error: Exception that occurred
        """
        if self.archiver:
            self.archiver.archive_failure(
                capability=self.__class__.__name__,
                input_data=input_data,
                error=error,
                context={"response_type": self.response_type},
            )
        else:
            # Fallback: log the failure
            self._logger.warning(f"Failure recorded: {self.__class__.__name__}: {error}")

    def _record_metrics(
        self,
        success: bool,
        latency_ms: float,
        confidence: Optional[float] = None,
    ) -> None:
        """Record execution metrics.

        Args:
            success: Whether the execution succeeded
            latency_ms: Execution duration in milliseconds
            confidence: Optional confidence score
        """
        if self.metrics:
            self.metrics.record_call(
                capability=self.__class__.__name__,
                success=success,
                latency_ms=latency_ms,
                confidence=confidence,
            )


__all__ = ["BaseCapability"]
