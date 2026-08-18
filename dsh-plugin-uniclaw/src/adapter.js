/**
 * UniClawAdapter — the DSH-side connection boundary over the DriverHost
 * transport. Connects out to the DriverHost-owned loopback listener; caches
 * NOTHING across reconnects (fresh-state guarantee); every request is typed
 * and deterministic. This boundary is read-only by construction: the wire
 * carries no Kernel-mutating method.
 */
import net from 'node:net';
import { encodeRequest, parseResponseLine, UniClawRpcError, ERROR_CODES } from './protocol.js';

const DEFAULT_TIMEOUT_MS = 5000;
const DEFAULT_MAX_ATTEMPTS = 3;
const DEFAULT_BACKOFF_MS = 250;

export class UniClawAdapter {
  constructor({ host = '127.0.0.1', port, timeoutMs = DEFAULT_TIMEOUT_MS, maxAttempts = DEFAULT_MAX_ATTEMPTS, backoffMs = DEFAULT_BACKOFF_MS } = {}) {
    if (!Number.isInteger(port) || port <= 0) {
      throw new TypeError('UniClawAdapter requires a positive integer port');
    }
    this.host = host;
    this.port = port;
    this.timeoutMs = timeoutMs;
    this.maxAttempts = maxAttempts;
    this.backoffMs = backoffMs;

    this.state = 'disconnected'; // disconnected | connecting | connected | error
    this.onConnectionChange = null; // (state) => void

    this._socket = null;
    this._buffer = '';
    this._nextId = 1;
    this._pending = new Map(); // id -> { resolve, reject, timer }
    this._connectingPromise = null;
    this._disposed = false;
  }

  /** Current connection state. */
  getState() {
    return this.state;
  }

  /** Connect (bounded retries with backoff). Resolves when connected; rejects with DRIVERHOST_DISCONNECTED when the DriverHost is unreachable. */
  async ensureConnected() {
    if (this._disposed) throw new UniClawRpcError(ERROR_CODES.DRIVERHOST_DISCONNECTED, 'adapter disposed');
    if (this.state === 'connected') return;
    if (this._connectingPromise) return this._connectingPromise;

    let lastError;
    for (let attempt = 1; attempt <= this.maxAttempts; attempt += 1) {
      if (this._disposed) throw new UniClawRpcError(ERROR_CODES.DRIVERHOST_DISCONNECTED, 'adapter disposed');
      try {
        await this._connectOnce();
        return;
      } catch (err) {
        lastError = err;
        if (attempt < this.maxAttempts) {
          await new Promise((resolve) => setTimeout(resolve, this.backoffMs * attempt));
        }
      }
    }
    this._setState('error');
    throw new UniClawRpcError(ERROR_CODES.DRIVERHOST_DISCONNECTED, `DriverHost unreachable at ${this.host}:${this.port}`, { cause: lastError });
  }

  _connectOnce() {
    if (this._connectingPromise) return this._connectingPromise;
    this._setState('connecting');

    this._connectingPromise = new Promise((resolve, reject) => {
      const socket = net.createConnection({ host: this.host, port: this.port });

      socket.setNoDelay(true);
      socket.setEncoding('utf8');

      // Stale-socket guard: a replaced socket's late close/error events must
      // never corrupt the active connection's state (fresh-state guarantee).
      const isCurrent = () => this._socket === socket;

      const onConnect = () => {
        cleanup();
        this._socket = socket;
        this._setState('connected');
        resolve();
      };
      const onError = (err) => {
        cleanup();
        this._setState('error');
        reject(err);
      };
      const cleanup = () => {
        socket.off('connect', onConnect);
        socket.off('error', onError);
      };

      socket.once('connect', onConnect);
      socket.once('error', onError);
      socket.on('data', (chunk) => {
        if (isCurrent()) this._onData(chunk);
      });
      socket.on('close', () => {
        if (isCurrent()) this._onClose();
      });
      socket.on('error', () => {
        if (!isCurrent()) return; // stale socket error — ignore
        this._rejectAllPending(new UniClawRpcError(ERROR_CODES.DRIVERHOST_DISCONNECTED, 'DriverHost connection error'));
        this._setState('error');
      });
    }).finally(() => {
      this._connectingPromise = null;
    });

    return this._connectingPromise;
  }

  _onData(chunk) {
    this._buffer += chunk;
    let newlineIndex;
    while ((newlineIndex = this._buffer.indexOf('\n')) >= 0) {
      const line = this._buffer.slice(0, newlineIndex);
      this._buffer = this._buffer.slice(newlineIndex + 1);
      if (line.length === 0) continue;
      this._handleResponseLine(line);
    }
  }

  _handleResponseLine(line) {
    let result;
    let error = null;
    try {
      result = parseResponseLine(line);
    } catch (err) {
      error = err;
    }
    // The server echoes the request id; settle by id when parseable, else settle nothing.
    let id = null;
    try {
      id = JSON.parse(line)?.id;
    } catch {
      // malformed — nothing to settle
    }
    const pending = id !== null && id !== undefined ? this._pending.get(id) : undefined;
    if (pending) {
      this._pending.delete(id);
      clearTimeout(pending.timer);
      if (error) pending.reject(error);
      else pending.resolve(result);
    }
  }

  _onClose() {
    this._setState('disconnected');
    this._socket = null;
    this._rejectAllPending(new UniClawRpcError(ERROR_CODES.DRIVERHOST_DISCONNECTED, 'DriverHost connection closed'));
  }

  _rejectAllPending(error) {
    for (const [id, pending] of this._pending) {
      clearTimeout(pending.timer);
      pending.reject(error);
      this._pending.delete(id);
    }
  }

  _request(method, params, timeoutMs = this.timeoutMs) {
    if (this._disposed || this.state !== 'connected') {
      return Promise.reject(new UniClawRpcError(ERROR_CODES.DRIVERHOST_DISCONNECTED, 'not connected to DriverHost'));
    }
    const id = this._nextId;
    this._nextId += 1;

    return new Promise((resolve, reject) => {
      const timer = setTimeout(() => {
        // Reject every pending request (including this one) and mark the
        // connection unusable: a half-dead socket must not strand callers.
        this._rejectAllPending(new UniClawRpcError(ERROR_CODES.DRIVERHOST_DISCONNECTED, `request timed out after ${timeoutMs}ms; connection considered unusable`));
        this._setState('error');
        this._closeSocket();
      }, timeoutMs);
      timer.unref?.();

      this._pending.set(id, { resolve, reject, timer });
      try {
        this._socket.write(encodeRequest(id, method, params));
      } catch (err) {
        this._pending.delete(id);
        clearTimeout(timer);
        reject(new UniClawRpcError(ERROR_CODES.DRIVERHOST_DISCONNECTED, `write failed: ${err.message}`));
      }
    });
  }

  _closeSocket() {
    if (this._socket) {
      const socket = this._socket;
      this._socket = null;
      try {
        socket.end();
        socket.destroy();
      } catch {
        // best-effort close
      }
    }
  }

  _setState(next) {
    if (this.state === next) return;
    this.state = next;
    if (typeof this.onConnectionChange === 'function') {
      try {
        this.onConnectionChange(next);
      } catch {
        // observer errors must not break the adapter
      }
    }
  }

  // ---- read-only surface (frozen method table) -----------------------------

  ping() {
    return this._request('ping');
  }

  listRuns() {
    return this._request('run.list');
  }

  getRunSnapshot(runId) {
    return this._request('run.snapshot.get', { runId });
  }

  getTrap(runId) {
    return this._request('run.trap.get', { runId });
  }

  getRuntimeEvents(runId, cursor) {
    return this._request('run.events.after', cursor ? { runId, cursor } : { runId });
  }

  drainRunEvents(runId) {
    return this._request('run.events.drain', { runId });
  }

  getEvidence(evidenceRef) {
    return this._request('evidence.get', { evidenceRef });
  }

  controlSupport(operation) {
    return this._request('control.support', { operation });
  }

  /**
   * Start a UniClaw Runtime.Agent semantic run (dsh-runtime-agent-subagent-run-entry).
   * ADDITIVE wire method run.start: validates/reserves/registers/schedules and
   * returns RunAccepted { accepted, runId, runState } IMMEDIATELY. This call
   * never waits for completion, never polls events, never invokes any inference
   * service, never issues device operations, and never retries Agent execution.
   * Deterministic rejection surfaces as a typed request_rejected RPC error.
   * @param {object} request - { goal, objects, capabilities, device } (wire shape)
   */
  runStart(request) {
    return this._request('run.start', request);
  }

  /**
   * Poll bounded pending assistance requests (ADDITIVE assistance.pending;
   * dsh-assistance-provider-adapter). Read-only; repeated polls are harmless.
   */
  assistancePending() {
    return this._request('assistance.pending', {});
  }

  /**
   * Submit an assistance resolve (ADDITIVE assistance.resolve). Returns
   * { resolved, diagnostic } — a business result, never an RPC error.
   * @param {object} params - { requestId, worldVersion, recommendation?, additionalEvidence?, reason? }
   */
  assistanceResolve(params) {
    return this._request('assistance.resolve', params);
  }

  /** Graceful close: pending requests fail with DRIVERHOST_DISCONNECTED. */
  disconnect() {
    this._rejectAllPending(new UniClawRpcError(ERROR_CODES.DRIVERHOST_DISCONNECTED, 'adapter disconnected'));
    this._closeSocket();
    this._setState('disconnected');
  }

  /** Terminal cleanup (plugin dispose). */
  dispose() {
    this._disposed = true;
    this.disconnect();
    this.onConnectionChange = null;
  }
}
