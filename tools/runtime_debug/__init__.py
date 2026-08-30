"""Runtime Debug — P1a read-only, deterministic, stdlib-only packet projections.

Layering (职责清晰 / 可扩展 / 可替换):
- status.py: closed status vocabulary + exit-code mapping (single source of truth).
- envelope.py: canonical result envelope serialization (deterministic output).
- packet.py: source adapter — P0 Evidence Packet v0 reader (fail-closed). A future
  capture-bundle/CLI source only has to produce the same Packet model elsewhere.
- query.py: Query Core — pure, deterministic projections (summarize / occurrence).
  New commands and rules live here; CLI never reimplements logic.
- cli.py: thin argument adapter (argv -> Query Core -> envelope -> exit code).
"""

__all__ = ["status", "envelope", "packet", "query", "cli"]