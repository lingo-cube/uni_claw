using UniClaw.Host;

// HOST-001 v0 入口：单次 headless run，产物落 ./runs/<runid>/，按终局退出。
var runsRoot = args.Length > 0 ? args[0] : "runs";
var result = HostRunner.RunOnce(runsRoot);
Console.WriteLine($"status   : {result.Status}" + (result.Reason is null ? "" : $" ({result.Reason})"));
Console.WriteLine($"outcome  : {result.OutcomeClassification ?? "-"}");
Console.WriteLine($"delivered: {result.DeliveredEffects} [{string.Join(",", result.ReceiptOutcomes)}]");
Console.WriteLine($"run dir  : {result.RunDir}");
Console.WriteLine($"digest   : {result.FactsDigest}");
return HostRunner.ExitCode(result.Status);
