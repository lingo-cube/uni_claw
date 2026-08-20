#!/usr/bin/env python3
"""
csharp-mcp-query.py — reliable C# semantic retrieval via the local .NET MCP servers.

Bridges the two project MCP servers (cwm-roslyn-navigator / csharper-mcp) over the
MCP stdio JSON-RPC protocol, so C# retrieval goes through real Roslyn semantic
navigation instead of grep. Injected env (DOTNET_ROOT / DOTNET_MULTILEVEL_LOOKUP)
is required for the .NET global tools to run under DSH, which scrubs
credential-shaped env on spawn.

Usage:
  python3 tools/csharp-mcp-query.py find_symbol --name IsResolvedParentReturnControl
  python3 tools/csharp-mcp-query.py find_references --symbolName IsResolvedParentReturnControl
  python3 tools/csharp-mcp-query.py get_diagnostics --scope Solution --path Test project?Active
  python3 tools/csharp-mcp-query.py tools            # list available MCP tools
"""
from __future__ import annotations

import json
import os
import subprocess
import sys
import time
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
SOLUTION = REPO_ROOT / "src" / "UniClaw.Runtime.sln"

# Two candidate server binaries (either may be present).
SERVERS = {
    "navigator": ("cwm-roslyn-navigator", ["--solution", str(SOLUTION)]),
    "csharper": ("csharper-mcp", ["--workspace-from-cwd"]),
}

SCRIPTED_ENV = {
    **os.environ,
    "DOTNET_ROOT": os.environ.get("DOTNET_ROOT", os.path.expanduser("~/.dotnet")),
    "DOTNET_MULTILEVEL_LOOKUP": "0",
    "PATH": f'{os.environ.get("DOTNET_ROOT", os.path.expanduser("~/.dotnet"))}:{os.environ.get("PATH", "")}',
}


def _tool_dir():
    return Path(os.environ.get("DOTNET_ROOT", os.path.expanduser("~/.dotnet"))) / "tools"


def _resolve_server(server: str):
    name, args = SERVERS[server]
    exe = _tool_dir() / name
    if not exe.exists():
        raise SystemExit(f"MCP server not found: {exe}. Is '{name}' installed under dotnet tools?")
    return [str(exe), *args]


class McpClient:
    def __init__(self, server: str):
        cmd = _resolve_server(server)
        self.proc = subprocess.Popen(
            cmd, stdin=subprocess.PIPE, stdout=subprocess.PIPE,
            stderr=subprocess.PIPE, text=True, env=SCRIPTED_ENV)
        self._pending = 0

    def send(self, obj: dict):
        self.proc.stdin.write(json.dumps(obj) + "\n")
        self.proc.stdin.flush()

    def initialize(self):
        self.send({"jsonrpc": "2.0", "id": "init",
                   "method": "initialize",
                   "params": {"protocolVersion": "2024-11-05", "capabilities": {},
                              "clientInfo": {"name": "dsh-cs-query", "version": "0"}}})
        self._wait("init")
        self.send({"jsonrpc": "2.0", "method": "notifications/initialized", "params": {}})

    def _wait(self, target_id, timeout: float = 40.0):
        import select
        deadline = time.time() + timeout
        last = None
        while time.time() < deadline:
            rlist, _, _ = select.select([self.proc.stdout], [], [], 0.5)
            if not rlist:
                continue
            line = self.proc.stdout.readline()
            if not line:
                break
            if line.startswith("#") or line.startswith("info:") or line.startswith("warn:"):
                continue
            try:
                msg = json.loads(line)
            except Exception:
                continue
            if msg.get("id") == target_id:
                last = msg
                return last
            # asynchronous server notification (not our response) — ignore
            if msg.get("method"):
                continue
            # a message with a different id or no id that isn't noise — ignore
            continue
        return last

    def call(self, tool: str, args: dict, timeout: float = 60.0):
        self.send({"jsonrpc": "2.0", "id": "call",
                   "method": "tools/call",
                   "params": {"name": tool, "arguments": args}})
        return self._wait("call", timeout)

    def close(self):
        try:
            self.proc.stdin.close()
            self.proc.wait(timeout=3)
        except Exception:
            self.proc.kill()


def _dump_result(resp):
    if resp is None:
        print("(no response / workspace still loading)")
        return
    if "error" in resp:
        print("ERROR:", resp["error"])
        return
    for block in resp.get("result", {}).get("content", []):
        text = block.get("text")
        if text is not None:
            print(text)
        elif "structuredContent" in block:
            print(json.dumps(block["structuredContent"], indent=2)[:4000])


def main():
    server = "navigator"
    argv = sys.argv[1:]
    if not argv:
        print(__doc__)
        return
    if argv[0] == "tools":
        c = McpClient(server)
        c.initialize()
        resp = c.call("tools/list", {})
        if resp and "result" in resp:
            for t in resp["result"]["tools"]:
                print(t["name"], "| schema:", list(t.get("inputSchema", {}).get("properties", {}).keys()))
        c.close()
        return

    tool = argv[0]
    args: dict = {}
    i = 1
    while i < len(argv):
        key = argv[i].lstrip("-")
        if i + 1 < len(argv) and not argv[i + 1].startswith("--"):
            args[key] = argv[i + 1]
            i += 2
        else:
            args[key] = True
            i += 1

    c = McpClient(server)
    try:
        c.initialize()
        # Roslyn workspace may still be loading on first call; retry a few times.
        # Roslyn cold workspace load can take ~30-60s on first query; wait it out
        # with backoff up to ~90s, then call once.
        # Roslyn cold workspace load takes ~30-60s on first query. Retry on the
        # SAME connection until a real result returns (treat transient None and
        # "Workspace is loading" responses as "not ready yet"), up to ~90s.
        def _content_of(resp):
            try:
                for block in resp["result"].get("content", []):
                    if block.get("text"):
                        return block["text"]
            except Exception:
                pass
            return json.dumps(resp or "")

        result_shown = False
        for attempt in range(12):
            resp = c.call(tool, args, timeout=30)
            content = _content_of(resp)
            lower = content.lower()
            if resp is None or "workspace is loading" in lower or "error" in lower:
                delay = min(3 * (attempt + 1), 12)
                print(f"(workspace loading; waiting {delay}s, attempt {attempt+1})", file=sys.stderr)
                time.sleep(delay)
                continue
            _dump_result(resp)
            result_shown = True
            break
        if not result_shown:
            sys.stderr.write(f"Workspace did not become ready; no result for '{tool}'.\n")
    finally:
        c.close()


if __name__ == "__main__":
    main()
