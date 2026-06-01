#!/usr/bin/env python3
"""Example: Using error callbacks with ADB client.

The ADB client has a two-tier error handling system:
1. Default: Logs errors automatically (no setup needed)
2. Custom: Register a callback for custom handling (retry, notify, etc.)
"""

from pathlib import Path
from src.adb.adb_client import RealADBClient, OperationType, ADBError


# ============================================================================
# Example 1: Default behavior (just logging)
# ============================================================================

def example_default_logging():
    """Default behavior - errors are automatically logged."""
    print("=" * 60)
    print("Example 1: 默认日志记录")
    print("=" * 60)
    print("\n创建客户端（无需额外配置）:")
    client = RealADBClient()
    print("✓ 客户端已创建，错误将自动记录到日志\n")

    print("尝试无效操作:")
    try:
        client.tap(999, 0.5)  # 无效坐标
    except ADBError as e:
        print(f"✓ 异常被抛出，同时错误已被记录到日志\n")


# ============================================================================
# Example 2: Custom callback with retry logic
# ============================================================================

class ADBController:
    """ADB controller with automatic retry on failure."""

    def __init__(self, max_retries: int = 3):
        self.client = RealADBClient()
        self.max_retries = max_retries
        self.retry_count = 0

        # Register custom error handler
        self.client.set_error_callback(self._on_error)

    def _on_error(self, operation: OperationType, message: str, exception=None):
        """Custom error handler with retry logic."""
        print(f"⚠️  错误: [{operation.value}] {message}")

        if self.retry_count < self.max_retries:
            self.retry_count += 1
            print(f"🔄 准备重试 ({self.retry_count}/{self.max_retries})...")
        else:
            print(f"❌ 已达最大重试次数，放弃操作")

    def tap_with_retry(self, x: float, y: float) -> bool:
        """Tap with automatic retry on failure."""
        self.retry_count = 0

        for attempt in range(self.max_retries + 1):
            try:
                self.client.tap(x, y)
                if attempt > 0:
                    print(f"✅ 重试 #{attempt} 成功!")
                return True
            except ADBError:
                if attempt < self.max_retries:
                    continue
                return False

        return False


def example_retry_logic():
    """Example with custom retry logic."""
    print("=" * 60)
    print("Example 2: 自定义重试逻辑")
    print("=" * 60)

    controller = ADBController(max_retries=2)

    print("\n尝试点击操作:")
    success = controller.tap_with_retry(0.5, 0.5)
    print(f"结果: {'成功' if success else '失败'}\n")


# ============================================================================
# Example 3: Callback for statistics/metrics
# ============================================================================

class MetricsCollector:
    """Collect error metrics for monitoring."""

    def __init__(self):
        self.client = RealADBClient()
        self.error_counts = {op.value: 0 for op in OperationType}
        self.total_errors = 0

        self.client.set_error_callback(self._track_error)

    def _track_error(self, operation: OperationType, message: str, exception=None):
        """Track error statistics."""
        self.error_counts[operation.value] += 1
        self.total_errors += 1
        print(f"[METRICS] {operation.value} error #{self.error_counts[operation.value]}")

    def print_report(self):
        """Print error statistics report."""
        print("\n错误统计报告:")
        print("-" * 40)
        for op, count in self.error_counts.items():
            if count > 0:
                print(f"  {op}: {count} 次")
        print(f"  总计: {self.total_errors} 次")


def example_metrics():
    """Example with error metrics collection."""
    print("=" * 60)
    print("Example 3: 错误统计收集")
    print("=" * 60)

    metrics = MetricsCollector()

    print("\n模拟多次操作失败:")
    for i in range(3):
        try:
            metrics.client.tap(999, 0.5)
        except ADBError:
            pass

    metrics.print_report()


# ============================================================================
# Main
# ============================================================================

if __name__ == "__main__":
    # Run examples
    example_default_logging()
    print("\n" + "=" * 60 + "\n")

    example_retry_logic()
    print("\n" + "=" * 60 + "\n")

    example_metrics()
