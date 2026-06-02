"""Flattened vision service for two-step pipeline.

This module implements the new two-step visual pipeline service that
separates multimodal visual perception from logical reasoning.
"""

import hashlib
import json
import logging
from dataclasses import dataclass
from typing import Optional, Dict, Any

from src.models.vision.flattened_screen import FlattenedScreen
from src.state.content_tree import PageAnalysis
from src.ai.vision.multimodal_analyzer import MultimodalAnalyzer, MultimodalAnalysisResult
from src.ai.vision.page_analysis_assembler import PageAnalysisAssembler, AssemblyResult


logger = logging.getLogger(__name__)


@dataclass
class VisionAnalysisResult:
    """Result from the two-step vision analysis pipeline.

    Attributes:
        page_analysis: The final page analysis
        total_latency_ms: Total pipeline latency
        multimodal_latency_ms: Multimodal step latency
        assembler_latency_ms: Assembler step latency
        total_tokens: Total tokens consumed
        multimodal_tokens: Tokens from multimodal step
        assembler_tokens: Tokens from assembler step
        multimodal_cached: Whether multimodal result was cached
        assembler_cached: Whether assembler result was cached
    """

    page_analysis: PageAnalysis
    total_latency_ms: float
    multimodal_latency_ms: float
    assembler_latency_ms: float
    total_tokens: int
    multimodal_tokens: int
    assembler_tokens: int
    multimodal_cached: bool
    assembler_cached: bool


class FlattenedVisionService:
    """Two-step visual pipeline service.

    This service implements the new architecture that separates:
    1. Multimodal visual perception → FlattenedScreen
    2. Text-based logical assembly → PageAnalysis

    The service includes caching and fallback to legacy service.
    """

    def __init__(
        self,
        multimodal_analyzer: MultimodalAnalyzer,
        assembler: PageAnalysisAssembler,
        screen_cache=None,
        page_analysis_cache=None,
        legacy_service=None,
    ):
        """Initialize the flattened vision service.

        Args:
            multimodal_analyzer: Multimodal analyzer for visual perception
            assembler: Page assembler for logical reasoning
            screen_cache: Optional cache for FlattenedScreen results
            page_analysis_cache: Optional cache for PageAnalysis results
            legacy_service: Optional legacy service for fallback
        """
        self.multimodal_analyzer = multimodal_analyzer
        self.assembler = assembler
        self.screen_cache = screen_cache
        self.page_analysis_cache = page_analysis_cache
        self.legacy_service = legacy_service

        logger.info("FlattenedVisionService initialized with two-step pipeline")

    def analyze_screenshot(
        self,
        image_data: bytes,
        context: Optional[Dict[str, Any]] = None,
    ) -> VisionAnalysisResult:
        """Analyze a screenshot using the two-step pipeline.

        Args:
            image_data: PNG format screenshot data
            context: Optional traversal context

        Returns:
            VisionAnalysisResult containing the PageAnalysis and metrics

        Raises:
            ValueError: If image_data is invalid
            RuntimeError: If pipeline fails (may fallback to legacy)
        """
        if not image_data:
            raise ValueError("image_data cannot be empty")

        try:
            # Step 1: Multimodal visual perception
            multimodal_result = self._analyze_multimodal(image_data)

            # Step 2: Text-based logical assembly
            assembly_result = self._assemble_page(
                multimodal_result.flattened_screen,
                context,
            )

            return VisionAnalysisResult(
                page_analysis=assembly_result.page_analysis,
                total_latency_ms=multimodal_result.latency_ms + assembly_result.latency_ms,
                multimodal_latency_ms=multimodal_result.latency_ms,
                assembler_latency_ms=assembly_result.latency_ms,
                total_tokens=multimodal_result.output_tokens + assembly_result.output_tokens,
                multimodal_tokens=multimodal_result.output_tokens,
                assembler_tokens=assembly_result.output_tokens,
                multimodal_cached=multimodal_result.cached,
                assembler_cached=assembly_result.cached,
            )

        except Exception as e:
            logger.warning(
                f"Flattened pipeline failed: {e}, "
                f"attempting fallback to legacy service"
            )
            if self.legacy_service:
                return self._fallback_to_legacy(image_data, context)
            else:
                logger.error("No legacy service available for fallback")
                raise

    def _analyze_multimodal(self, image_data: bytes) -> MultimodalAnalysisResult:
        """Perform multimodal visual analysis (Step 1).

        Args:
            image_data: PNG format screenshot data

        Returns:
            MultimodalAnalysisResult with FlattenedScreen
        """
        # Check cache first
        if self.screen_cache:
            cached = self.screen_cache.get(image_data)
            if cached is not None:
                logger.debug("Screen cache hit")
                return MultimodalAnalysisResult(
                    flattened_screen=cached,
                    latency_ms=0,
                    input_tokens=0,
                    output_tokens=0,
                    cached=True,
                )

        # Call multimodal analyzer
        result = self.multimodal_analyzer.analyze(image_data)

        # Cache the result
        if self.screen_cache:
            self.screen_cache.set(image_data, result.flattened_screen)

        logger.debug(
            f"Multimodal analysis complete: {result.latency_ms:.0f}ms, "
            f"{result.input_tokens + result.output_tokens} tokens"
        )

        return result

    def _assemble_page(
        self,
        flattened_screen: FlattenedScreen,
        context: Optional[Dict[str, Any]] = None,
    ) -> AssemblyResult:
        """Assemble page analysis from flattened screen (Step 2).

        Args:
            flattened_screen: Flattened screen representation
            context: Optional traversal context

        Returns:
            AssemblyResult with PageAnalysis
        """
        # Generate cache key
        cache_key = self._generate_assembly_cache_key(flattened_screen, context)

        # Check cache first
        if self.page_analysis_cache:
            cached = self.page_analysis_cache.get(cache_key)
            if cached is not None:
                logger.debug("Page analysis cache hit")
                return AssemblyResult(
                    page_analysis=cached,
                    latency_ms=0,
                    input_tokens=0,
                    output_tokens=0,
                    cached=True,
                )

        # Call assembler
        result = self.assembler.assemble(flattened_screen, context or {})

        # Cache the result
        if self.page_analysis_cache:
            self.page_analysis_cache.set(cache_key, result.page_analysis)

        logger.debug(
            f"Page assembly complete: {result.latency_ms:.0f}ms, "
            f"{result.input_tokens + result.output_tokens} tokens"
        )

        return result

    def _generate_assembly_cache_key(
        self,
        flattened_screen: FlattenedScreen,
        context: Optional[Dict[str, Any]] = None,
    ) -> str:
        """Generate cache key for page assembly.

        Args:
            flattened_screen: Flattened screen representation
            context: Optional context

        Returns:
            Cache key string
        """
        # Hash the flattened screen representation
        screen_json = json.dumps(flattened_screen.to_dict(), sort_keys=True)
        screen_hash = hashlib.md5(screen_json.encode()).hexdigest()

        # Hash the context
        context_json = json.dumps(context or {}, sort_keys=True)
        context_hash = hashlib.md5(context_json.encode()).hexdigest()

        return f"{screen_hash}:{context_hash}"

    def _fallback_to_legacy(
        self,
        image_data: bytes,
        context: Optional[Dict[str, Any]] = None,
    ) -> VisionAnalysisResult:
        """Fallback to legacy vision service.

        Args:
            image_data: PNG format screenshot data
            context: Optional context

        Returns:
            VisionAnalysisResult with fallback analysis
        """
        logger.info("Falling back to legacy vision service")

        page_analysis = self.legacy_service.analyze_screenshot(image_data, context)

        return VisionAnalysisResult(
            page_analysis=page_analysis,
            total_latency_ms=0,
            multimodal_latency_ms=0,
            assembler_latency_ms=0,
            total_tokens=0,
            multimodal_tokens=0,
            assembler_tokens=0,
            multimodal_cached=False,
            assembler_cached=False,
        )
