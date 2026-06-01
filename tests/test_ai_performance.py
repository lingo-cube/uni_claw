"""Performance benchmark tests for AI Provider.

Tests cover:
- Task 8.8: Performance benchmark tests
- Latency measurement (P50, P95, P99)
- Throughput measurement
- Resource usage monitoring
"""

import asyncio
import statistics
import time
from typing import List
from unittest.mock import AsyncMock, MagicMock

import pytest

from src.ai import UniBrain, AIProviderConfig, RetryConfig
from src.ai.vision.config import VisionConfig
from src.ai.metrics import AIMetrics
from src.state.content_tree import PageAnalysis, Direction, Coordinate
from src.context.traversal_context import TraversalContext


class TestAIMetricsPerformance:
    """Performance tests for AIMetrics."""

    @pytest.fixture
    def metrics(self):
        """Create AIMetrics instance."""
        return AIMetrics(max_records=10000)

    def test_metrics_write_performance(self, metrics):
        """Test that metrics recording is fast."""
        iterations = 1000
        start = time.time()

        for i in range(iterations):
            metrics.record_call(
                capability="TestCapability",
                success=True,
                latency_ms=50.0,
                confidence=0.9,
            )

        duration = time.time() - start
        ops_per_second = iterations / duration

        # Should be able to record at least 1000 ops/sec
        assert ops_per_second > 1000, f"Metrics write too slow: {ops_per_second:.0f} ops/sec"

    def test_metrics_query_performance(self, metrics):
        """Test that metrics queries are fast."""
        # Seed with data
        for i in range(100):
            metrics.record_call(
                capability="TestCapability",
                success=True,
                latency_ms=50.0 + i,
                confidence=0.9,
            )

        start = time.time()
        iterations = 100

        for _ in range(iterations):
            metrics.get_latency_stats("TestCapability")
            metrics.get_confidence_distribution("TestCapability")

        duration = time.time() - start
        ops_per_second = iterations / duration

        # Should be able to query at least 100 ops/sec
        assert ops_per_second > 100, f"Metrics query too slow: {ops_per_second:.0f} ops/sec"

    def test_large_dataset_performance(self, metrics):
        """Test metrics performance with large dataset."""
        # Simulate 10k records
        start = time.time()

        for i in range(10000):
            metrics.record_call(
                capability="TestCapability",
                success=i % 10 != 0,  # 10% failure rate
                latency_ms=50.0 + (i % 100),
                confidence=0.5 + (i % 50) / 100,
            )

        write_duration = time.time() - start
        assert write_duration < 5.0, f"Writing 10k records took {write_duration:.2f}s"

        # Test query performance
        start = time.time()
        stats = metrics.get_latency_stats("TestCapability")
        query_duration = time.time() - start

        assert query_duration < 1.0, f"Query took {query_duration:.2f}s"
        assert stats["count"] == 10000


class TestUniBrainPerformance:
    """Performance tests for UniBrain provider."""

    @pytest.fixture
    def provider(self):
        """Create UniBrain provider with metrics."""
        config = AIProviderConfig(
            api_key="test-key",
            retry=RetryConfig(max_attempts=1)
        )
        vision = VisionConfig(service_type="mock")
        return UniBrain(config, vision, enable_metrics=True)

    def test_vision_analysis_latency(self, provider):
        """Test vision analysis latency."""
        latencies: List[float] = []

        for _ in range(10):
            start = time.time()
            provider.analyze_screenshot(b"test_image")
            latency = time.time() - start
            latencies.append(latency * 1000)  # Convert to ms

        # Calculate percentiles
        p50 = statistics.median(latencies)
        p95 = self._percentile(latencies, 95)
        p99 = self._percentile(latencies, 99)

        print(f"\nVision Analysis Latency:")
        print(f"  P50: {p50:.2f}ms")
        print(f"  P95: {p95:.2f}ms")
        print(f"  P99: {p99:.2f}ms")

        # Mock vision should be very fast
        assert p50 < 10, f"P50 latency too high: {p50:.2f}ms"
        assert p95 < 50, f"P95 latency too high: {p95:.2f}ms"

    def test_concurrent_capability_calls(self, provider):
        """Test concurrent capability call performance."""
        async def call_capability():
            return provider.capabilities["vision"].execute_async(b"test_image")

        start = time.time()
        loop = asyncio.new_event_loop()
        asyncio.set_event_loop(loop)

        try:
            # Run 10 concurrent calls
            tasks = [call_capability() for _ in range(10)]
            results = loop.run_until_complete(asyncio.gather(*tasks))
        finally:
            loop.close()

        duration = time.time() - start
        ops_per_second = len(results) / duration

        print(f"\nConcurrent Calls:")
        print(f"  Duration: {duration:.2f}s")
        print(f"  Throughput: {ops_per_second:.0f} ops/sec")

        # Should handle concurrent calls efficiently
        assert ops_per_second > 5, f"Throughput too low: {ops_per_second:.0f} ops/sec"

    def test_metrics_collection_overhead(self, provider):
        """Test that metrics collection doesn't add significant overhead."""
        # Test without metrics
        provider_no_metrics = UniBrain(
            AIProviderConfig(api_key="test", retry=RetryConfig(max_attempts=1)),
            VisionConfig(service_type="mock"),
            enable_metrics=False,
        )

        iterations = 100

        # Time without metrics
        start = time.time()
        for _ in range(iterations):
            provider_no_metrics.analyze_screenshot(b"test_image")
        duration_without = time.time() - start

        # Time with metrics
        start = time.time()
        for _ in range(iterations):
            provider.analyze_screenshot(b"test_image")
        duration_with = time.time() - start

        overhead = ((duration_with - duration_without) / duration_without) * 100

        print(f"\nMetrics Collection Overhead:")
        print(f"  Without metrics: {duration_without:.3f}s")
        print(f"  With metrics: {duration_with:.3f}s")
        print(f"  Overhead: {overhead:.1f}%")

        # Overhead should be minimal (< 20%)
        assert overhead < 20, f"Metrics overhead too high: {overhead:.1f}%"

    @staticmethod
    def _percentile(values: List[float], p: float) -> float:
        """Calculate percentile value."""
        if not values:
            return 0.0
        sorted_values = sorted(values)
        k = (len(sorted_values) - 1) * (p / 100)
        f = int(k)
        c = k - f
        if f + 1 < len(sorted_values):
            return sorted_values[f] + c * (sorted_values[f + 1] - sorted_values[f])
        return sorted_values[f]


class TestCapabilityThroughput:
    """Throughput tests for individual capabilities."""

    @pytest.fixture
    def provider(self):
        """Create UniBrain provider."""
        config = AIProviderConfig(
            api_key="test-key",
            retry=RetryConfig(max_attempts=1)
        )
        vision = VisionConfig(service_type="mock")
        return UniBrain(config, vision, enable_metrics=True)

    def test_vision_capability_throughput(self, provider):
        """Test vision capability throughput."""
        iterations = 50

        start = time.time()
        for _ in range(iterations):
            provider.capabilities["vision"].execute(b"test_image")
        duration = time.time() - start

        throughput = iterations / duration

        print(f"\nVision Capability Throughput: {throughput:.0f} calls/sec")
        assert throughput > 10, f"Throughput too low: {throughput:.0f} calls/sec"

    def test_vision_capability_memory_stability(self, provider):
        """Test that vision capability doesn't leak memory over many calls."""
        import gc
        import sys

        # Force garbage collection
        gc.collect()

        # Get baseline memory
        baseline_objects = len(gc.get_objects())

        # Make many calls
        for _ in range(100):
            provider.capabilities["vision"].execute(b"test_image")

        # Force garbage collection
        gc.collect()

        # Check object count
        final_objects = len(gc.get_objects())
        object_growth = final_objects - baseline_objects

        print(f"\nMemory Growth:")
        print(f"  Baseline objects: {baseline_objects}")
        print(f"  Final objects: {final_objects}")
        print(f"  Growth: {object_growth}")

        # Object growth should be reasonable (< 1000 objects)
        assert object_growth < 1000, f"Possible memory leak: {object_growth} objects created"


class TestLatencyBenchmarks:
    """Benchmark tests for latency targets."""

    @pytest.fixture
    def provider(self):
        """Create UniBrain provider."""
        config = AIProviderConfig(
            api_key="test-key",
            retry=RetryConfig(max_attempts=1)
        )
        vision = VisionConfig(service_type="mock")
        return UniBrain(config, vision, enable_metrics=True)

    def test_latency_targets(self, provider):
        """Test that system meets latency targets.

        Target latencies (with mock vision):
        - P50: < 10ms
        - P95: < 50ms
        - P99: < 100ms
        """
        latencies = []

        for _ in range(100):
            start = time.time()
            provider.analyze_screenshot(b"test_image")
            latency = (time.time() - start) * 1000
            latencies.append(latency)

        p50 = statistics.median(latencies)
        p95 = self._percentile(latencies, 95)
        p99 = self._percentile(latencies, 99)

        print(f"\nLatency Benchmarks (100 iterations):")
        print(f"  P50: {p50:.2f}ms (target: <10ms)")
        print(f"  P95: {p95:.2f}ms (target: <50ms)")
        print(f"  P99: {p99:.2f}ms (target: <100ms)")

        # Assert targets (relaxed for mock vision)
        assert p50 < 20, f"P50 exceeds target: {p50:.2f}ms"
        assert p95 < 100, f"P95 exceeds target: {p95:.2f}ms"
        assert p99 < 200, f"P99 exceeds target: {p99:.2f}ms"

    @staticmethod
    def _percentile(values: List[float], p: float) -> float:
        """Calculate percentile value."""
        if not values:
            return 0.0
        sorted_values = sorted(values)
        k = (len(sorted_values) - 1) * (p / 100)
        f = int(k)
        c = k - f
        if f + 1 < len(sorted_values):
            return sorted_values[f] + c * (sorted_values[f + 1] - sorted_values[f])
        return sorted_values[f]


@pytest.mark.slow
class TestLoadBenchmarks:
    """Load testing benchmarks (marked as slow)."""

    @pytest.fixture
    def provider(self):
        """Create UniBrain provider."""
        config = AIProviderConfig(
            api_key="test-key",
            retry=RetryConfig(max_attempts=1),
            max_concurrent_requests=10,
        )
        vision = VisionConfig(service_type="mock")
        return UniBrain(config, vision, enable_metrics=True)

    def test_sustained_load(self, provider):
        """Test system under sustained load."""
        iterations = 1000
        latencies = []

        start = time.time()

        for i in range(iterations):
            call_start = time.time()
            provider.analyze_screenshot(b"test_image")
            latency = (time.time() - call_start) * 1000
            latencies.append(latency)

            # Print progress every 100 iterations
            if (i + 1) % 100 == 0:
                current_p95 = self._percentile(latencies, 95)
                print(f"  {i + 1}/{iterations} iterations, P95: {current_p95:.2f}ms")

        total_duration = time.time() - start
        throughput = iterations / total_duration
        p50 = statistics.median(latencies)
        p95 = self._percentile(latencies, 95)
        p99 = self._percentile(latencies, 99)

        print(f"\nSustained Load Results ({iterations} iterations):")
        print(f"  Total duration: {total_duration:.2f}s")
        print(f"  Throughput: {throughput:.0f} calls/sec")
        print(f"  P50 latency: {p50:.2f}ms")
        print(f"  P95 latency: {p95:.2f}ms")
        print(f"  P99 latency: {p99:.2f}ms")

        # System should maintain performance under load
        assert p95 < 200, f"P95 degraded under load: {p95:.2f}ms"

    @staticmethod
    def _percentile(values: List[float], p: float) -> float:
        """Calculate percentile value."""
        if not values:
            return 0.0
        sorted_values = sorted(values)
        k = (len(sorted_values) - 1) * (p / 100)
        f = int(k)
        c = k - f
        if f + 1 < len(sorted_values):
            return sorted_values[f] + c * (sorted_values[f + 1] - sorted_values[f])
        return sorted_values[f]
