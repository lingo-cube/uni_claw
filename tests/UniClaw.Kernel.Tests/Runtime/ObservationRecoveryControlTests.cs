using UniClaw.Kernel.Control;
using Xunit;

namespace UniClaw.Kernel.Tests.Runtime;

public sealed class ObservationRecoveryControlTests
{
    [Fact]
    public void ResumeDoesNotRestartDeadlineOrReplenishRequests()
    {
        var now = 0L;
        var control = new ObservationRecoveryControl(TimeSpan.FromSeconds(2), 2, () => now, 1000);
        Assert.True(control.TrySchedule("attempt:1", out var first));
        now = 1000;
        Assert.True(control.TrySchedule("attempt:1", out var second));
        Assert.Equal(TimeSpan.FromSeconds(1), second);
        Assert.False(control.TrySchedule("attempt:1", out _));
        control.Complete("attempt:1");
        Assert.False(control.TrySchedule("attempt:2", out _)); // run quota retained
        Assert.Equal(2, control.RequestsUsed);
    }

    [Fact]
    public void LateCompletionCannotReopenExpiredWait()
    {
        var now = 0L;
        var control = new ObservationRecoveryControl(TimeSpan.FromSeconds(2), 4, () => now, 1000);
        Assert.True(control.TrySchedule("attempt:1", out _));
        now = 2001;
        Assert.False(control.CanAccept("attempt:1"));
        Assert.False(control.TrySchedule("attempt:1", out _));
        Assert.False(control.CanAccept("attempt:other"));
    }
}
