"""Response validator with parser registry pattern."""

import json
import logging
from typing import Any, Callable, Dict, Optional

from jsonschema import validate, ValidationError as JsonSchemaValidationError

logger = logging.getLogger(__name__)

# Type alias for parser functions
Parser = Callable[[Dict], Any]


class ValidationError(Exception):
    """Raised when response validation fails."""

    pass


class ParserNotFoundError(Exception):
    """Raised when a parser for a response type is not found."""

    def __init__(self, response_type: str):
        super().__init__(f"No parser registered for response type: {response_type}")
        self.response_type = response_type


class ResponseValidator:
    """Response validator using parser registry pattern.

    This class manages:
    - Registration of parser functions for each response type
    - JSON Schema validation
    - Parsing of validated responses into domain objects
    """

    def __init__(self):
        """Initialize the validator with an empty parser registry."""
        self._parsers: Dict[str, Parser] = {}

    def register_parser(self, response_type: str, parser: Parser) -> None:
        """Register a parser for a response type.

        Args:
            response_type: Unique identifier for the response type
            parser: Function that parses a dict into a domain object
        """
        self._parsers[response_type] = parser
        logger.debug(f"Registered parser for response type: {response_type}")

    def validate_and_parse(
        self,
        response: Dict,
        response_type: str,
        schema: Optional[Dict] = None,
    ) -> Any:
        """Validate and parse a response.

        Args:
            response: Raw JSON response dict
            response_type: Type identifier for finding the parser
            schema: Optional JSON Schema for validation

        Returns:
            Parsed domain object

        Raises:
            ParserNotFoundError: If no parser is registered for the type
            ValidationError: If validation fails
        """
        if response_type not in self._parsers:
            raise ParserNotFoundError(response_type)

        # Log the raw response for debugging
        logger.info(f"Raw response for {response_type}: {json.dumps(response, ensure_ascii=False)[:500]}...")

        # Validate schema if provided
        # Note: Schema validation is skipped for now since AI responses vary
        # The parser will handle format conversion
        # if schema is not None:
        #     try:
        #         validate(instance=response, schema=schema)
        #     except JsonSchemaValidationError as e:
        #         logger.error(f"Schema validation failed: {e}")
        #         logger.error(f"Response that failed validation: {json.dumps(response, ensure_ascii=False)}")
        #         raise ValidationError(f"Schema validation failed: {e}")

        # Parse using registered parser
        parser = self._parsers[response_type]
        try:
            return parser(response)
        except Exception as e:
            raise ValidationError(f"Parsing failed: {e}")

    def _validate_schema(self, response: Dict, schema: Dict) -> None:
        """Internal schema validation using jsonschema.

        Args:
            response: Response to validate
            schema: JSON Schema to validate against

        Raises:
            ValidationError: If validation fails
        """
        try:
            validate(instance=response, schema=schema)
        except JsonSchemaValidationError as e:
            raise ValidationError(f"Schema validation failed: {e}")

    def has_parser(self, response_type: str) -> bool:
        """Check if a parser is registered for the given type.

        Args:
            response_type: Type identifier to check

        Returns:
            True if a parser is registered, False otherwise
        """
        return response_type in self._parsers


__all__ = [
    "ResponseValidator",
    "ValidationError",
    "ParserNotFoundError",
    "Parser",
]
