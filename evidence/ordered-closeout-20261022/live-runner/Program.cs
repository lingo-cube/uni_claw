using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using UniClaw.Agent.Dsh;
using UniClaw.Agent.Profile;
using UniClaw.Host;
using UniClaw.Host.Capability;
using UniClaw.Host.Dsh.Tests;
using UniClaw.Host.SettingsCoverage;
using UniClaw.Kernel.Capability;
using UniClaw.Kernel.Runtime;

var root=Directory.GetCurrentDirectory();
var output=Path.GetFullPath(args[0]);
Directory.CreateDirectory(output);
var json=ProductProtocolJson.CreateOptions();json.WriteIndented=true;
void Save(string name,object? value)=>File.WriteAllText(Path.Combine(output,name),JsonSerializer.Serialize(value,json));
var serial=Environment.GetEnvironmentVariable("UNICLAW_ANDROID_DEVICE")??throw new Exception("missing device");
var lease=Environment.GetEnvironmentVariable("UNICLAW_ANDROID_RUN_DIR")??throw new Exception("missing lease");
if(!File.ReadAllLines(Path.Combine(lease,".state")).Contains("SERIAL="+serial))throw new Exception("lease mismatch");
var bindings=UniagentDshBindingsYaml.LoadDefault();
if(bindings.Service.BaseUri.Port!=3081)throw new Exception("registered endpoint required");
var profile=UniAgentProfileYaml.LoadDefault();
var models=DshModelManagement.FromBindings(bindings,profile);
var model=DshModelManagement.ResolveDshModel(models,LogicalProfileId.AgentDecision);
var slowModel=DshModelManagement.ResolveDshModel(models,LogicalProfileId.Text);
using var traffic=new Traffic(output);
await using var peer=new DshOpenedHttpPeer(bindings.Service,handler:traffic,model:model,expectedProfile:profile,requestTimeout:TimeSpan.FromSeconds(210));
// The dedicated service and the earlier handshake attachment belong to this Leader.
await peer.DetachAsync(CancellationToken.None);
var slowBridge=new DshSlowConsult((id,prompt,selected,image,token)=>peer.ExecuteSlowAsync(id,prompt,selected,image,token,reasoningEffort:"high"),slowModel);
UniPerceptionPipeline.Fetch fetch=(request,correlation,token)=>slowBridge.FetchAsync(request,correlation,TimeSpan.FromSeconds(15),token)
 .ContinueWith(t=>new UniPerceptionFetchResult(t.Result.Status,t.Result.Admitted,t.Result.Proposals,t.Result.SemanticDisposition,t.Result.Diagnostic),TaskScheduler.Default);
var semantic=new DshSemanticPerception((id,prompt,selected,effort,token)=>peer.ExecuteSlowAsync(id,prompt,selected,cancellationToken:token,reasoningEffort:effort),slowModel);
var product=PerceptionCapabilityComposition.RegisterProductPerception(uniPerception:semantic.CreateCapability());
ModelManagementCapabilityComposition.RegisterModelManagement(product,models,DshModelManagement.RealizationName);
var integration=RuntimeIntegrationCapabilityComposition.RegisterLanguageInspector();
var original=SettingsCoverageConfig.LoadDefault();
var config=original with { Bounds=original.Bounds with {
 MaxSteps=48,MaxConsultRounds=32,MaxTotalSteps=48,MaxTotalConsultRounds=32,
 AuthorizedTotalSteps=48,AuthorizedTotalConsultRounds=32,GoalDirectedContinuationEnabled=false,
 RenewalThresholdSteps=0,RenewalThresholdRounds=0 } };
Save("preregistration.json",new {date="2026-10-22",purpose="bounded current Settings/trajectory acceptance; no forced signal or fabricated fields",serial,lease,
 model,profile.Revision,config,limits=new {deviceScope="registered clone",steps=48,consultations=32,timeoutMinutes=15,repetitions=1},
 acceptance=new[]{"preflight before formal flow","real current Host contexts and DSH exports retained","if natural stagnation occurs: same cutoff in heartbeat and casePacket, actual received prompt inspected","one real bounded device run; remaining frontier disclosed; no all-tree claim from bounded success"},
 sources=Directory.EnumerateFiles(Path.Combine(root,"src"),"*.cs",SearchOption.AllDirectories).Where(p=>!p.Contains("/obj/")&&!p.Contains("/bin/"))
 .ToDictionary(p=>Path.GetRelativePath(root,p),p=>Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(p)))) });
var start=new ProcessStartInfo("adb"){RedirectStandardOutput=true,RedirectStandardError=true};
foreach(var a in new[]{"-s",serial,"shell","am","start","-S","-a","android.settings.SETTINGS"})start.ArgumentList.Add(a);
using(var process=Process.Start(start)!){var text=await process.StandardOutput.ReadToEndAsync();await process.WaitForExitAsync();File.WriteAllText(Path.Combine(output,"open-settings.txt"),text);if(process.ExitCode!=0)throw new Exception("Settings launch failed");}
await Task.Delay(2000);
DshAgentAdapter? agent=null;var session="trajectory-20261022-"+Guid.NewGuid().ToString("N");var count=0;
LanguageInspectionSelection? selection=null;
AgentDecision? Consult(AgentDecisionContext context){
 var index=++count;var dir=Path.Combine(output,"consults",index.ToString("D2"));Directory.CreateDirectory(dir);
 File.WriteAllText(Path.Combine(dir,"context.json"),JsonSerializer.Serialize(context,json));
 agent??=new DshAgentAdapter(new DshOpenedDecisionChannel(peer),session,context.RunId,turnTimeout:TimeSpan.FromSeconds(200));
 var requestsBefore=traffic.RequestIds.Count;
 try{
  var decision=agent.Consult(context);
  selection??=agent.TakeInitialTaskInitialization()?.CapabilitySelection is {} s?new LanguageInspectionSelection(s.CapabilityId,s.ExpectedLanguage??"",s.IgnoreRoutes):null;
  File.WriteAllText(Path.Combine(dir,"decision.json"),JsonSerializer.Serialize(decision,decision?.GetType()??typeof(object),json));
  File.WriteAllText(Path.Combine(dir,"diagnostics.json"),JsonSerializer.Serialize(agent.Diagnostics,json));
  return decision;
 }finally{
  if(agent.DshSessionId is {Length:>0} sid && traffic.RequestIds.Skip(requestsBefore).LastOrDefault() is {} requestId){
   try{var snapshot=PlanGenerationTrajectoryCollector.CaptureAsync(bindings.Service,sid,requestId,Path.Combine(dir,"trajectory")).GetAwaiter().GetResult();
    File.WriteAllText(Path.Combine(dir,"trajectory-summary.json"),JsonSerializer.Serialize(snapshot with {ConsultationRawJsonl="see trajectory/dsh-consultation.raw.jsonl"},json));}
   catch(Exception e){File.WriteAllText(Path.Combine(dir,"trajectory-error.txt"),e.Message);}
  }
  Console.WriteLine($"consultation={index} heartbeat={context.Heartbeat is not null} casePacket={context.CasePacket is not null}");
 }
}
using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(15));
try{
 var result=await SettingsCoverageRunner.RunAsync(Path.Combine(output,"runs"),new SettingsCoverageRunner.Options(serial,
  new LivePerception.LiveAssets(serial,"wifi-settings",Path.Combine(root,"platforms/perception"),Path.Combine(root,".perception/venv/bin/python"),Path.Combine(root,".perception/cache")),
  config,Consult,DshSessionIdAccessor:()=>agent?.DshSessionId,SlowConsult:fetch,LanguageInspection:config.LanguageInspectionRequest,
  LanguageInspectionSelectionProvider:()=>selection,RuntimeIntegrationCapabilities:integration,ProductCapabilities:product,
  SemanticPages:new[]{new SemanticPageCandidate("observed-page","当前观察页面","只标记当前实际呈现的系统页面；标题从当前文字选择，材料不足为unknown，不猜预期")}),timeout.Token);
 Save("result.json",result);Console.WriteLine($"result={result.Status} steps={result.Steps.Count} consultations={count} runDir={result.RunDir}");
}catch(Exception e){Save("failure.json",new {type=e.GetType().FullName,message=e.Message,stack=e.StackTrace});Environment.ExitCode=3;}
finally{
 try{if(agent is not null)await agent.RevokeAttachmentAsync();else await peer.DetachAsync(CancellationToken.None);Save("attachment-cleanup.json",new {detached=true});}
 catch(Exception e){Save("attachment-cleanup.json",new {detached=false,error=e.Message});}
 if(agent is not null)await agent.DisposeAsync();
}
sealed class Traffic(string output):HttpMessageHandler{
 readonly HttpClientHandler inner=new(); public List<string> RequestIds{get;}=[];
 protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){
  var body=request.Content is null?"":await request.Content.ReadAsStringAsync(token);
  File.AppendAllText(Path.Combine(output,"transport.jsonl"),JsonSerializer.Serialize(new {direction="request",path=request.RequestUri?.AbsolutePath,body})+"\n");
  if(request.RequestUri?.AbsolutePath=="/api/uniclaw-agent/consult"){using var doc=JsonDocument.Parse(body);RequestIds.Add(doc.RootElement.GetProperty("requestId").GetString()!);}
  using var invoker=new HttpMessageInvoker(inner,false);var response=await invoker.SendAsync(request,token);
  var responseBody=await response.Content.ReadAsStringAsync(token);
  File.AppendAllText(Path.Combine(output,"transport.jsonl"),JsonSerializer.Serialize(new {direction="response",status=(int)response.StatusCode,body=responseBody})+"\n");
  response.Content=new StringContent(responseBody,Encoding.UTF8,"application/json");return response;
 }
 protected override void Dispose(bool disposing){if(disposing)inner.Dispose();base.Dispose(disposing);}
}
