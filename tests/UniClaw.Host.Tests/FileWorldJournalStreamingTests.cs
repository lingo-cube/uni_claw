using System.Text.Json;
using UniClaw.Host.Runtime;
using UniClaw.Kernel.World;
using Xunit;
namespace UniClaw.Host.Tests;

// GC.GetTotalMemory measures the whole process. Run this collection without
// other collections allocating concurrently; keep the original memory limit.
[CollectionDefinition("WorldJournalMemory", DisableParallelization = true)]
public sealed class WorldJournalMemoryCollection { }

[Collection("WorldJournalMemory")]
public sealed class FileWorldJournalStreamingTests
{
    [Fact]
    public void OriginalWriterDoesNotKeepASecondFullHistoryInManagedMemory()
    {
        var path=Path.Combine(Path.GetTempPath(),"world-stream-"+Guid.NewGuid().ToString("N"));
        var data=JsonSerializer.SerializeToElement(new { recordedPayload=new string('x',256*1024) });
        try
        {
            using var journal=new FileWorldJournal(path);
            // Warm serializer metadata before the broad retained-memory check.
            journal.Append(new(1,"fixture",JsonSerializer.SerializeToElement(new { warmup=true })));
            var before=GC.GetTotalMemory(true);
            for(var i=2;i<=97;i++)journal.Append(new(i,"fixture",data));
            var retained=GC.GetTotalMemory(true)-before;
            GC.KeepAlive(journal);
            Assert.True(retained < 24L*1024*1024,$"Writer retained {retained} bytes for 96 already-persisted payloads");
            Assert.Equal(97,journal.ReadRecords().Count);
        }
        finally { File.Delete(path);File.Delete(path+".head"); }
    }
    [Fact]
    public void WriterReadPreservesFullPrefixAndCanContinueOriginalSequence()
    {
        var path=Path.Combine(Path.GetTempPath(),"world-stream-"+Guid.NewGuid().ToString("N"));
        var data=JsonSerializer.SerializeToElement(new { title="Network & internet 日本語", entries=new[]{1,2,3}, nested=new { visible=true } });
        try
        {
            string recording;
            using(var writer=new FileWorldJournal(path))
            {
                recording=writer.RecordingId;
                Assert.Empty(writer.ReadRecords());
                writer.Append(new(1,"first",data));
                var first=writer.ReadRecords();Assert.Single(first);Assert.True(JsonElement.DeepEquals(data,first[0].Data));
                writer.Append(new(2,"second",data));
                Assert.Single(first);Assert.Equal(2,writer.ReadRecords().Count);
                Assert.Throws<InvalidDataException>(()=>writer.Append(new(2,"duplicate",data)));
            }
            using var reader=new FileWorldJournal(path,readOnly:true);
            Assert.Equal(recording,reader.RecordingId);
            Assert.Equal(new[]{1,2},reader.ReadRecords().Select(r=>r.Sequence));
            Assert.All(reader.ReadRecords(),r=>Assert.True(JsonElement.DeepEquals(data,r.Data)));
        }
        finally { File.Delete(path);File.Delete(path+".head"); }
    }
}
