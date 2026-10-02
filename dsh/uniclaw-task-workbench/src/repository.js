/**
 * TaskRepository — PNL-001 存储缝（决策 3）。
 *
 * 调用方只面对本模块导出的接口；存储介质是 JSON 文件（默认
 * ~/.dsh/uniclaw-tasks/tasks.json，config 可覆盖）。后续"入库"时换实现，
 * 调用方不改。文件形状版本化（storeVersion），加载时版本不符 fail-closed。
 *
 * 本模块不校验业务 schema（那是 index.js 协议层的职责），只负责：
 * 读写、ID 铸造、原子落盘（tmp + rename）、内存态与磁盘一致性。
 */

import { createHash, randomBytes } from 'node:crypto'
import { mkdirSync, readFileSync, renameSync, writeFileSync, existsSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { homedir } from 'node:os'

const STORE_VERSION = 1

const newTaskId = () => `task-${randomBytes(8).toString('hex')}`
const newInstanceId = () => `psi-${randomBytes(8).toString('hex')}`

/**
 * @param {string} filePath JSON store location (config override for tests).
 * @param {() => string} now clock injection for tests.
 */
export function createTaskRepository(filePath, now = () => new Date().toISOString()) {
  const file = filePath ?? join(homedir(), '.dsh', 'uniclaw-tasks', 'tasks.json')
  let store = null

  const load = () => {
    if (store !== null) return store
    if (!existsSync(file)) {
      store = { storeVersion: STORE_VERSION, tasks: [] }
      return store
    }
    const parsed = JSON.parse(readFileSync(file, 'utf8'))
    if (parsed === null || typeof parsed !== 'object'
      || parsed.storeVersion !== STORE_VERSION || !Array.isArray(parsed.tasks)) {
      throw new Error(`task store corrupted or unsupported version: ${file}`)
    }
    store = parsed
    return store
  }

  const persist = () => {
    mkdirSync(dirname(file), { recursive: true })
    const tmp = `${file}.tmp-${process.pid}-${Date.now()}`
    writeFileSync(tmp, JSON.stringify(load(), null, 2), 'utf8')
    renameSync(tmp, file)
  }

  const findTask = (taskId) => load().tasks.find(t => t.taskId === taskId) ?? null

  return {
    file,
    /** All task definitions with their instances (caller must not mutate). */
    list() { return JSON.parse(JSON.stringify(load().tasks)) },
    get(taskId) {
      const task = findTask(taskId)
      return task === null ? null : JSON.parse(JSON.stringify(task))
    },
    /** Create a task definition; returns the stored record. */
    create({ taskId, title, requirement, projectRef, status }) {
      const tasks = load().tasks
      const record = {
        taskId: typeof taskId === 'string' && taskId.length > 0 ? taskId : newTaskId(),
        title, requirement,
        ...(projectRef !== undefined ? { projectRef } : {}),
        status: status ?? 'active',
        createdAt: now(),
      }
      if (findTask(record.taskId) !== null) throw new Error(`taskId already exists: ${record.taskId}`)
      tasks.push(record)
      persist()
      return JSON.parse(JSON.stringify(record))
    },
    updateStatus(taskId, status) {
      const task = findTask(taskId)
      if (task === null) throw new Error(`unknown taskId: ${taskId}`)
      task.status = status
      task.updatedAt = now()
      persist()
      return JSON.parse(JSON.stringify(task))
    },
    /** Append a task instance (S2: called by instantiate after session create). */
    addInstance(taskId, instance) {
      const task = findTask(taskId)
      if (task === null) throw new Error(`unknown taskId: ${taskId}`)
      if (!Array.isArray(task.instances)) task.instances = []
      task.instances.push(instance)
      persist()
      return JSON.parse(JSON.stringify(instance))
    },
    updateInstance(taskId, instanceId, patch) {
      const task = findTask(taskId)
      const instance = task?.instances?.find(i => i.instanceId === instanceId)
      if (instance === undefined) throw new Error(`unknown instance: ${instanceId}`)
      Object.assign(instance, patch)
      persist()
      return JSON.parse(JSON.stringify(instance))
    },
    /** Test seam: mint IDs without touching the store. */
    _idShapes: { newTaskId, newInstanceId },
  }
}

/** sha256 of a file — schema-hash self-check (same discipline as product-protocol). */
export const sha256File = (path) =>
  createHash('sha256').update(readFileSync(path)).digest('hex')
