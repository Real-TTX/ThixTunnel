using ThixTunnel.Core.Infrastructure;
using ThixTunnel.Core.Models;

namespace ThixTunnel.Tests;

public sealed class SshErrorClassifierTests
{
    [Theory]
    [InlineData("tunneluser@10.0.0.24: Permission denied (publickey).", TunnelProblem.AuthenticationRequired)]
    [InlineData("Host key verification failed.", TunnelProblem.HostKeyUnknown)]
    [InlineData("No ED25519 host key is known for gateway.example.com and you have requested strict checking.\r\nHost key verification failed.", TunnelProblem.HostKeyUnknown)]
    [InlineData("@@@@@\r\n@    WARNING: REMOTE HOST IDENTIFICATION HAS CHANGED!     @\r\nHost key verification failed.", TunnelProblem.HostKeyChanged)]
    [InlineData("bind [127.0.0.1]:4455: Address already in use\r\nchannel_setup_fwd_listener_tcpip: cannot listen to port: 4455", TunnelProblem.LocalPortInUse)]
    [InlineData("ssh: Could not resolve hostname gateway.example.com: No such host is known.", TunnelProblem.HostUnreachable)]
    [InlineData("ssh: connect to host 10.0.0.24 port 22: Connection timed out", TunnelProblem.HostUnreachable)]
    [InlineData("Timeout, server 10.0.0.24 not responding.", TunnelProblem.ConnectionLost)]
    [InlineData("something new", TunnelProblem.Unknown)]
    public void RecognisesTypicalSshErrors(string output, TunnelProblem expected) =>
        Assert.Equal(expected, SshErrorClassifier.Classify(output));

    [Theory]
    [InlineData(TunnelProblem.HostUnreachable, true)]
    [InlineData(TunnelProblem.ConnectionLost, true)]
    [InlineData(TunnelProblem.AuthenticationRequired, false)]
    [InlineData(TunnelProblem.HostKeyUnknown, false)]
    [InlineData(TunnelProblem.HostKeyChanged, false)]
    [InlineData(TunnelProblem.LocalPortInUse, false)]
    public void OnlyProblemsThatCanSolveThemselvesAreRetried(TunnelProblem problem, bool retry) =>
        Assert.Equal(retry, SshErrorClassifier.IsRetryable(problem));

    [Fact]
    public void LastLineSkipsTheBannerOfAChangedHostKey() =>
        Assert.Equal("Host key verification failed.",
            SshErrorClassifier.LastLine("@@@@\n@ WARNING @\nHost key verification failed.\n@@@@\n"));
}
