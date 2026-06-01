#!/usr/bin/env python3
"""Direct Settings Traversal - skip app navigation, start from current Settings screen."""

import argparse
import logging
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent.parent))

from src.adb.adb_client import RealADBClient
from src.config import get_settings
from src.state.state_manager import StateManager
from src.traversal import TraversalConfig, TraversalEngine
from src.vision import MiMoCCVisionServiceFactory

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s - %(name)s - %(levelname)s - %(message)s",
)
logger = logging.getLogger(__name__)


def print_event(event):
    """Print traversal events to console."""
    print(f"\n[EVENT] {event}")


def main():
    """Main entry point for direct Settings traversal."""
    parser = argparse.ArgumentParser(description="Direct Settings Traversal")
    parser.add_argument("--device", help="ADB device ID")
    parser.add_argument("--max-steps", type=int, default=200, help="Maximum traversal steps")
    parser.add_argument("--reset", action="store_true", help="Reset traversal state")

    args = parser.parse_args()
    settings = get_settings()

    if args.device:
        settings.adb_device_id = args.device

    # Initialize state manager
    state_manager = StateManager(settings.state_file)

    if args.reset:
        logger.info("Resetting traversal state...")
        state_manager.reset()

    # Create clients
    logger.info("Using real ADB client")
    adb = RealADBClient(
        adb_path=settings.adb_path,
        device_id=settings.adb_device_id or None,
    )

    if not adb.is_connected():
        logger.error("No ADB device connected.")
        sys.exit(1)

    # Create vision service
    logger.info(f"Using MiMo CC vision service ({settings.mimo_cc_model})")
    vision = MiMoCCVisionServiceFactory.from_settings(settings)

    # Create traversal engine
    config = TraversalConfig(
        max_steps=args.max_steps,
        wait_time=0.5,
        max_retries=2,
    )

    engine = TraversalEngine(
        adb_client=adb,
        vision_service=vision,
        state=state_manager.state,
        config=config,
        event_callback=print_event,
    )

    try:
        # Skip navigation - assume Settings is already open
        logger.info("Starting direct Settings traversal (app should already be open)...")

        # Initialize structure
        logger.info("Analyzing initial Settings structure...")
        if not engine.initialize_structure():
            logger.error("Failed to initialize structure")
            sys.exit(1)

        # Run traversal
        logger.info("Starting Settings traversal...")
        summary = engine.run()

        # Print results
        print("\n" + "=" * 60)
        print("SETTINGS TRAVERSAL COMPLETE")
        print("=" * 60)
        print(f"Total steps: {summary['total_steps']}")
        print(f"Elapsed time: {summary['elapsed_time']:.1f}s")
        print(f"Visited items: {summary['visited_count']}")
        print(f"Final path: {summary['final_path']}")
        print("\nContent Tree:")
        print("-" * 60)
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
