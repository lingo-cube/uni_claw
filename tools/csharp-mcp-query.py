#!/usr/bin/env python3
"""
csharp-mcp-query.py — reliable C# semantic retrieval via the local .NET MCP servers.

Bridges the two project MCP servers (cwm-roslyn-navigator / csharper-mcp) over the
MCP stdio JSON-RPC protocol, so C# retrieval goes through real Roslyn semantic
navigation instead of grep. Injected env (DOTNET_ROOT / DOTNET_MULTILEVEL_LOOKUP)
is required for the .NET global tools to run under DSH, which scrubs
credential-shaped env on spawn.

A persistent --daemon keeps the Roslyn workspace loaded so repeated queries are
near-instant instead of paying a 30-60s cold workspace load per call.

Usage:
  # one-time / first call (slow: cold Roslyn load)
  python3 tools/csharp-mcp-query.py find_symbol --name IsResolvedParentReturnControl

  # after start, subsequent calls hit the warm daemon (fast)
  python3 tools/csharp-mcp-query.py daemon-start
  python3 tools/csharp-mcp-query.py find_symbol --name IsResolvedParentReturnControl
  python3 tools/csharp-mcp-query.py daemon-stop

  python3 tools/csharp-mcp-query.py find_references --symbolName IsResolvedParentReturnControl
  python3 tools/csharp-mcp-query.py get_diagnostics --scope File --path src/UniClaw.Runtime/Agent/Agent.OpenWorld.cs
  python3 tools/csharp-mcp-query.py tools
"""
from __future__ import annotations

import json
import os
import select
import socket
import subprocess
import sys
import threading
import time
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
SOLUTION = REPO_ROOT / "src" / "UniClaw.Runtime.sln"
SOCKET_PATH = "/tmp/csharp-mcp-daemon.sock"
PID_PATH = "/tmp/csharp-mcp-daemon.pid"

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

DEFAULT_SERVER = "navigator"


def _tool_dir():
    return Path(os.environ.get("DOTNET_ROOT", os.path.expanduser("~/.dotnet"))) / "tools"


def _resolve_server(server: str):
    name, args = SERVERS[server]
    exe = _tool_dir() / name
    if not exe.exists():
        raise SystemExit(f"MCP server not found: {exe}. Is '{name}' installed under dotnet tools?")
    return [str(exe), *args]


# ─────────────────────────────────────────────────────────────────────────
# Direct (one-shot) client — used when no daemon is running / first call.
# ─────────────────────────────────────────────────────────────────────────

class McpClient:
    def __init__(self, server: str):
        cmd = _resolve_server(server)
        self.proc = subprocess.Popen(
            cmd, stdin=subprocess.PIPE, stdout=subprocess.PIPE,
            stderr=subprocess.PIPE, text=True, env=SCRIPTED_ENV)

    def send(self, obj: dict):
        self.proc.stdin.write(json.dumps(obj) + "\n")
        self.proc.stdin.flush()

    def initialize(self):
        self.send({"jsonrpc": "2.0", "id": "init",
                   "method": "initialize",
                   "params": {"protocolVersion": "2024-11-05", "capabilities": {},
                              "clientInfo": {"name": "dsh-cs-query", "version": "0"}}})
        self._wait("init", 60)
        self.send({"jsonrpc": "2.0", "method": "notifications/initialized", "params": {}})

    def _wait(self, target_id, timeout: float = 60.0):
        import select
        deadline = time.time() + timeout
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
                return msg
            if msg.get("method"):
                continue
        return None

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


# ─────────────────────────────────────────────────────────────────────────
# Daemon: one persistent MCP server child, serialized JSON-RPC over a Unix
# socket. Workspace loads once; warm queries are near-instant.
# ─────────────────────────────────────────────────────────────────────────

class _DaemonServer:
    """Owns the MCP server child process and serializes JSON-RPC on one line-
    framed stdio channel. Holds the initialized connection forever."""

    def __init__(self, server: str):
        cmd = _resolve_server(server)
        self.proc = subprocess.Popen(
            cmd, stdin=subprocess.PIPE, stdout=subprocess.PIPE,
            stderr=subprocess.PIPE, text=True, env=SCRIPTED_ENV)
        self._lock = threading.Lock()
        self._init()

    def _init(self):
        self.proc.stdin.write(json.dumps({
            "jsonrpc": "2.0", "id": "init", "method": "initialize",
            "params": {"protocolVersion": "2024-11-05", "capabilities": {},
                       "clientInfo": {"name": "dsh-cs-daemon", "version": "0"}}}) + "\n")
        self.proc.stdin.flush()
        self._wait("init", 90)
        self.proc.stdin.write(json.dumps({
            "jsonrpc": "2.0", "method": "notifications/initialized", "params": {}}) + "\n")
        self.proc.stdin.flush()
        # warm the workspace: fire one no-op listTools and swallow the loading replies
        for _ in range(4):
            self.call("tools/list", {}, timeout=20)
            time.sleep(0.5)

    def _wait(self, target_id, timeout=60.0):
        deadline = time.time() + timeout
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
                return msg
        return None

    def call(self, tool: str, args: dict, timeout: float = 60.0):
        with self._lock:
            self.proc.stdin.write(json.dumps({
                "jsonrpc": "2.0", "id": "call", "method": "tools/call",
                "params": {"name": tool, "arguments": args}}) + "\n")
            self.proc.stdin.flush()
            return self._wait("call", timeout)


def _is_daemon_running():
    if not os.path.exists(SOCKET_PATH):
        return False
    # pid check
    try:
        pid = int(open(PID_PATH).read().strip())
        os.kill(pid, 0)
    except Exception:
        return False
    return True


def _daemon_rpc(tool: str, args: dict, timeout: float = 60.0):
    """Send one JSON-RPC line to the running daemon socket, read the reply."""
    client = socket.socket(socket.AF_UNIX, socket.SOCK_STREAM)
    client.settimeout(timeout)
    client.connect(SOCKET_PATH)
    request = {"jsonrpc": "2.0", "id": "call",
               "method": "tools/call",
               "params": {"name": tool, "arguments": args}}
    client.sendall((json.dumps(request) + "\n").encode("utf-8"))
    buf = b""
    while True:
        chunk = client.recv(65536)
        if not chunk:
            break
        buf += chunk
        try:
            msg = json.loads(buf.decode("utf-8"))
            if msg.get("id") == "call":
                client.close()
                return msg
        except Exception:
            continue  # incomplete line
    client.close()
    return None


def daemon_start(server: str = DEFAULT_SERVER):
    if _is_daemon_running():
        print("daemon already running")
        return
    d = _DaemonServer(server)
    server_sock = socket.socket(socket.AF_UNIX, socket.SOCK_STREAM)
    if os.path.exists(SOCKET_PATH):
        os.unlink(SOCKET_PATH)
    server_sock.bind(SOCKET_PATH)
    server_sock.listen(8)
    if os.path.exists(PID_PATH):
        os.unlink(PID_PATH)
    open(PID_PATH, "w").write(str(os.getpid()))
    pid = os.fork()
    if pid > 0:
        # parent exits; child runs the loop
        os._exit(0)

    # child (daemon) — record the REAL daemon pid here, not the parent's.
    if os.path.exists(PID_PATH):
        os.unlink(PID_PATH)
    open(PID_PATH, "w").write(str(os.getpid()))

    while True:
        try:
            conn, _ = server_sock.accept()
        except OSError:
            break
        # read one line request
        data = b""
        conn.settimeout(5)
        try:
            while b"\n" not in data:
                chunk = conn.recv(65536)
                if not chunk:
                    break
                data += chunk
            req = json.loads(data.decode("utf-8"))
            resp = d.call(req["params"]["name"], req["params"]["arguments"], timeout=120)
            payload = (json.dumps(resp if resp is not None else {}) + "\n").encode("utf-8")
            conn.sendall(payload)
        except Exception:
            pass
        finally:
            conn.close()


def daemon_stop():
    try:
        pid = int(open(PID_PATH).read().strip())
        os.kill(pid, 9)
        os.unlink(SOCKET_PATH)
        if os.path.exists(PID_PATH):
            os.unlink(PID_PATH)
        print("daemon stopped")
    except Exception as e:
        print(f"cannot stop daemon: {e}")


# ─────────────────────────────────────────────────────────────────────────

def main():
    argv = sys.argv[1:]
    if not argv:
        print(__doc__)
        return

    if argv[0] == "daemon-start":
        daemon_start(argv[1] if len(argv) > 1 else DEFAULT_SERVER)
        sys.exit(0)
    if argv[0] == "daemon-stop":
        daemon_stop()
        sys.exit(0)

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

    # If a warm daemon is up, route through it (fast). Else do a one-shot call.
    if _is_daemon_running():
        resp = _daemon_rpc(tool, args)
        _dump_result(resp)
        return

    # one-shot (cold) path
    c = McpClient(DEFAULT_SERVER)
    try:
        c.initialize()
        # cold Roslyn workspace load can take 30-60s on first query; retry on the
        # SAME connection until a real result (treat transient None / "loading"
        # as not-ready-yet), up to ~100s.
        result_shown = False
        for attempt in range(12):
            resp = c.call(tool, args, timeout=30)
            content = ""
            try:
                for block in resp["result"].get("content", []):
                    if block.get("text"):
                        content = block["text"]
                        break
            except Exception:
                content = json.dumps(resp or "")
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
