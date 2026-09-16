using RomM.Client;
using RomMBat.Agent.Commands;
using RomMBat.Core.Sets;
using RomMBat.Core.Sync;
using Xunit;

namespace RomMBat.Agent.Tests;

/// <summary>
/// Which exit code a failure earns, which is what a script and the ES hooks branch on.
/// </summary>
public sealed class ExitCodeTests
{
    [Theory]
    [InlineData(FailureCause.Unreachable, ExitCode.Offline)]
    [InlineData(FailureCause.None, ExitCode.Offline)]
    [InlineData(FailureCause.Failed, ExitCode.ServerError)]
    [InlineData(FailureCause.NotAuthorized, ExitCode.NotPaired)]
    public void Offline_is_reserved_for_the_failure_that_clears_itself(FailureCause cause, int expected) =>
        Assert.Equal(expected, ExitCode.For(cause));

    [Theory]
    [InlineData(RomMResponseStatus.Unauthorized, ExitCode.NotPaired)]
    [InlineData(RomMResponseStatus.Forbidden, ExitCode.NotPaired)]
    [InlineData(RomMResponseStatus.ServerError, ExitCode.ServerError)]
    [InlineData(RomMResponseStatus.NotFound, ExitCode.ServerError)]
    public void A_server_answer_is_never_offline(RomMResponseStatus status, int expected) =>
        Assert.Equal(expected, ExitCode.For(status));

    [Fact]
    public void A_sync_the_server_rejected_exits_not_paired_rather_than_ok()
    {
        // SyncState.Rejected used to fall through the default arm, so a 401 mid-run exited 0.
        Assert.Equal(ExitCode.NotPaired, SyncCommand.ExitCodeFor(new SyncReport(SyncState.Rejected, [])));
    }

    [Theory]
    [InlineData(FailureCause.Unreachable, ExitCode.Offline)]
    [InlineData(FailureCause.Failed, ExitCode.ServerError)]
    [InlineData(FailureCause.NotAuthorized, ExitCode.NotPaired)]
    public void An_incomplete_sync_exits_on_its_worst_cause(FailureCause cause, int expected) =>
        Assert.Equal(expected, SyncCommand.ExitCodeFor(new SyncReport(SyncState.Incomplete, [], cause)));
}
