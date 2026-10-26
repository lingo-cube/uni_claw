// WORLD-VISUALIZATION-001 切片 2：WorldViz standalone harness 构建脚本。
// 把组件模块（相对 require 收集，沿 build-browser-bundle.mjs 思路）+ 样式 +
// 导出器产出的真实 fixture 数据内联成自包含 harness/world-viz.html，
// 供浏览器真实交互验证（无 DSH 宿主依赖；DSH 入口接线属切片 2 尾项）。
import { readFile, writeFile, stat } from 'node:fs/promises'
import { resolve, relative, dirname, extname } from 'node:path'
import { fileURLToPath } from 'node:url'

const webRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const repoRoot = resolve(webRoot, '../..')
const fixturePath = process.argv[2]
  ? resolve(process.argv[2])
  : resolve(repoRoot, 'changes/WORLD-VISUALIZATION-001/prototype/web/data.js')
const outputName = process.argv[3] || 'world-viz.html'

const seen = new Map()
async function collect(path) {
  const id = relative(webRoot, path).replaceAll('\\', '/')
  if (seen.has(id)) return
  const source = await readFile(path, 'utf8'); seen.set(id, source)
  for (const match of source.matchAll(/require\(['"](\.\.?\/[^'"]+)['"]\)/g)) {
    let child = resolve(dirname(path), match[1])
    if (!extname(child)) { try { await stat(child + '.js'); child += '.js' } catch { child += '/index.js' } }
    await collect(child)
  }
}

await collect(resolve(webRoot, 'src/features/world-viz/create-world-viz.js'))
await collect(resolve(webRoot, 'src/adapters/world-fixture.js'))
const modules = [...seen.entries()].map(([id, source]) => `  ${JSON.stringify(id)}: (require, module, exports) => {\n${source}\n  }`).join(',\n')
const cssText = await readFile(resolve(webRoot, 'src/styles/world-viz.css'), 'utf8')
const fixtureText = await readFile(fixturePath, 'utf8')

const html = `<!doctype html>
<html lang="zh-CN">
<meta charset="utf-8">
<title>WorldViz standalone harness</title>
<style>${cssText}</style>
<body style="margin:0;height:100vh">
<div id="app" style="height:100vh"></div>
<div id="eventlog" style="position:fixed;bottom:0;right:0;max-width:340px;max-height:120px;overflow:auto;background:#000a;color:#9fe6a8;font:11px monospace;padding:4px 8px;border-radius:6px 0 0 0"></div>
<script>
window.__WORLD_VIZ_HARNESS__ = { revision: (location.hash.length > 1 ? location.hash.slice(1) : null) };
</script>
<script>${fixtureText}</script>
<script>
(function () {
  const cache = Object.create(null);
  const modules = {
${modules}
  };
  const load = (id, parent = '') => {
    const entryIds = ['src/features/world-viz/create-world-viz.js', 'src/adapters/world-fixture.js'];
    const direct = entryIds.includes(id) && modules[id] ? id : null;
    if (!id.startsWith('.') && !direct) throw new Error('harness only bundles relative requires: ' + id);
    const resolvedId = direct || (() => {
      const base = parent.split('/').slice(0, -1).join('/');
      const key = id.split('/').reduce((p, x) => {
        if (x === '..') p.pop(); else if (x !== '.') p.push(x);
        return p;
      }, base ? base.split('/') : []).join('/');
      return [key, key + '.js'].find((k) => modules[k]) || key;
    })();
    if (!modules[resolvedId]) throw new Error('missing bundled module: ' + id + ' (from ' + parent + ')');
    if (cache[resolvedId]) return cache[resolvedId].exports;
    const m = { exports: {} }; cache[resolvedId] = m;
    modules[resolvedId]((x) => load(x, resolvedId), m, m.exports);
    return m.exports;
  };
  const { createWorldViz } = load('src/features/world-viz/create-world-viz.js');
  const { createWorldFixtureDataSource } = load('src/adapters/world-fixture.js');
  const log = (line) => { const el = document.getElementById('eventlog'); el.textContent += line + '\\n'; };
  const dataSource = createWorldFixtureDataSource(window.__WORLD_VIZ_DATA__);
  const component = createWorldViz({
    dataSource,
    options: { initialRevision: window.__WORLD_VIZ_HARNESS__.revision },
    mount: ({ html }) => { document.getElementById('app').innerHTML = html; wire(); },
  });
  function wire() {
    document.querySelectorAll('.wv-rev').forEach((cell) =>
      cell.addEventListener('click', () => component.selectRevision(cell.dataset.revision)));
    document.querySelectorAll('[data-node]').forEach((g) =>
      g.addEventListener('click', (ev) => { ev.stopPropagation(); component.selectNode(g.dataset.node); }));
  }
  component.onEvent('revision-select', (e) => log('event revision-select ' + e.revisionId));
  component.onEvent('node-select', (e) => log('event node-select ' + e.nodeId));
  component.start();
})();
</script>
`
const outDir = resolve(webRoot, 'harness')
const { mkdir, readdir, copyFile } = await import('node:fs/promises')
await mkdir(outDir, { recursive: true })
// 携带截图资产（导出器按 captureId 精确映射拷至 fixture 同级 assets/）。
const assetsSource = resolve(dirname(fixturePath), 'assets')
try {
  const files = await readdir(assetsSource)
  await mkdir(resolve(outDir, 'assets'), { recursive: true })
  for (const file of files) await copyFile(resolve(assetsSource, file), resolve(outDir, 'assets', file))
  console.log(`assets copied: ${files.length}`)
} catch { console.log('assets: none') }
await writeFile(resolve(outDir, outputName), html, 'utf8')
console.log(`harness written: web/uniclaw-workspace/harness/${outputName} (${seen.size} modules, fixture ${fixturePath})`)
