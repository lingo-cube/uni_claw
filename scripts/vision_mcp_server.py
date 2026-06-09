"""MCP Server for Vision Analysis.

This is a standalone MCP server that provides vision analysis capabilities.
It can be connected to by any MCP-compatible client (including our MCPProvider).

Run with: python scripts/vision_mcp_server.py
"""

import asyncio
import json
import base64
from typing import Any
from mcp.server.models import InitializationOptions
from mcp.server import NotificationOptions, Server
from mcp.types import (
    Resource,
    Tool,
    TextContent,
    ImageContent,
    EmbeddedResource,
)


# Create MCP server instance
server = Server("vision-analysis")

# Store the last analyzed image for reference
last_analysis = None


@server.list_resources()
async def handle_list_resources() -> list[Resource]:
    """List available resources."""
    return [
        Resource(
            uri="vision://status",
            name="Vision Service Status",
            description="Current status of the vision analysis service",
            mimeType="text/plain"
        )
    ]


@server.read_resource()
async def handle_read_resource(uri: str) -> str:
    """Read a resource."""
    if uri == "vision://status":
        return json.dumps({
            "status": "ready",
            "last_analysis": last_analysis
        })
    else:
        raise ValueError(f"Unknown resource: {uri}")


@server.list_tools()
async def handle_list_tools() -> list[Tool]:
    """List available tools."""
    return [
        Tool(
            name="analyze_image",
            description="Analyze an image and return PageAnalysis format",
            inputSchema={
                "type": "object",
                "properties": {
                    "image_data": {
                        "type": "string",
                        "description": "Base64 encoded image data (with or without data URL prefix)"
                    },
                    "prompt": {
                        "type": "string",
                        "description": "Analysis prompt"
                    }
                },
                "required": ["image_data"]
            }
        )
    ]


@server.call_tool()
async def handle_call_tool(name: str, arguments: dict) -> list[TextContent | ImageContent | EmbeddedResource]:
    """Handle tool calls."""
    global last_analysis

    if name == "analyze_image":
        image_data = arguments.get("image_data", "")
        prompt = arguments.get("prompt", "Analyze this image")

        # Decode base64 image
        if image_data.startswith("data:image"):
            image_data = image_data.split(",", 1)[1]

        try:
            image_bytes = base64.b64decode(image_data)
            size_kb = len(image_bytes) / 1024
        except:
            size_kb = 0

        # Perform analysis (in production, call actual vision AI)
        analysis = {
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
        }

        last_analysis = {
            "prompt": prompt,
            "image_size_kb": size_kb,
            "timestamp": asyncio.get_event_loop().time()
        }

        return [
            TextContent(
                type="text",
                text=json.dumps({"success": True, "content": json.dumps(analysis, ensure_ascii=False)})
            )
        ]

    else:
        raise ValueError(f"Unknown tool: {name}")


async def main():
    """Run the MCP server."""
    # Run the server using stdio
    from mcp.server.stdio import stdio_server

    async with stdio_server() as (read_stream, write_stream):
        await server.run(
            read_stream,
            write_stream,
            InitializationOptions(
                server_name="vision-analysis",
                server_version="1.0.0",
                capabilities=server.get_capabilities(
                    notification_options=NotificationOptions(),
                    experimental_capabilities={},
                )
            )
        )


if __name__ == "__main__":
    asyncio.run(main())
