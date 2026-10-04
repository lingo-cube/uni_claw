/**
 * Host-facing adapter for the PNL-004 Runtime launch seam.
 *
 * This module owns transport only. It never mints runId or productSessionId;
 * the configured Runtime service remains the authority for both values.
 */

const DEFAULT_CREATE_PATH = '/api/uniclaw-runtime/runs'
const DEFAULT_RECOVER_PATH = '/api/uniclaw-runtime/runs/recover'
const CONTRACT_VERSION = 'uniclaw.workspace.contract.v1'
const REQUEST_SCHEMA = 'uniclaw.workspace.task-launch-request.v1'

const unavailable = (message) => {
  const error = new Error(message)
  error.code = 'runtime-unavailable'
  return error
}

const invalidResponse = (message) => {
  const error = new Error(message)
  error.code = 'runtime-response-invalid'
  return error
}

const joinUrl = (baseUrl, path) => `${String(baseUrl).replace(/\/$/, '')}/${String(path).replace(/^\//, '')}`

const readJson = async (response) => {
  if (!response || typeof response.json !== 'function') throw invalidResponse('Runtime transport returned no JSON response')
  let payload
  try {
    payload = await response.json()
  } catch (error) {
    throw invalidResponse(`Runtime transport returned invalid JSON: ${error?.message ?? error}`)
  }
  if (response.ok === false || (typeof response.status === 'number' && response.status >= 400)) {
    const message = typeof payload?.message === 'string' ? payload.message
      : (typeof payload?.error?.message === 'string' ? payload.error.message : `Runtime transport failed (${response.status ?? 'unknown'})`)
    const failure = new Error(message)
    failure.code = typeof payload?.code === 'string' ? payload.code
      : (typeof payload?.error?.code === 'string' ? payload.error.code : 'runtime-transport-failed')
    throw failure
  }
  return payload?.run && typeof payload.run === 'object' ? payload.run : payload
}

const validateRun = (value) => {
  if (!value || typeof value !== 'object'
    || typeof value.runId !== 'string' || value.runId.length === 0
    || typeof value.productSessionId !== 'string' || value.productSessionId.length === 0) {
    throw invalidResponse('Runtime response must contain runId and productSessionId')
  }
  return { ...value }
}

const requestBody = (request) => ({
  schemaVersion: REQUEST_SCHEMA,
  contractVersion: CONTRACT_VERSION,
  projectRef: request.projectRef,
  testSetRef: request.testSetRef,
  taskRef: request.taskRef,
  correlationId: request.correlationId,
  idempotencyKey: request.idempotencyKey,
  launchId: request.launchId,
})

/**
 * @param {{baseUrl?: string, fetchImpl?: Function, timeoutMs?: number, createPath?: string, recoverPath?: string}} options
 */
export function createRuntimeProvider(options = {}) {
  const baseUrl = typeof options.baseUrl === 'string' && options.baseUrl.trim().length > 0
    ? options.baseUrl.trim()
    : null
  const fetchImpl = options.fetchImpl ?? globalThis.fetch
  const timeoutMs = Number.isFinite(options.timeoutMs) && options.timeoutMs > 0 ? options.timeoutMs : 15_000
  const createPath = options.createPath || DEFAULT_CREATE_PATH
  const recoverPath = options.recoverPath || DEFAULT_RECOVER_PATH

  const post = async (path, body) => {
    if (baseUrl === null) throw unavailable('UNICLAW_RUNTIME_BASE_URL is not configured')
    if (typeof fetchImpl !== 'function') throw unavailable('Runtime transport fetch is unavailable')
    const controller = typeof AbortController === 'function' ? new AbortController() : null
    const timer = controller === null ? null : setTimeout(() => controller.abort(), timeoutMs)
    try {
      const response = await fetchImpl(joinUrl(baseUrl, path), {
        method: 'POST',
        headers: { 'content-type': 'application/json' },
        body: JSON.stringify(body),
        ...(controller === null ? {} : { signal: controller.signal }),
      })
      return validateRun(await readJson(response))
    } catch (error) {
      if (error?.code) throw error
      if (error?.name === 'AbortError') throw unavailable(`Runtime transport timed out after ${timeoutMs}ms`)
      throw unavailable(`Runtime transport failed: ${error?.message ?? error}`)
    } finally {
      if (timer !== null) clearTimeout(timer)
    }
  }

  return Object.freeze({
    createRun: (request) => post(createPath, requestBody(request)),
    recoverRun: (request) => post(recoverPath, {
      schemaVersion: REQUEST_SCHEMA,
      contractVersion: CONTRACT_VERSION,
      launchId: request.launchId,
      idempotencyKey: request.idempotencyKey,
    }),
  })
}

export const runtimeProviderContract = Object.freeze({
  requestSchema: REQUEST_SCHEMA,
  contractVersion: CONTRACT_VERSION,
  createPath: DEFAULT_CREATE_PATH,
  recoverPath: DEFAULT_RECOVER_PATH,
})
