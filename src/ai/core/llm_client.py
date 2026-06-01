"""LLM client for DeepSeek API integration."""

import asyncio
import logging
from typing import Dict, List, Optional, Union

import aiohttp

from .config import AIProviderConfig

logger = logging.getLogger(__name__)


class APIError(Exception):
    """Base exception for API errors."""

    pass


class RateLimitError(APIError):
    """Exception for rate limit errors."""

    pass


class TimeoutError(APIError):
    """Exception for timeout errors."""

    pass


class LLMClient:
    """DeepSeek API client with retry logic and concurrent control."""

    def __init__(self, config: AIProviderConfig):
        """Initialize the LLM client.

        Args:
            config: AI provider configuration
        """
        self.config = config
        self._semaphore = asyncio.Semaphore(config.max_concurrent_requests)
        self._session: Optional[aiohttp.ClientSession] = None

    async def _get_session(self) -> aiohttp.ClientSession:
        """Get or create the HTTP session."""
        if self._session is None or self._session.closed:
            timeout = aiohttp.ClientTimeout(total=self.config.request_timeout)
            self._session = aiohttp.ClientSession(timeout=timeout)
        return self._session

    async def _close_session(self) -> None:
        """Close the HTTP session."""
        if self._session and not self._session.closed:
            await self._session.close()

    def _extract_json(self, content: str) -> str:
        """Extract JSON from AI response, handling markdown code blocks.

        Args:
            content: Raw AI response text

        Returns:
            Extracted JSON string
        """
        content = content.strip()

        # Extract JSON if embedded in markdown
        if "```json" in content:
            return content.split("```json")[1].split("```")[0].strip()
        elif "```" in content:
            return content.split("```")[1].split("```")[0].strip()
        return content

    async def _call_api(
        self,
        messages: List[Dict],
        response_format: Optional[Dict],
    ) -> Dict:
        """Make a single API call to DeepSeek.

        Args:
            messages: List of message dictionaries
            response_format: Response format specification (optional)

        Returns:
            Parsed JSON response

        Raises:
            APIError: On API errors
            RateLimitError: On rate limit errors
            TimeoutError: On timeout errors
        """
        session = await self._get_session()

        headers = {
            "Authorization": f"Bearer {self.config.api_key}",
            "Content-Type": "application/json",
        }

        payload = {
            "model": self.config.model,
            "messages": messages,
        }

        # Only add response_format if it's provided
        if response_format:
            payload["response_format"] = response_format

        try:
            async with session.post(
                f"{self.config.base_url}/chat/completions",
                headers=headers,
                json=payload,
            ) as response:
                if response.status == 429:
                    raise RateLimitError("Rate limit exceeded")
                elif response.status >= 500:
                    raise APIError(f"Server error: {response.status}")
                elif response.status >= 400:
                    error_text = await response.text()
                    raise APIError(f"Client error {response.status}: {error_text}")

                data = await response.json()
                return data["choices"][0]["message"]["content"]

        except asyncio.TimeoutError as e:
            raise TimeoutError(f"Request timeout: {e}")
        except aiohttp.ClientError as e:
            raise APIError(f"HTTP client error: {e}")

    async def _call_with_retry(
        self,
        messages: List[Dict],
        response_format: Dict,
    ) -> Dict:
        """Call API with exponential backoff retry.

        Args:
            messages: List of message dictionaries
            response_format: Response format specification

        Returns:
            Parsed JSON response

        Raises:
            APIError: On final failure after all retries
        """
        import json

        last_error = None

        for attempt in range(self.config.retry.max_attempts):
            try:
                async with self._semaphore:
                    response_text = await self._call_api(messages, response_format)

                    # Log response for debugging
                    logger.info(f"[LLM] Raw response length: {len(response_text)}")
                    logger.debug(f"Raw API response (first 200 chars): {response_text[:200]}...")

                    if not response_text or response_text.strip() == "":
                        raise APIError("Empty response from API")

                    # Extract JSON from markdown code blocks if present
                    json_text = self._extract_json(response_text)
                    logger.debug(f"Extracted JSON (first 200 chars): {json_text[:200]}...")

                    # Parse JSON response
                    return json.loads(json_text)

            except (RateLimitError, TimeoutError, APIError) as e:
                last_error = e
                if attempt < self.config.retry.max_attempts - 1:
                    # Calculate delay with exponential backoff
                    delay = min(
                        self.config.retry.base_delay
                        * (self.config.retry.exponential_base ** attempt),
                        self.config.retry.max_delay,
                    )
                    logger.warning(
                        f"API call failed (attempt {attempt + 1}/{self.config.retry.max_attempts}): {e}. "
                        f"Retrying in {delay:.2f}s..."
                    )
                    await asyncio.sleep(delay)
                else:
                    logger.error(f"API call failed after {self.config.retry.max_attempts} attempts: {e}")

        raise last_error or APIError("Unknown error in API call")

    async def call(
        self,
        messages: List[Dict],
        schema: Dict,
    ) -> Dict:
        """Call DeepSeek API with structured output.

        Args:
            messages: List of message dictionaries
            schema: JSON Schema for structured output

        Returns:
            Parsed JSON response

        Raises:
            APIError: On API errors after retries
        """
        # DeepSeek doesn't support structured output yet
        # Use traditional approach: prompt for JSON response
        return await self._call_with_retry(messages, None)

    async def call_with_prompt(
        self,
        prompt: str,
        schema: Dict,
        variables: Optional[Dict] = None,
    ) -> Dict:
        """Call DeepSeek API with a prompt template.

        Args:
            prompt: Prompt template with {variable} placeholders
            schema: JSON Schema for structured output
            variables: Variables to inject into the prompt

        Returns:
            Parsed JSON response

        Raises:
            APIError: On API errors after retries
        """
        # Inject variables into prompt
        formatted_prompt = self._inject_variables(prompt, variables or {})

        # Build messages
        messages = [{"role": "user", "content": formatted_prompt}]

        return await self.call(messages, schema)

    def _inject_variables(self, template: str, variables: Dict) -> str:
        """Inject variables into template.

        Args:
            template: Template string with {variable} placeholders
            variables: Variables to inject

        Returns:
            Formatted string with variables replaced
        """
        result = template
        for key, value in variables.items():
            placeholder = f"{{{key}}}"
            result = result.replace(placeholder, str(value))
        return result

    async def __aenter__(self):
        """Async context manager entry."""
        return self

    async def __aexit__(self, exc_type, exc_val, exc_tb):
        """Async context manager exit."""
        await self._close_session()


__all__ = [
    "LLMClient",
    "APIError",
    "RateLimitError",
    "TimeoutError",
]
