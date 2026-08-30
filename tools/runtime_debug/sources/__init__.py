"""Source adapters for the Debug Query Core.

packet.py  consumes P0 Evidence Packet JSON; bundle.py consumes Harness capture
bundle directories. Both produce contract-limited models consumed by query.py;
neither touches Runtime processes nor mutates inputs.
"""