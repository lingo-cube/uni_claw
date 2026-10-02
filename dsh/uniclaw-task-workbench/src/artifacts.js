// Read-only index of existing run output. Correlation is an explicit
// metadata.dshSessionId; directory names and transcript text never bind a run.
import { existsSync, readdirSync, readFileSync } from 'node:fs'
import { join, resolve } from 'node:path'

export function createArtifactSource(roots = []) {
  const directories = roots.map(root => resolve(root))
  const scan = () => {
    const runs = []
    const warnings = []
    const walk = (directory) => {
      for (const item of readdirSync(directory, { withFileTypes: true })) {
        const path = join(directory, item.name)
        if (item.isDirectory()) walk(path)
        else if (item.isFile() && item.name === 'metadata.json') {
          try {
            const metadata = JSON.parse(readFileSync(path, 'utf8'))
            if (typeof metadata?.dshSessionId !== 'string' || !metadata.dshSessionId) continue
            const files = readdirSync(directory, { withFileTypes: true })
              .filter(file => file.isFile() && /\.(json|jsonl|journal|log|md|txt)$/.test(file.name))
              .map(file => ({ ref: join(directory, file.name), name: file.name }))
            const readJson = (name) => {
              const file = files.find(candidate => candidate.name === name)
              if (!file) return null
              try { return JSON.parse(readFileSync(file.ref, 'utf8')) } catch (error) {
                warnings.push(`产物读取失败：${file.ref}: ${error.message}`)
                return null
              }
            }
            const traceFile = files.find(file => file.name === 'trace.json')
            let trace = null
            if (traceFile) {
              try {
                const value = JSON.parse(readFileSync(traceFile.ref, 'utf8'))
                if (value.schemaVersion === 'trc/0.1' && Array.isArray(value.spans)) trace = value
                else warnings.push(`不支持的 Trace 格式：${traceFile.ref}`)
              } catch (error) { warnings.push(`Trace 读取失败：${traceFile.ref}: ${error.message}`) }
            }
            runs.push({ source: path, metadata, files, trace, facts: readJson('facts.json'), coverageSteps: readJson('coverage-steps.json') })
          } catch (error) { warnings.push(`运行元数据读取失败：${path}: ${error.message}`) }
        }
      }
    }
    for (const directory of directories) {
      if (existsSync(directory)) {
        try { walk(directory) } catch (error) { warnings.push(`运行目录读取失败：${directory}: ${error.message}`) }
      }
    }
    return { runs, warnings }
  }
  return {
    scan,
    read(sessionId, ref) {
      const candidates = scan().runs.filter(run => run.metadata.dshSessionId === sessionId)
        .flatMap(run => run.files)
      const normalizedRef = typeof ref === 'string' ? ref.replaceAll('\\', '/') : ''
      const matches = candidates.filter(file => file.ref === ref
        || file.name === ref
        || (normalizedRef.length > 0 && file.ref.replaceAll('\\', '/').endsWith(`/${normalizedRef}`)))
      const file = matches.length === 1 ? matches[0] : null
      if (!file) throw new Error('该文件不属于此任务实例的已关联运行产物')
      return { ref: file.ref, name: file.name, text: readFileSync(file.ref, 'utf8') }
    },
  }
}
