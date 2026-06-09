"""Demo: Using MCP SDK to connect to MCP server.

This shows how to use the official MCP Python SDK to connect
to an MCP server and call its tools.
"""

import asyncio
import mcp
from mcp import ClientSession, StdioServerParameters


async def connect_to_mcp_server():
    """Connect to an MCP server and list available tools.

    The MCP server needs to be running and accessible via stdio or HTTP.
    """
    # For stdio-based MCP servers
    server_params = StdioServerParameters(
        command="node",  # Or "python" for Python-based servers
        args=["path/to/mcp-server.js"]
    )

    async with ClientSession() as session:
        # Initialize connection
        await session.initialize()

        # List available tools
        tools_result = await session.list_tools()
        print("Available tools:")
        for tool in tools_result.tools:
            print(f"  - {tool.name}: {tool.description}")

        # Call a specific tool
        if tools_result.tools:
            tool_name = tools_result.tools[0].name
            result = await session.call_tool(tool_name, arguments={})
            print(f"Tool result: {result}")


if __name__ == "__main__":
    asyncio.run(connect_to_mcp_server())
