"""Main entry point for uni-claw traversal demo."""

import argparse
import logging
import sys
from pathlib import Path

# Add parent directory to path for imports
sys.path.insert(0, str(Path(__file__).parent.parent))

from src.adb.adb_client import MockADBClient, RealADBClient
from src.config import get_settings
from src.state.state_manager import StateManager
from src.traversal import TraversalConfig, TraversalEngine
from src.vision import (
    ClaudeVisionService,
    MiMoVisionService,
    MiMoVisionServiceFactory,
    MiMoCCVisionService,
    MiMoCCVisionServiceFactory,
    MockVisionService,
)

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s - %(name)s - %(levelname)s - %(message)s",
)
logger = logging.getLogger(__name__)


def print_event(event):
    """Print traversal events to console."""
    print(f"\n[EVENT] {event}")


def create_vision_service(provider: str, settings):
    """Create vision service based on provider.

    Args:
        provider: 'anthropic', 'mimo', 'mimo-cc', or 'mock'
        settings: Application settings

    Returns:
        VisionService instance
    """
    if provider == "mock":
        logger.info("Using Mock vision service")
        return MockVisionService()

    elif provider == "anthropic":
        logger.info(f"Using Anthropic Claude ({settings.vision_model})")
        if not settings.anthropic_api_key:
            logger.error("ANTHROPIC_API_KEY not set")
            sys.exit(1)
        return ClaudeVisionService(
            api_key=settings.anthropic_api_key,
            model=settings.vision_model,
        )

    elif provider == "mimo":
        logger.info(f"Using XiaoMi MiMo v1 ({settings.mimo_model})")
        return MiMoVisionServiceFactory.from_settings(settings)

    elif provider == "mimo-cc":
        logger.info(f"Using XiaoMi MiMo CC /anthropic ({settings.mimo_cc_model})")
        return MiMoCCVisionServiceFactory.from_settings(settings)

    else:
        logger.error(f"Unknown vision provider: {provider}")
        sys.exit(1)


def main():
    """Main entry point."""
    parser = argparse.ArgumentParser(description="Uni-claw: AI mobile UI traversal")
    parser.add_argument("target", help="Target app name to traverse")
    parser.add_argument("--mock", action="store_true", help="Use mock clients for testing")
    parser.add_argument("--reset", action="store_true", help="Reset traversal state")
    parser.add_argument("--device", help="ADB device ID")
    parser.add_argument("--model", help="Vision model name")
    parser.add_argument(
        "--vision-provider",
        choices=["anthropic", "mimo", "mimo-cc", "mock"],
        help="Vision service provider (default: from settings or 'anthropic')",
    )

    args = parser.parse_args()

    # Load settings
    settings = get_settings()

    # Override with CLI args
    if args.device:
        settings.adb_device_id = args.device
    if args.model:
        settings.vision_model = args.model

    # Determine vision provider
    vision_provider = args.vision_provider or settings.vision_provider
    if args.mock:
        vision_provider = "mock"

    # Initialize state manager
    state_manager = StateManager(settings.state_file)

    if args.reset:
        logger.info("Resetting traversal state...")
        state_manager.reset()

    # Create clients
    if args.mock:
        logger.info("Using mock ADB client (demo mode)")
        adb = MockADBClient()
    else:
        logger.info("Using real ADB client")
        adb = RealADBClient(
            adb_path=settings.adb_path,
            device_id=settings.adb_device_id or None,
        )

        if not adb.is_connected():
            logger.error("No ADB device connected. Please connect a device.")
            sys.exit(1)

    # Create vision service
    vision = create_vision_service(vision_provider, settings)

    # Create traversal engine
    config = TraversalConfig(
        max_steps=settings.max_steps,
        wait_time=settings.wait_time,
        max_retries=settings.max_retries,
    )

    engine = TraversalEngine(
        adb_client=adb,
        vision_service=vision,
        state=state_manager.state,
        config=config,
        event_callback=print_event,
    )

    try:
        # Navigate to target app
        logger.info(f"Navigating to '{args.target}'...")
        if not engine.navigate_to_app(args.target):
            logger.error(f"Failed to find app: {args.target}")
            sys.exit(1)

        # Initialize structure
        logger.info("Analyzing initial structure...")
        if not engine.initialize_structure():
            logger.error("Failed to initialize structure")
            sys.exit(1)

        # Run traversal
        logger.info("Starting traversal...")
        summary = engine.run()

        # Print results
        print("\n" + "=" * 50)
        print("TRAVERSAL COMPLETE")
        print("=" * 50)
        print(f"Total steps: {summary['total_steps']}")
        print(f"Elapsed time: {summary['elapsed_time']:.1f}s")
        print(f"Visited items: {summary['visited_count']}")
        print(f"Final path: {summary['final_path']}")
        print("\nContent Tree:")
        print("-" * 50)
        print(summary['tree'])

        # Save state
        state_manager.save()

    except KeyboardInterrupt:
        logger.info("\nTraversal interrupted by user")
        state_manager.save()
        sys.exit(0)
    except Exception as e:
        logger.error(f"Traversal failed: {e}", exc_info=True)
        sys.exit(1)


if __name__ == "__main__":
    main()
