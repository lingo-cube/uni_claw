#!/usr/bin/env python3
"""Loopback-only static read surface, rooted in this tool's directory."""
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
import argparse

class ReadOnlyHandler(SimpleHTTPRequestHandler):
    def end_headers(self):
        self.send_header('Cache-Control', 'no-cache')
        self.send_header('X-Content-Type-Options', 'nosniff')
        super().end_headers()

    def do_GET(self):
        # Reject symlink escapes as well as ordinary parent traversal.
        resolved = Path(self.translate_path(self.path)).resolve()
        if not resolved.is_relative_to(Path(self.directory).resolve()):
            self.send_error(403)
            return
        super().do_GET()

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description='UniClaw World 只读可视化')
    parser.add_argument('--port', type=int, default=8765)
    args = parser.parse_args()
    root = Path(__file__).resolve().parent
    server = ThreadingHTTPServer(('127.0.0.1', args.port), partial(ReadOnlyHandler, directory=str(root)))
    print(f'World 可视化：http://127.0.0.1:{args.port}（只读，Ctrl+C 结束）', flush=True)
    server.serve_forever()
