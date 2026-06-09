"""MCP Client for calling MCP tools directly.

This module provides a client for calling MCP tools using the MCP Python SDK.
"""

import asyncio
import base64
import json
import logging
from typing import Any, Dict, Optional
from pathlib import Path

try:
    from mcp import ClientSession, StdioServerParameters
    from mcp.client.stdio import stdio_client
    MCP_AVAILABLE = True
except ImportError:
    MCP_AVAILABLE = False
    logging.warning("MCP SDK not installed. Install with: pip install mcp")

logger = logging.getLogger(__name__)


class MCPClient:
    """Client for calling MCP tools."""

    def __init__(self, server_command: str, server_args: list = None):
        """Initialize MCP client.

        Args:
            server_command: Command to start the MCP server
            server_args: Arguments for the server command
        """
        if not MCP_AVAILABLE:
            raise ImportError("MCP SDK is not installed. Run: pip install mcp")

        self.server_command = server_command
        self.server_args = server_args or []
        self.session: Optional[ClientSession] = None
        self._initialized = False

    async def __aenter__(self):
        """Enter context manager and connect to server."""
        await self.connect()
        return self

    async def __aexit__(self, exc_type, exc_val, exc_tb):
        """Exit context manager and close connection."""
        await self.close()

    async def connect(self):
        """Connect to the MCP server."""
        if self._initialized:
            return

        server_params = StdioServerParameters(
            command=self.server_command,
            args=self.server_args,
        )

        stdio_transport = await stdio_client(server_params)
        self.session = ClientSession(stdio_transport[0], stdio_transport[1])

        # Initialize the session
        await self.session.initialize()
        self._initialized = True

        logger.info(f"Connected to MCP server: {self.server_command}")

    async def close(self):
        """Close the connection to the MCP server."""
        if self.session:
            await self.session.close()
            self._initialized = False

    async def list_tools(self) -> list:
        """List available tools from the MCP server.

        Returns:
            list: List of available tools
        """
        if not self._initialized:
            await self.connect()

        result = await self.session.list_tools()
        return result.tools

    async def call_tool(
        self,
        tool_name: str,
        arguments: Dict[str, Any],
        read_timeout_seconds: int = 60,
    ) -> Any:
        """Call a tool on the MCP server.

        Args:
            tool_name: Name of the tool to call
            arguments: Arguments for the tool
            read_timeout_seconds: Timeout for reading response

        Returns:
            Tool result
        """
        if not self._initialized:
            await self.connect()

        logger.info(f"Calling MCP tool: {tool_name}")

        result = await self.session.call_tool(
            name=tool_name,
            arguments=arguments,
            read_timeout_seconds=read_timeout_seconds,
        )

        return result

    async def analyze_image(
        self,
        image_path: str,
        prompt: str = "Analyze this image",
    ) -> str:
        """Analyze an image using MCP vision tool.

        This is a convenience method for calling image analysis tools.
        The actual tool name and parameters depend on the MCP server.

        Args:
            image_path: Path to the image file
            prompt: Analysis prompt

        Returns:
            str: Analysis result
        """
        # Read and encode image
        with open(image_path, "rb") as f:
            image_data = f.read()

        image_base64 = base64.b64encode(image_data).decode("utf-8")
        image_url = f"data:image/jpeg;base64,{image_base64}"

        # Try common tool names for image analysis
        tool_names = ["analyze_image", "vision_analyze", "analyze"]

        for tool_name in tool_names:
            try:
                result = await self.call_tool(
                    tool_name=tool_name,
                    arguments={
                        "imageSource": image_url,
                        "prompt": prompt,
                    },
                )
                return str(result)
            except Exception as e:
                logger.debug(f"Tool {tool_name} failed: {e}")
                continue

        raise RuntimeError("No image analysis tool available on this MCP server")


class MCPVisionClient:
    """High-level client for MCP vision analysis."""

    def __init__(self):
        """Initialize the vision client."""
        if not MCP_AVAILABLE:
            raise ImportError("MCP SDK is not installed")

        # Common MCP servers for vision analysis
        # These would need to be configured based on your setup
        self.client: Optional[MCPClient] = None
        self.server_config = self._detect_server_config()

    def _detect_server_config(self) -> Optional[Dict[str, Any]]:
        """Detect available MCP server configuration.

        This method checks for common MCP server configurations.
        In production, this would read from a config file.
        """
        # Check if server config is available in environment
        import os

        config_path = os.environ.get("MCP_SERVER_CONFIG")
        if config_path and Path(config_path).exists():
            with open(config_path) as f:
                return json.load(f)

        # Default configurations for common MCP servers
        # These would need to be adjusted based on your setup
        return None

    async def analyze(
        self,
        image_path: str,
        prompt: str = "Analyze this image",
    ) -> str:
        """Analyze an image.

        Args:
            image_path: Path to image file
            prompt: Analysis prompt

        Returns:
            str: Analysis result
        """
        if self.client is None:
            raise RuntimeError(
                "MCP client not configured. Please set MCP_SERVER_CONFIG "
                "or configure a server manually."
            )

        async with self.client:
            return await self.client.analyze_image(image_path, prompt)


# Test function
async def test_mcp_connection():
    """Test MCP client connection."""
    print("Testing MCP client...")

    # This would require an actual MCP server to be running
    # For now, just test that the SDK is available
    if MCP_AVAILABLE:
        print("MCP SDK is available")
        print("Note: To actually use MCP tools, you need to:")
        print("1. Have an MCP server running")
        print("2. Configure the server command and args")
        print("3. Call tools using the client")
    else:
        print("MCP SDK is not available")


if __name__ == "__main__":
    asyncio.run(test_mcp_connection())
