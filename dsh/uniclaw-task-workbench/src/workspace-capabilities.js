const CAPABILITY_VERSION = 'v1'
const SCHEMA_VERSION = 'uniclaw.workspace.capability.v1'
const CONTRACT_VERSION = 'uniclaw.workspace.contract.v1'
const ERROR_CODES = new Set(['unavailable', 'uncorrelated', 'stale', 'permission-denied', 'not-found', 'timeout'])

const queryError = (code, message, source = 'dsh') => ({
  schemaVersion: 'uniclaw.workspace.query-error.v1',
  contractVersion: CONTRACT_VERSION,
  code: ERROR_CODES.has(code) ? code : 'unavailable',
  message,
  retryable: ['unavailable', 'stale', 'timeout'].includes(code),
  source,
})

const envelope = (capability, data, now, revision, requestId = `${capability}-${Date.now()}`) => ({
  schemaVersion: SCHEMA_VERSION,
  contractVersion: CONTRACT_VERSION,
  capability,
  capabilityVersion: CAPABILITY_VERSION,
  requestId,
  ok: true,
  data,
  ...(revision !== undefined ? { revision } : {}),
  ...(now ? { observedAt: now() } : { observedAt: new Date().toISOString() }),
})

const failed = (capability, error, now, revision, requestId) => ({
  schemaVersion: SCHEMA_VERSION,
  contractVersion: CONTRACT_VERSION,
  capability,
  capabilityVersion: CAPABILITY_VERSION,
  requestId: requestId || `${capability}-${Date.now()}`,
  ok: false,
  error,
  ...(revision !== undefined ? { revision } : {}),
  ...(now ? { observedAt: now() } : { observedAt: new Date().toISOString() }),
})

const panelError = (error, source = 'dsh') => {
  const codeMap = {
    'artifact-not-found': 'not-found',
    'artifact-source-unavailable': 'unavailable',
    'task-instance-not-found': 'not-found',
    'session-not-found': 'not-found',
    'permission-denied': 'permission-denied',
    timeout: 'timeout',
  }
  return queryError(codeMap[error?.code] || 'unavailable', error?.message || 'DSH panel request failed', source)
}

const invokePanel = async (capability, method, args, meta) => {
  try {
    const result = await method(...args)
    if (!result || result.success !== true) return failed(capability, panelError(result?.error), meta.now, meta.revision, args[0]?.requestId)
    return envelope(capability, result, meta.now, meta.revision, args[0]?.requestId)
  } catch (error) {
    return failed(capability, queryError('unavailable', error instanceof Error ? error.message : 'DSH panel request failed'), meta.now, meta.revision, args[0]?.requestId)
  }
}

const resolve = async (resolver, productSessionId) => {
  if (typeof resolver !== 'function' || typeof productSessionId !== 'string' || productSessionId.length === 0) return null
  try {
    const result = await resolver(productSessionId)
    const dshSessionId = typeof result === 'string' ? result : result?.dshSessionId
    return typeof dshSessionId === 'string' && dshSessionId.length > 0 ? dshSessionId : null
  } catch {
    return undefined
  }
}

const safeRef = (ref) => typeof ref === 'string' && ref.length > 0
  && !/[\u0000-\u001f\u007f]/.test(ref)
  && !ref.startsWith('/') && !ref.startsWith('\\')
  && !/^[A-Za-z][A-Za-z0-9+.-]*:/.test(ref) && !ref.startsWith('//')
  && !ref.split('/').includes('..') && !ref.split('\\').includes('..')

const sessionProjection = (payload, productSessionId, dshSessionId) => ({
  productSessionId,
  hostSessionRef: { host: 'dsh', sessionId: dshSessionId },
  task: payload.task ?? null,
  instance: payload.instance ?? null,
  projectRef: payload.instance?.projectRef ?? payload.task?.projectRef ?? null,
  testSetRef: payload.instance?.testSetRef ?? payload.task?.testSetRef ?? null,
  taskRef: payload.instance?.taskRef ?? payload.task?.taskRef ?? null,
  runId: payload.instance?.runId ?? payload.uniclawTraceContext?.runId ?? null,
  launchStages: Array.isArray(payload.instance?.launchStages) ? payload.instance.launchStages : [],
  storageNamespaceRef: payload.instance?.storageNamespaceRef ?? null,
  metadata: payload.metadata ?? null,
  metadataClaims: Array.isArray(payload.metadataClaims) ? payload.metadataClaims : (Array.isArray(payload.instance?.metadata) ? payload.instance.metadata : []),
  conversation: payload.conversation,
  conversationGroups: payload.conversationGroups,
  runStages: payload.runStages,
  dshTrace: payload.dshTrace,
  uniflowTrace: payload.uniflowTrace,
  uniclawTrace: payload.uniclawTrace,
  evidence: payload.evidence,
  source: 'dsh',
})

const relabel = (result, capability) => ({ ...result, capability })

const taskInstanceProjection = (instance) => {
  const productSessionId = typeof instance?.productSessionId === 'string' && instance.productSessionId.length > 0
    ? instance.productSessionId : undefined
  return {
    ...instance,
    hostSessionRef: typeof instance?.sessionId === 'string' ? { host: 'dsh', sessionId: instance.sessionId } : undefined,
    ...(productSessionId ? { productSessionId, correlationStatus: 'correlated' } : { correlationStatus: 'uncorrelated' }),
  }
}

export function createDshWorkspaceCapabilities({ panel, resolveProductSession, now, revision } = {}) {
  const meta = { now, revision }
  if (!panel || typeof panel !== 'object') throw new TypeError('panel is required')

  const TaskQuery = {
    async listProjects(request = {}) {
      if (typeof panel.workspace !== 'function') return failed('TaskQuery', queryError('unavailable', 'DSH workspace capability is unavailable'), now, revision, request.requestId)
      const result = await invokePanel('TaskQuery', panel.workspace.bind(panel), [request], meta)
      if (!result.ok) return result
      if (!Array.isArray(result.data.projects)) {
        return failed('TaskQuery', queryError('unavailable', 'DSH workspace payload is missing projects'), now, revision, request.requestId)
      }
      if (result.data.projects.some(project => !Array.isArray(project?.instances))) {
        return failed('TaskQuery', queryError('unavailable', 'DSH workspace project instances payload is malformed'), now, revision, request.requestId)
      }
      const projects = result.data.projects.map(project => ({
        ...project,
        instances: Array.isArray(project?.instances) ? project.instances.map(taskInstanceProjection) : project?.instances,
      }))
      const uncorrelated = projects.flatMap(project => Array.isArray(project.instances)
        ? project.instances.filter(instance => instance.correlationStatus === 'uncorrelated').map(instance => queryError('uncorrelated', `DSH session ${instance.sessionId} has no ProductSession mapping`))
        : [])
      return { ...result, data: { projects, errors: [...uncorrelated, ...(result.data.artifactWarnings?.map(message => queryError('unavailable', message, 'dsh-artifact')) ?? [])] } }
    },
    async listTaskInstances(request = {}) {
      const projectsResult = await this.listProjects(request)
      if (!projectsResult.ok) return projectsResult
      const project = projectsResult.data.projects.find(item => item.projectId === request.projectId)
      if (!project) return failed('TaskQuery', queryError('not-found', `Project ${request.projectId} was not found`), now, revision, request.requestId)
      if (!Array.isArray(project.instances)) return failed('TaskQuery', queryError('unavailable', 'Project instances payload is malformed'), now, revision, request.requestId)
      return { ...projectsResult, data: { taskInstances: project.instances, errors: projectsResult.data.errors ?? [] } }
    },
  }

const SessionQuery = {
    async getSession(request = {}) {
      const dshSessionId = await resolve(resolveProductSession, request.productSessionId)
      if (dshSessionId === undefined) return failed('SessionQuery', queryError('unavailable', 'ProductSession resolver failed'), now, revision, request.requestId)
      if (!dshSessionId) return failed('SessionQuery', queryError('uncorrelated', 'ProductSession has no explicit DSH session mapping'), now, revision, request.requestId)
      if (typeof panel.session !== 'function') return failed('SessionQuery', queryError('unavailable', 'DSH session capability is unavailable'), now, revision, request.requestId)
      const result = await invokePanel('SessionQuery', panel.session.bind(panel), [{ sessionId: dshSessionId, requestId: request.requestId }], meta)
      if (!result.ok) return result
      const requiredArrays = ['conversation', 'conversationGroups', 'dshTrace', 'uniflowTrace', 'uniclawTrace', 'evidence']
      if (requiredArrays.some(key => !Array.isArray(result.data?.[key]))) {
        return failed('SessionQuery', queryError('unavailable', 'DSH session payload has malformed projection arrays'), now, revision, request.requestId)
      }
      return { ...result, data: sessionProjection(result.data, request.productSessionId, dshSessionId) }
    },
    async getTimeline(request = {}) {
      const session = await this.getSession(request)
      if (!session.ok) return session
      const data = session.data
      return { ...session, data: { items: data.conversation ?? [], errors: [] } }
    },
  }

  const TraceQuery = {
    async getTraces(request = {}) {
      const session = await SessionQuery.getSession(request)
      if (!session.ok) return relabel(session, 'TraceQuery')
      const data = session.data
      const sourceTraces = [
        { source: 'dsh', authority: 'dsh-host', schemaVersion: null, items: data.dshTrace ?? [] },
        { source: 'uniflow', authority: 'uniclaw-harness', schemaVersion: null, items: data.uniflowTrace ?? [] },
        { source: 'uniclaw', authority: 'uniclaw-runtime', schemaVersion: null, items: data.uniclawTrace ?? [] },
      ]
      const traces = sourceTraces.flatMap(({ source, items }) => items.map(item => ({ ...item, source })))
      const traceContext = {
        productSessionId: data.productSessionId ?? request.productSessionId ?? null,
        hostSessionRef: data.hostSessionRef ?? null,
        runId: data.runId ?? null,
        correlationId: request.correlationId ?? request.requestId ?? null,
        observedAt: session.observedAt ?? new Date().toISOString(),
        sources: sourceTraces.map(({ source, authority, schemaVersion, items }) => ({
          source, authority, schemaVersion, count: items.length, truncated: false, cursor: null,
        })),
      }
      return { ...session, capability: 'TraceQuery', data: { traces, traceContext, errors: [] } }
    },
  }

  const EvidenceQuery = {
    async getEvidence(request = {}) {
      const session = await SessionQuery.getSession(request)
      if (!session.ok) return relabel(session, 'EvidenceQuery')
      return { ...session, capability: 'EvidenceQuery', data: { evidence: session.data.evidence ?? [], errors: [] } }
    },
  }

  const DetailQuery = {
    async resolveDetail(request = {}) {
      const productSessionId = request.productSessionId ?? request.detailRef?.productSessionId
      const dshSessionId = await resolve(resolveProductSession, productSessionId)
      if (dshSessionId === undefined) return failed('DetailQuery', queryError('unavailable', 'ProductSession resolver failed'), now, revision, request.requestId)
      if (!dshSessionId) return failed('DetailQuery', queryError('uncorrelated', 'ProductSession has no explicit DSH session mapping'), now, revision, request.requestId)
      const ref = request.detailRef?.refId
      if (!safeRef(ref)) return failed('DetailQuery', queryError('not-found', 'DetailRef must be a controlled relative reference'), now, revision, request.requestId)
      if (typeof panel.artifact !== 'function') return failed('DetailQuery', queryError('unavailable', 'DSH artifact capability is unavailable'), now, revision, request.requestId)
      const result = await invokePanel('DetailQuery', panel.artifact.bind(panel), [{ sessionId: dshSessionId, ref, requestId: request.requestId }], meta)
      return result.ok ? { ...result, data: { ...result.data, source: 'dsh', productSessionId } } : result
    },
  }

  return { TaskQuery, SessionQuery, TraceQuery, EvidenceQuery, DetailQuery }
}
