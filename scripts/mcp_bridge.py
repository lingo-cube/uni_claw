"""MCP Bridge Server for uni-claw

This server provides HTTP access to MCP tools that are available in Claude Code.
Run this script directly in Claude Code to expose MCP tools via HTTP.

Usage (in Claude Code):
    python scripts/mcp_bridge.py

Then from your Python code:
    import requests
    response = requests.post("http://127.0.0.1:8765/mcp/analyze_image", json={
        "image_data": "base64_encoded_image",
        "prompt": "Analyze this image"
    })
"""

import asyncio
import base64
import json
import logging
import os
import sys
from pathlib import Path
from aiohttp import web
from typing import Dict, Any, Optional

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
)
logger = logging.getLogger(__name__)


class MCPBridgeServer:
    """Bridge server that exposes MCP tools via HTTP.

    This server runs within Claude Code environment and provides HTTP endpoints
    that external Python code can call to use MCP tools.
    """

    def __init__(self, host: str = "127.0.0.1", port: int = 8765):
        self.host = host
        self.port = port
        self.app = web.Application()
        self._setup_routes()

    def _setup_routes(self):
        """Setup HTTP routes."""
        self.app.router.add_post("/mcp/analyze_image", self.handle_analyze_image)
        self.app.router.add_get("/health", self.handle_health)
        self.app.router.add_get("/", self.handle_info)

    async def handle_info(self, request):
        """Server info endpoint."""
        return web.json_response({
            "server": "mcp-bridge",
            "version": "1.0",
            "description": "Bridge server for MCP tools in Claude Code",
            "endpoints": {
                "/health": "Health check",
                "/mcp/analyze_image": "Analyze image using MCP 4.5v vision tool",
                "/": "This info page"
            },
            "note": "This server bridges Claude Code MCP tools to HTTP for external Python code"
        })

    async def handle_health(self, request):
        """Health check endpoint."""
        return web.json_response({
            "status": "ok",
            "server": "mcp-bridge"
        })

    async def handle_analyze_image(self, request):
        """Handle image analysis using MCP tool.

        This endpoint receives image data and prompt, then calls the MCP
        analyze_image tool that's available in Claude Code environment.
        """
        try:
            data = await request.json()
            image_data = data.get("image_data", "")
            prompt = data.get("prompt", "Analyze this image")
            image_source = data.get("image_source", "")

            logger.info(f"Received analyze_image request, prompt: {prompt[:50]}...")

            # Determine how to provide the image
            if image_source:
                # Caller provided a URL
                result = await self.call_mcp_with_url(image_source, prompt)
            elif image_data:
                # Caller provided base64 data
                result = await self.call_mcp_with_base64(image_data, prompt)
            else:
                return web.json_response({
                    "error": "Either image_data or image_source must be provided",
                    "success": False
                }, status=400)

            return web.json_response(result)

        except Exception as e:
            logger.error(f"Error in handle_analyze_image: {e}", exc_info=True)
            return web.json_response({
                "error": str(e),
                "success": False,
                "error_type": type(e).__name__
            }, status=500)

    async def call_mcp_with_base64(self, image_data: str, prompt: str) -> Dict[str, Any]:
        """Call MCP tool with base64 image data.

        Args:
            image_data: Base64 encoded image (with or without data URL prefix)
            prompt: Analysis prompt

        Returns:
            Dict with analysis result
        """
        # Clean base64 data if it has data URL prefix
        if image_data.startswith("data:image"):
            image_data = image_data.split(",", 1)[1]

        # Create data URL for MCP tool
        data_url = f"data:image/png;base64,{image_data}"

        return await self._execute_mcp_analysis(data_url, prompt)

    async def call_mcp_with_url(self, image_source: str, prompt: str) -> Dict[str, Any]:
        """Call MCP tool with image URL.

        Args:
            image_source: URL to the image
            prompt: Analysis prompt

        Returns:
            Dict with analysis result
        """
        return await self._execute_mcp_analysis(image_source, prompt)

    async def _execute_mcp_analysis(self, image_source: str, prompt: str) -> Dict[str, Any]:
        """Execute MCP image analysis.

        NOTE: This is a stub implementation. The actual MCP tool call would
        need to be made through the Agent SDK framework or MCP client library.

        For a complete implementation, you would need to:
        1. Import the MCP client library
        2. Connect to the MCP server
        3. Call the analyze_image tool

        Current limitation: This bridge server is a template showing the
        architecture. The actual MCP tool integration requires access to
        the MCP client that's embedded in Claude Code.
        """
        logger.warning("Using mock implementation - actual MCP tool integration needed")
        logger.info(f"Would call MCP tool with image_source: {image_source[:50]}...")

        # Mock response for testing the HTTP endpoint
        # In production, this would call the actual MCP tool
        return {
            "success": True,
            "content": json.dumps({
                "level1_dir": "top",
                "level1_menus": [
                    {"name": "通用", "coordinate": {"x": 0.25, "y": 0.08}, "active": True},
                    {"name": "声音", "coordinate": {"x": 0.45, "y": 0.08}, "active": False},
                    {"name": "显示", "coordinate": {"x": 0.65, "y": 0.08}, "active": False},
                    {"name": "网络", "coordinate": {"x": 0.85, "y": 0.08}, "active": False}
                ],
                "level2_dir": "left",
                "level2_menus": [],
                "current_path": ["通用"],
                "items": [
                    {
                        "name": "温度单位",
                        "type": "toggle",
                        "coordinate": {"x": 0.15, "y": 0.2},
                        "parent": None,
                        "description": "温度显示单位",
                        "expected_action": "toggle",
                        "expects_page_change": False,
                        "expects_state_change": True
                    },
                    {
                        "name": "时间设置",
                        "type": "menu_item",
                        "coordinate": {"x": 0.15, "y": 0.3},
                        "parent": None,
                        "description": "系统时间设置",
                        "expected_action": "navigate",
                        "expects_page_change": True,
                        "expects_state_change": False
                    },
                    {
                        "name": "语言",
                        "type": "menu_item",
                        "coordinate": {"x": 0.15, "y": 0.4},
                        "parent": None,
                        "description": "当前: 简体中文",
                        "expected_action": "navigate",
                        "expects_page_change": True,
                        "expects_state_change": False
                    }
                ],
                "is_popup": False,
                "popup_info": None,
                "close_button": None,
                "back_button": {"x": 0.05, "y": 0.08},
                "has_scroll": True,
                "is_end_of_list": False
            }, ensure_ascii=False),
            "provider": "mcp-bridge",
            "note": "This is a mock response. Actual MCP tool integration requires access to Claude Code's MCP client."
        }

    def run(self):
        """Start the server."""
        logger.info(f"Starting MCP Bridge Server on http://{self.host}:{self.port}")
        logger.info("Endpoints available:")
        logger.info(f"  - http://{self.host}:{self.port}/health")
        logger.info(f"  - http://{self.host}:{self.port}/mcp/analyze_image")
        logger.info("")
        logger.info("Your Python code can now call MCP tools via HTTP!")
        logger.info("")
        logger.info("Example curl command:")
        logger.info(f'  curl -X POST http://{self.host}:{self.port}/mcp/analyze_image \\')
        logger.info('    -H "Content-Type: application/json" \\')
        logger.info('    -d \'{"image_data": "base64_encoded_image", "prompt": "Analyze this"}\'')
        logger.info("")

        web.run_app(self.app, host=self.host, port=self.port)


def main():
    """Main entry point."""
    import argparse

    parser = argparse.ArgumentParser(
        description="MCP Bridge Server for Claude Code",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  python mcp_bridge.py                    # Run on default port 8765
  python mcp_bridge.py --port 9000        # Run on custom port
  python mcp_bridge.py --host 0.0.0.0     # Listen on all interfaces

This server provides HTTP access to MCP tools available in Claude Code.
Run this script within Claude Code to enable external Python code to use MCP tools.
        """
    )

    parser.add_argument("--host", default="127.0.0.1", help="Host to bind to (default: 127.0.0.1)")
    parser.add_argument("--port", type=int, default=8765, help="Port to bind to (default: 8765)")

    args = parser.parse_args()

    server = MCPBridgeServer(host=args.host, port=args.port)
    server.run()


if __name__ == "__main__":
    main()
