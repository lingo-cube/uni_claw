#!/usr/bin/env python3
"""VisionHostBehavioralProofs test server fixture (reconstructed).

Contract (per VisionHostBehavioralProofs.cs):
  usage: vh_test_server.py <socket-path> <mode>
  modes: normal | malformed | unsupported | slow | not-ready
  All modes print "READY" (flushed) after binding, then serve HTTP over UDS.
"""
import http.server
import json
import os
import socket
import socketserver
import sys
import time


def main() -> None:
    sock_path, mode = sys.argv[1], sys.argv[2] if len(sys.argv) > 2 else "normal"
    if os.path.exists(sock_path):
        os.unlink(sock_path)

    class Handler(http.server.BaseHTTPRequestHandler):
        def log_message(self, *args):  # silence request logging
            pass

        def _normalize_path(self) -> None:
            # .NET HttpClient with a UDS ConnectCallback emits absolute-form
            # request-targets ("GET http://localhost/version"), so self.path
            # arrives as "http://localhost/version" and would miss the
            # origin-form route matchers → 404. Normalize once on receipt, for
            # both do_GET and do_POST, so routing is uniform.
            import urllib.parse
            if self.path.startswith(("http://", "https://")):
                self.path = urllib.parse.urlparse(self.path).path or "/"

        def _send_json(self, payload: dict, code: int = 200) -> None:
            body = json.dumps(payload).encode()
            self.send_response(code)
            self.send_header("Content-Type", "application/json")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)

        def do_GET(self) -> None:
            self._normalize_path()
            if self.path.startswith("/v1/analyze"):
                # H14: "slow" mode exceeds the 1s client timeout
                if mode == "slow":
                    time.sleep(30)
                    return
                self._send_json({"candidates": [], "metadata": {
                    "schema": "uniclaw.localVisionEvidence.v1"}})
            elif self.path == "/health":
                warm = mode != "not-ready"
                self._send_json({"status": "ok", "warm": warm})
            elif self.path == "/version":
                if mode == "malformed":
                    body = b"{not valid json"
                    self.send_response(200)
                    self.send_header("Content-Type", "application/json")
                    self.send_header("Content-Length", str(len(body)))
                    self.end_headers()
                    self.wfile.write(body)
                elif mode == "unsupported":
                    self._send_json({"supportedSchemas": ["other.schema.v9"]})
                elif mode == "wrong-model":
                    self._send_json({
                        "supportedSchemas": ["uniclaw.localVisionEvidence.v1"],
                        "serviceVersion": "1.0",
                        "modelId": "a" * 64,
                        "configHash": "c" * 64,
                        "configId": "config:expected",
                        "pipelineRevision": "prev:expected",
                        "deploymentId": "deploy:expected"})
                elif mode == "wrong-config":
                    self._send_json({
                        "supportedSchemas": ["uniclaw.localVisionEvidence.v1"],
                        "serviceVersion": "1.0",
                        "modelId": "m" * 64,
                        "configHash": "c" * 64,
                        "configId": "config:WRONG",
                        "pipelineRevision": "prev:expected",
                        "deploymentId": "deploy:expected"})
                elif mode == "wrong-pipeline":
                    self._send_json({
                        "supportedSchemas": ["uniclaw.localVisionEvidence.v1"],
                        "serviceVersion": "1.0",
                        "modelId": "m" * 64,
                        "configHash": "c" * 64,
                        "configId": "config:expected",
                        "pipelineRevision": "prev:WRONG",
                        "deploymentId": "deploy:expected"})
                else:
                    self._send_json({"supportedSchemas": ["uniclaw.localVisionEvidence.v1"],
                                     "serviceVersion": "1.0",
                                     "modelId": "0f72dd1cb7eb798dfc6aeba85076fac9b60631cd84ee1a0a61fdbe2ae08ef9c8",
                                     "configHash": "a85d7e78a27cde2321c64a8d62fab46179242f056f1addb6bf6698839aafddc3"})
            else:
                self.send_response(404)
                self.end_headers()

        def do_POST(self) -> None:
            self._normalize_path()
            if self.path.startswith("/v1/analyze"):
                if mode == "slow":
                    time.sleep(30)  # exceeds the 1s client timeout (H14)
                    return
                self._send_json({"candidates": [], "metadata": {
                    "schema": "uniclaw.localVisionEvidence.v1"}})
            else:
                self.send_response(404)
                self.end_headers()

    class UnixServer(socketserver.ThreadingMixIn,
                     socketserver.UnixStreamServer):
        pass

    with UnixServer(sock_path, Handler) as server:
        print("READY", flush=True)
        server.serve_forever()


if __name__ == "__main__":
    main()
