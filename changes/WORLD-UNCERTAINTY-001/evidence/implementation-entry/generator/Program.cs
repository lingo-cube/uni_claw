using System.Text;
using UniClaw.Agent.Dsh;
var schema = ProductProtocolSchema.Current;
foreach(var path in new[]{"schemas/agt-002/product-protocol.schema.json","dsh/uniclaw-decision-channel/schema/product-protocol.schema.json"}) File.WriteAllText(path,schema.Json+"\n",new UTF8Encoding(false));
File.WriteAllText("schemas/agt-002/product-protocol.d.ts",ProductProtocolSchemaGenerator.GenerateTypeScript(),new UTF8Encoding(false));
File.WriteAllText("dsh/uniclaw-decision-channel/schema/schema-hash.txt",schema.SchemaHash+"\n",new UTF8Encoding(false));
Console.WriteLine(schema.SchemaHash);
