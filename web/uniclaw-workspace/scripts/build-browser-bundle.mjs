import { readFile, writeFile, stat } from 'node:fs/promises'
import { resolve, relative, dirname, extname } from 'node:path'
import { fileURLToPath } from 'node:url'
const webRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..')
const repoRoot = resolve(webRoot, '../..')
const styleText = await readFile(resolve(webRoot, 'src/styles/workspace.css'), 'utf8')
const seen = new Map()
async function collect(path) {
  const id = relative(webRoot, path).replaceAll('\\', '/')
  if (seen.has(id)) return
  const source = await readFile(path, 'utf8'); seen.set(id, source)
  for (const match of source.matchAll(/require\(['"](\.\.?\/[^'"]+)['"]\)/g)) {
    let child = resolve(dirname(path), match[1]); if (!extname(child)) { try { await stat(child + '.js'); child += '.js' } catch { child += '/index.js' } } await collect(child)
  }
}
const entry = resolve(webRoot, 'src/browser/entry.js'); await collect(entry)
const modules = [...seen.entries()].map(([id, source]) => `  ${JSON.stringify(id)}: (require, module, exports) => {\n${source}\n  }`).join(',\n')
const bridgeId = relative(webRoot, entry).replaceAll('\\', '/')
const client = `window.__ModuleLoader__.load({id:"@uniclaw/dsh-task-workbench",factory:(externalRequire)=>{const cache=Object.create(null);const modules={\n${modules}\n};const load=(id,parent="")=>{if(id==="react")return externalRequire(id);if(id==="@uniclaw/workspace/browser")id=${JSON.stringify(bridgeId)};if(id!==${JSON.stringify(bridgeId)}&&!id.startsWith("."))return externalRequire(id);const base=parent.split("/").slice(0,-1).join("/");const key=id.split("/").reduce((p,x)=>{if(x==="..")p.pop();else if(x!==".")p.push(x);return p},base?base.split("/"):[]).join("/");const k0=key.endsWith(".js")?key:key+".js";const k=modules[k0]?k0:k0.replace(/\\.js$/,"/index.js");if(cache[k])return cache[k].exports;if(!modules[k])throw Error("missing bundled module: "+k);const m={exports:{}};cache[k]=m;modules[k]((x)=>load(x,k),m,m.exports);return m.exports};const bridge=load(${JSON.stringify(bridgeId)});const workspaceStyleText=${JSON.stringify(styleText)};const strict=(typeSymbol)=>({mode:"strict",typeSymbol,schema:{parse:(value)=>value},create:()=>({parse:(value)=>value})});const param=(name)=>({name,wire:name,source:"json",codec:strict("uniclaw-task-workbench/"+name)});const descriptor=(method,names)=>({id:"uniclawTaskPanel."+method,service:"uniclawTaskPanel",namespace:"uniclawTaskPanel",method,invocation:{kind:"direct"},parameters:names.map(param),result:strict("uniclaw-task-workbench/JsonValue")});const contribution={package:"@uniclaw/dsh-task-workbench",descriptors:[descriptor("workspace",[]),descriptor("session",["sessionId"]),descriptor("artifact",["sessionId","ref"])]};function apply(ctx){const slots=ctx.get("slots");if(!slots)return;slots.inject("sidebar.footer.action",()=>slots.register({name:"sidebar.footer.action",id:"uniclaw-task-panel-entry",order:12,label:"UniClaw 工作空间"},()=>externalRequire("react").createElement("button",{type:"button",onClick:()=>{const panel=ctx.get("remote.uniclawTaskPanel");const mountApi=panel&&typeof panel.$mount==="function"?panel:ctx.get("remote");if(!mountApi||typeof mountApi.$mount!=="function")return;mountApi.$mount(contribution).then(()=>{const mountedPanel=ctx.get("remote.uniclawTaskPanel");const api=mountedPanel&&typeof mountedPanel.workspace==="function"?mountedPanel:mountApi;if(!api||typeof api.workspace!=="function"||typeof api.session!=="function"||typeof api.artifact!=="function"||typeof document==="undefined"||!document.body)return;bridge.createDshWorkspaceBrowserBridge({remote:api,container:document.body,styleText:workspaceStyleText}).start()})}},"UniClaw 工作空间")));slots.inject("shell.overlay",()=>slots.register({name:"shell.overlay",id:"uniclaw-task-panel-overlay",order:12,label:"UniClaw 工作空间"},()=>null))}return{apply,inject:["slots","remote"]}}});\n`
await writeFile(resolve(repoRoot, 'dsh/uniclaw-task-workbench/src/client.js'), client)
console.log(`generated ${seen.size} shared modules`)
