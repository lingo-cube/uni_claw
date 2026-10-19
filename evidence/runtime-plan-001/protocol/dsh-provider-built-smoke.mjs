import assert from 'node:assert/strict';
import { createServer } from 'node:http';
import { once } from 'node:events';
import { DeepSeekAdapter, resolveAdapterOptions } from '/Users/fran/Documents/Code/dk-harness/packages/llm/llm-deepseek/lib/index.js';
import { createUserMessage } from '/Users/fran/Documents/Code/dk-harness/packages/llm/llm/lib/index.js';
const events = [
  {type:'message_start',message:{usage:{input_tokens:12,output_tokens:1}}},
  {type:'content_block_start',index:0,content_block:{type:'tool_use',id:'call_1',name:'submit_decision',input:{}}},
  {type:'content_block_delta',index:0,delta:{type:'input_json_delta',partial_json:'{'}},
  {type:'content_block_stop',index:0},
  {type:'message_delta',delta:{stop_reason:'tool_use'},usage:{output_tokens:5}},
  {type:'message_stop'},
];
let requests=0;
const server=createServer((request,response)=>{requests++;request.resume();response.setHeader('content-type','text/event-stream');response.end(events.map(e=>`event: ${e.type}\ndata: ${JSON.stringify(e)}\n\n`).join(''));});
server.listen(0,'127.0.0.1');await once(server,'listening');
const address=server.address();assert.ok(address && typeof address!=='string');
const adapter=new DeepSeekAdapter({options:()=>resolveAdapterOptions({baseURL:`http://127.0.0.1:${address.port}/anthropic`}),resolveAuth:async()=>({headers:{'x-api-key':'fixture-key'}}),resolveUserId:()=> 'fixture-user',resolveAttachments:()=>undefined,resolveImageAccess:()=>undefined,prepareExtensions:async()=>({fields:{},accept:async()=>{}})});
const chunks=[];let failure;
try {
  for await(const chunk of adapter.stream({provider:'deepseek-official',model:'deepseek-flash',messages:[createUserMessage({source:{kind:'user'},content:[{type:'text',text:'fixture request'}]})]})) chunks.push(chunk);
} catch(error) {failure=error;}
finally {server.closeAllConnections();await new Promise((resolve,reject)=>server.close(e=>e?reject(e):resolve()));}
assert.equal(failure?.code,'MALFORMED_RESPONSE');
assert.deepEqual(chunks.at(-1),{type:'usage',usage:{inputTokens:12,outputTokens:5,totalTokens:17}});
assert.equal(chunks.some(c=>c.type==='finish'),false);
assert.equal(requests,1);
console.log(JSON.stringify({publishedArtifact:true,usage:chunks.at(-1).usage,error:failure.code,finish:false,requests}));
