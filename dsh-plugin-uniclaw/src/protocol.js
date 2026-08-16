/**
 * Wire contract for the UniClaw DriverHost transport (protocol baseline §9):
 * loopback TCP, newline-delimited JSON-RPC. Pure helpers — encoding, line
 * framing, typed error mapping. No I/O, no state.
 */

export const PROTOCOL_VERSION = 1;
export const SERVICE_NAME = 'dsh-uniclaw-driverhost';
export const BASELINE_CHANGE = 'dsh-uniclaw-control-plane-protocol-baseline';

/** Typed protocol error codes (frozen set). Server: bad_request / unknown_method / internal_error. Client-side: driverhost_disconnected. */
export const ERROR_CODES = Object.freeze({
  BAD_REQUEST: 'bad_request',
  UNKNOWN_METHOD: 'unknown_method',
  INTERNAL_ERROR: 'internal_error',
  DRIVERHOST_DISCONNECTED: 'driverhost_disconnected',
});

/** Typed protocol error carrying the frozen code plus a human-readable message. */
export class UniClawRpcError extends Error {
  constructor(code, message, options) {
    super(message, options);
    this.name = 'UniClawRpcError';
    this.code = code;
  }
}

/** Encode one request as a newline-terminated JSON line. */
export function encodeRequest(id, method, params) {
  const payload = { jsonrpc: '2.0', id, method };
  if (params !== undefined) payload.params = params;
  return JSON.stringify(payload) + '\n';
}

/** Parse one response line; throws UniClawRpcError on a typed error response. */
export function parseResponseLine(line) {
  let parsed;
  try {
    parsed = JSON.parse(line);
  } catch (err) {
    throw new UniClawRpcError(ERROR_CODES.INTERNAL_ERROR, `malformed response line: ${err.message}`);
  }
  if (parsed && typeof parsed === 'object' && parsed.error) {
    throw new UniClawRpcError(parsed.error.code, parsed.error.message || 'protocol error');
  }
  return parsed?.result;
}
