#!/usr/bin/env python3
"""Detach an emulator from the provisioning shell and keep its lease alive.

The provisioning script may run under a task runner that reaps background
children when the shell exits. The emulator itself therefore starts a new
session; this small supervisor only waits for it and returns its exit code.
"""

from __future__ import annotations

import os
import signal
import subprocess
import sys


def main() -> int:
    if len(sys.argv) < 2:
        print("usage: emulator-supervisor.py <emulator> [args...]", file=sys.stderr)
        return 2

    child = subprocess.Popen(
        sys.argv[1:],
        stdin=subprocess.DEVNULL,
        start_new_session=True,
    )

    def forward(signum: int, _frame: object) -> None:
        if child.poll() is None:
            try:
                os.killpg(child.pid, signum)
            except ProcessLookupError:
                pass

    signal.signal(signal.SIGTERM, forward)
    signal.signal(signal.SIGINT, forward)
    return child.wait()


if __name__ == "__main__":
    raise SystemExit(main())
