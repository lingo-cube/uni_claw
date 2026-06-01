"""UniBrain - AI Provider for uni-claw framework."""

import logging
from typing import Dict, Optional, Tuple

from .core.config import AIProviderConfig
from .core.llm_client import LLMClient
from .core.prompts import PromptRegistry
from .core.validator import ResponseValidator
from .vision.service import VisionService
from .vision.config import VisionConfig, create_vision_service
from .capabilities.types import (
    TraversalPlan,
    PageTypeVerification,
    SafetyScreeningResult,
    ContextDecisionResult,
    NodeOperation,
    NodeStrategy,
    TraversalNode,
    MismatchDetails,
    Suggestion,
    SafetyEvaluation,
    PageLevelGuidance,
)
from .capabilities.parse_to_plan import ParseToPlanCapability
from .capabilities.verify_page_type import VerifyPageTypeCapability
from .capabilities.screen_safety import ScreenSafetyCapability
from .capabilities.vision_analysis import VisionAnalysisCapability
from .capabilities.context_decision import ContextDecisionCapability
from .advisor import AIStrategyAdvisor
from .types import DecisionResult, ContainerInference
from ..state.content_tree import PageAnalysis
from ..context.traversal_context import TraversalContext
from .metrics import AIMetrics, FailureArchiver

logger = logging.getLogger(__name__)


class UniBrain(AIStrategyAdvisor):
    """UniBrain AI Provider - implements AIStrategyAdvisor interface.

    This provider orchestrates five AI capabilities:
    - ParseToPlanCapability: Instruction parsing
    - VerifyPageTypeCapability: Page type verification
    - ScreenSafetyCapability: Element safety screening
    - VisionAnalysisCapability: Screenshot analysis
    - ContextDecisionCapability: Context-aware decision making
    """

    def __init__(
        self,
        ai_config: AIProviderConfig,
        vision_config: Optional[VisionConfig] = None,
        enable_metrics: bool = True,
        enable_archiving: bool = True,
    ):
        """Initialize the UniBrain provider.

        Args:
            ai_config: AI provider configuration for LLM capabilities
            vision_config: Optional vision service configuration (defaults to mock)
            enable_metrics: Enable metrics collection
            enable_archiving: Enable failure archiving
        """
        # Initialize core components
        self.client = LLMClient(ai_config)
        self.validator = ResponseValidator()
        self.ai_config = ai_config

        # Initialize prompt registry
        self.prompt_registry = PromptRegistry(ai_config)

        # Initialize vision service
        if vision_config is None:
            vision_config = VisionConfig(service_type="mock")
        self.vision_service = create_vision_service(vision_config)

        # Initialize metrics and archiving
        self.metrics = AIMetrics() if enable_metrics else None
        self.archiver = FailureArchiver() if enable_archiving else None

        # Register parsers
        self._register_parsers()

        # Initialize capabilities with metrics and archiver
        self.capabilities = {
            "parse": ParseToPlanCapability(
                self.client, self.validator, ai_config, self.prompt_registry,
                metrics=self.metrics, archiver=self.archiver,
            ),
            "verify": VerifyPageTypeCapability(
                self.client, self.validator, ai_config, self.prompt_registry,
                metrics=self.metrics, archiver=self.archiver,
            ),
            "safety": ScreenSafetyCapability(
                self.client, self.validator, ai_config, self.prompt_registry,
                metrics=self.metrics, archiver=self.archiver,
            ),
            "vision": VisionAnalysisCapability(
                self.vision_service, self.validator,
            ),
            "decision": ContextDecisionCapability(
                self.client, self.validator, ai_config, self.prompt_registry,
                metrics=self.metrics, archiver=self.archiver,
            ),
        }

        logger.info("UniBrain provider initialized with all capabilities")

    def _register_parsers(self) -> None:
        """Register parsers for all response types."""
        # TraversalPlan parser
        def parse_traversal_plan(response: Dict) -> TraversalPlan:
            static_nodes = response.get("static_nodes") or []
            return TraversalPlan(
                entry_app=response.get("entry_app"),
                root_node=self._parse_node(response["root_node"], is_root=True),
                static_nodes=[self._parse_node(n, is_root=False) for n in static_nodes],
                template_registry=response.get("template_registry", "default"),
                mode=response.get("mode", "hybrid"),
                reasoning=response.get("reasoning"),
                confidence=response.get("confidence", 1.0),
            )

        # PageTypeVerification parser
        def parse_page_verification(response: Dict) -> PageTypeVerification:
            mismatch = None
            if "mismatch_details" in response:
                mismatch = MismatchDetails(
                    missing_items=response["mismatch_details"].get("missing_items", []),
                    unexpected_items=response["mismatch_details"].get("unexpected_items", []),
                    type_conflict=response["mismatch_details"].get("type_conflict"),
                )
            suggestion = None
            if "suggestion" in response:
                suggestion = Suggestion(
                    action=response["suggestion"]["action"],
                    target=response["suggestion"].get("target"),
                    reason=response["suggestion"].get("reason", ""),
                )
            return PageTypeVerification(
                is_match=response["is_match"],
                confidence=response["confidence"],
                actual_type=response["actual_type"],
                reasoning=response.get("reasoning", ""),
                mismatch_details=mismatch,
                suggestion=suggestion,
            )

        # SafetyScreeningResult parser
        def parse_safety_result(response: Dict) -> SafetyScreeningResult:
            evaluations = [
                SafetyEvaluation(
                    name=e["name"],
                    safety_tag=e["safety_tag"],
                    confidence=e["confidence"],
                    reason=e["reason"],
                    context_dependency=e.get("context_dependency"),
                    task_relevance=e.get("task_relevance"),
                )
                for e in response["evaluations"]
            ]
            guidance = None
            if "page_level_guidance" in response:
                guidance = PageLevelGuidance(
                    overall_safe_to_proceed=response["page_level_guidance"]["overall_safe_to_proceed"],
                    recommended_max_parallel=response["page_level_guidance"].get("recommended_max_parallel", 3),
                    special_precautions=response["page_level_guidance"].get("special_precautions", []),
                    task_suitability=response["page_level_guidance"].get("task_suitability"),
                )
            return SafetyScreeningResult(
                evaluations=evaluations,
                page_level_guidance=guidance,
            )

        # ContextDecisionResult parser
        def parse_decision_result(response: Dict) -> ContextDecisionResult:
            return ContextDecisionResult(
                result=response["result"],
                action=response["action"],
                target=response.get("target"),
                params=response.get("params"),
                reasoning=response.get("reasoning", ""),
                confidence=response["confidence"],
                safety_verified=response.get("safety_verified", True),
            )

        # Register all parsers
        self.validator.register_parser("TraversalPlan", parse_traversal_plan)
        self.validator.register_parser("PageTypeVerification", parse_page_verification)
        self.validator.register_parser("SafetyScreeningResult", parse_safety_result)
        self.validator.register_parser("ContextDecisionResult", parse_decision_result)
        # PageAnalysis is handled by Pydantic in Vision Service
        self.validator.register_parser("PageAnalysis", lambda r: PageAnalysis(**r))

    def _parse_node(self, node_dict: Dict, is_root: bool = False) -> TraversalNode:
        """Parse a node dict into TraversalNode.

        Handles both full format and simplified format from AI.
        """
        # Check if it's the simplified format (missing required fields)
        if "node_id" not in node_dict:
            # Convert simplified format to full format
            node_type = node_dict.get("type", "container")

            # Handle different simplified formats
            if "strategy" in node_dict:
                # Format with strategy and match
                strategy = node_dict.get("strategy", "dynamic")
                return TraversalNode(
                    node_id="root" if is_root else f"node_{id(node_dict)}",
                    name="root" if is_root else node_type,
                    node_type=node_type,
                    operation=NodeOperation(
                        action="click",
                        target=None,
                        params=None,
                        restore=None,
                    ),
                    precondition=None,
                    children_strategy=NodeStrategy(
                        type=strategy,
                        dynamic_rules={"match": "*"} if strategy == "dynamic" else None,
                        static_children=None,
                    ),
                    error_policy=None,
                )

            # Format with type and children
            return TraversalNode(
                node_id="root" if is_root else f"node_{id(node_dict)}",
                name="root" if is_root else node_type,
                node_type=node_type,
                operation=NodeOperation(
                    action="click",
                    target=None,
                    params=None,
                    restore=None,
                ),
                precondition=None,
                children_strategy=NodeStrategy(
                    type="dynamic_match" if node_dict.get("children") else "none",
                    dynamic_rules={"match": "*"} if node_dict.get("children") else None,
                    static_children=None,
                ),
                error_policy=None,
            )

        # Original full format parsing
        return TraversalNode(
            node_id=node_dict["node_id"],
            name=node_dict["name"],
            node_type=node_dict["node_type"],
            operation=NodeOperation(**node_dict["operation"]),
            precondition=node_dict.get("precondition"),
            children_strategy=NodeStrategy(**node_dict["children_strategy"]),
            error_policy=node_dict.get("error_policy"),
        )

    def infer_container_type(
        self, ui: PageAnalysis, context: TraversalContext
    ) -> ContainerInference:
        """Infer container type using AI.

        Args:
            ui: Current page analysis
            context: Current traversal context

        Returns:
            ContainerInference with type and confidence
        """
        verification = self.capabilities["verify"].execute({
            "page_analysis": ui,
            "expected_type": "auto_detect",
        })

        return ContainerInference(
            container_type=verification.actual_type,
            confidence=verification.confidence,
            matched_template=verification.actual_type,
        )

    def decide_next_action(
        self,
        goal: str,
        ui: PageAnalysis,
        context: TraversalContext,
    ) -> Tuple[DecisionResult, Optional[dict]]:
        """Decide next action using AI.

        Args:
            goal: The goal to achieve
            ui: Current page analysis
            context: Current traversal context

        Returns:
            Tuple of (DecisionResult, optional node_data)
        """
        # First, screen for safety
        safety_result = self.capabilities["safety"].execute({
            "page_analysis": ui,
            "instruction": goal,
        })

        # Then make decision with safety context
        decision = self.capabilities["decision"].execute({
            "reason": f"Achieve goal: {goal}",
            "page_analysis": ui,
            "context": context._asdict() if hasattr(context, "_asdict") else {},
            "safety_result": safety_result,
        })

        # Check confidence threshold
        if decision.confidence < 0.7:
            return DecisionResult.UNSURE, None

        # Convert to node data
        node_data = None
        if decision.result == "success" and decision.action != "no_action":
            node_data = {
                "action": decision.action,
                "target": decision.target,
                "params": decision.params,
                "reasoning": decision.reasoning,
            }

        return DecisionResult.SUCCESS, node_data

    def handle_exception(
        self,
        exception: dict,
        ui: PageAnalysis,
        context: TraversalContext,
    ) -> Tuple[DecisionResult, Optional[dict]]:
        """Handle exception using AI.

        Args:
            exception: Exception context dict
            ui: Current page analysis
            context: Current traversal context

        Returns:
            Tuple of (DecisionResult, optional recovery node_data)
        """
        # Convert exception to recovery goal
        recovery_goal = f"Recover from: {exception.get('type', 'Unknown error')}"

        return self.decide_next_action(recovery_goal, ui, context)

    def analyze_screenshot(self, image_data: bytes) -> PageAnalysis:
        """Analyze screenshot using Vision Service.

        Args:
            image_data: PNG image bytes

        Returns:
            PageAnalysis with detected elements
        """
        return self.capabilities["vision"].execute(image_data)

    def verify_page_with_vision(
        self,
        image_data: bytes,
        expected_type: str,
    ) -> PageTypeVerification:
        """Verify page type using vision analysis.

        Args:
            image_data: PNG image bytes
            expected_type: Expected page type

        Returns:
            PageTypeVerification result
        """
        # First analyze with vision
        page_analysis = self.analyze_screenshot(image_data)

        # Then verify type
        return self.capabilities["verify"].execute({
            "page_analysis": page_analysis,
            "expected_type": expected_type,
        })

    def get_metrics_summary(self) -> Optional[Dict]:
        """Get metrics summary.

        Returns:
            Dict with metrics summary or None if metrics disabled
        """
        if self.metrics:
            return self.metrics.get_summary()
        return None

    def get_latency_stats(self, capability: str) -> Optional[Dict]:
        """Get latency statistics for a capability.

        Args:
            capability: Capability name

        Returns:
            Dict with latency stats or None if metrics disabled
        """
        if self.metrics:
            return self.metrics.get_latency_stats(capability)
        return None

    def get_failure_summary(self) -> Optional[Dict]:
        """Get failure summary.

        Returns:
            Dict with failure summary or None if archiving disabled
        """
        if self.archiver:
            return self.archiver.get_failure_summary()
        return None

    def get_failures(self, capability: Optional[str] = None, limit: int = 100) -> list:
        """Get failure records.

        Args:
            capability: Optional capability name to filter by
            limit: Maximum records to return

        Returns:
            List of failure records
        """
        if self.archiver:
            return self.archiver.get_failures(capability, limit)
        return []


__all__ = ["UniBrain"]
