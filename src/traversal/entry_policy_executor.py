"""Application entry policy execution with fallback chain.

Handles strategy chain building, per-strategy execution (deeplink,
cold launch, bind-current-screen), and wait-condition verification.
"""

import time
from typing import Any, Dict, List, Optional

from src.graph.node import EntryPolicy, EntryStrategy
from src.graph.plan import TraversalPlan
from src.exception.initialization import EntryError, EntryPolicyError


class EntryPolicyExecutor:
    """Executes the configured entry policy with automatic fallback.

    Dependencies (injected):
    - plan: TraversalPlan with entry policy configuration
    - vision_service: for find_app_icon, find_element, get_current_page
    - action_executor: for press_home, click, execute_deeplink
    - trace_recorder: optional, for recording entry attempts
    - should_record: optional callable → bool, trace-level gating
    """

    def __init__(
        self,
        plan: TraversalPlan,
        vision_service: Any,
        action_executor: Any,
        trace: Optional[Any] = None,
    ):
        self._plan = plan
        self._vision = vision_service
        self._action = action_executor
        self._trace_coordinator = trace

    # -- public ----------------------------------------------------------------

    def execute(self) -> None:
        """Run entry policy fallback chain. Raises EntryPolicyError if all fail."""
        strategy_chain = self._build_chain()
        failed: List[str] = []
        last_error = None

        for strategy in strategy_chain:
            try:
                self._execute_strategy(strategy)
                self._record_success(strategy)
                return
            except EntryError as e:
                failed.append(strategy.value)
                last_error = e
                self._record_failure(strategy, str(e))

        raise EntryPolicyError(
            f"All entry strategies failed for app '{self._plan.entry_app}'",
            failed_strategies=failed,
            last_error=last_error,
        )

    def wait_for_condition(self) -> bool:
        """Verify entry condition. Raises WaitConditionError on failure."""
        from src.exception.initialization import WaitConditionError

        policy = self._plan.entry_policy or EntryPolicy()
        if not policy.wait_condition:
            return True

        mode = self._get_wait_mode()
        condition = policy.wait_condition or {}

        if mode == "fast":
            ok = self._verify_once(condition)
        else:
            ok = self._verify_polling(condition)

        if not ok:
            raise WaitConditionError(
                f"Entry condition not satisfied for app '{self._plan.entry_app}'",
                condition=condition,
                timeout_seconds=self._get_wait_timeout(),
            )
        return True

    # -- strategy chain --------------------------------------------------------

    def _build_chain(self) -> List[EntryStrategy]:
        policy = self._plan.entry_policy or EntryPolicy()
        chain: List[EntryStrategy] = []

        if isinstance(policy.strategy, str):
            try:
                chain.append(EntryStrategy.from_value(policy.strategy))
            except ValueError:
                chain.append(EntryStrategy.COLD_LAUNCH)
        else:
            chain.append(policy.strategy)

        if policy.fallback:
            if isinstance(policy.fallback, str):
                try:
                    fallback = EntryStrategy.from_value(policy.fallback)
                    if fallback != chain[0]:
                        chain.append(fallback)
                except ValueError:
                    pass

        if EntryStrategy.BIND_CURRENT_SCREEN not in chain:
            chain.append(EntryStrategy.BIND_CURRENT_SCREEN)

        return chain

    def _execute_strategy(self, strategy: EntryStrategy) -> None:
        if strategy == EntryStrategy.DIRECT_DEEPLINK:
            self._execute_deeplink()
        elif strategy == EntryStrategy.COLD_LAUNCH:
            self._execute_cold_launch()
        elif strategy == EntryStrategy.BIND_CURRENT_SCREEN:
            self._execute_bind_current()
        else:
            raise EntryError(strategy.value, f"Unknown strategy: {strategy.value}")

    def _execute_deeplink(self) -> None:
        deeplink = f"{self._plan.entry_app}://"
        try:
            self._action.execute_deeplink(deeplink)
            delay_ms = self._get_action_delay()
            if delay_ms > 0:
                time.sleep(delay_ms / 1000.0)
        except Exception as e:
            raise EntryError("direct_deeplink", f"Failed to send deeplink: {e}") from e

    def _execute_cold_launch(self) -> None:
        try:
            self._action.press_home()
            time.sleep(0.5)
            icon_target = self._find_app_icon()
            if not icon_target:
                raise EntryError("cold_launch", f"App icon not found for '{self._plan.entry_app}'")
            self._action.click(icon_target)
            delay_ms = self._get_action_delay()
            if delay_ms > 0:
                time.sleep(delay_ms / 1000.0)
        except EntryError:
            raise
        except Exception as e:
            raise EntryError("cold_launch", f"Failed to launch app: {e}") from e

    def _execute_bind_current(self) -> None:
        delay_ms = self._get_action_delay()
        if delay_ms > 0:
            time.sleep(delay_ms / 1000.0)

    def _find_app_icon(self) -> Optional[str]:
        try:
            result = self._vision.find_element(
                query=f"App icon for {self._plan.entry_app}",
                screen_context="home_screen",
            )
            if result and result.get("found"):
                return result.get("target")
            return None
        except Exception:
            return None

    def _get_action_delay(self) -> int:
        if self._plan.entry_config:
            return self._plan.entry_config.action_delay_ms
        return self._plan.meta.get("action_delay_ms", 100)

    # -- wait condition --------------------------------------------------------

    def _verify_once(self, condition: dict) -> bool:
        try:
            current_path = self._get_current_page_path()
            expected_page = condition.get("page_name")
            if not expected_page:
                return True
            if current_path and current_path[-1] == expected_page:
                return True
            return False
        except Exception:
            return False

    def _verify_polling(self, condition: dict) -> bool:
        timeout = self._get_wait_timeout()
        interval = self._get_wait_interval()
        start = time.time()
        while time.time() - start < timeout:
            if self._verify_once(condition):
                return True
            time.sleep(interval)
        return False

    def _get_current_page_path(self) -> Optional[List[str]]:
        try:
            result = self._vision.get_current_page()
            if result:
                return result.get("path")
            return None
        except Exception:
            return None

    def _get_wait_mode(self) -> str:
        if self._plan.entry_config:
            return self._plan.entry_config.wait_mode
        return self._plan.meta.get("wait_mode", "fast")

    def _get_wait_timeout(self) -> float:
        if self._plan.entry_config:
            return self._plan.entry_config.wait_timeout
        return self._plan.meta.get("wait_timeout", 10.0)

    def _get_wait_interval(self) -> float:
        if self._plan.entry_config:
            return self._plan.entry_config.wait_interval
        return self._plan.meta.get("wait_interval", 1.0)

    # -- trace -----------------------------------------------------------------

    @property
    def _trace_active(self) -> bool:
        return (
            self._trace_coordinator is not None
            and self._trace_coordinator.active
            and self._trace_coordinator.should_record_entry_attempt()
        )

    def _record_success(self, strategy: EntryStrategy) -> None:
        if not self._trace_active:
            return
        from src.trace.models import SpanNode
        span = SpanNode(
            span_type="execution",
            action="entry_strategy",
            target=strategy.value,
            status="success",
        )
        self._trace_coordinator._recorder.record_span(span)

    def _record_failure(self, strategy: EntryStrategy, reason: str) -> None:
        if not self._trace_active:
            return
        from src.trace.models import SpanNode
        span = SpanNode(
            span_type="execution",
            action="entry_strategy",
            target=strategy.value,
            status="failed",
            metadata={"error": reason},
        )
        self._trace_coordinator._recorder.record_span(span)
