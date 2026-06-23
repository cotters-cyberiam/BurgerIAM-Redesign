using Grpc.Core;

namespace MenuService.Tests;

public sealed class MockServerCallContext : ServerCallContext
{
    private readonly CancellationToken _cancellationToken;

    public MockServerCallContext(CancellationToken cancellationToken = default)
    {
        _cancellationToken = cancellationToken;
    }

    protected override string MethodCore => string.Empty;
    protected override string HostCore => string.Empty;
    protected override string PeerCore => string.Empty;
    protected override DateTime DeadlineCore => DateTime.MaxValue;
    protected override Metadata RequestHeadersCore => [];
    protected override CancellationToken CancellationTokenCore => _cancellationToken;
    protected override Metadata ResponseTrailersCore => [];
    protected override Status StatusCore { get => Status.DefaultSuccess; set => throw new NotImplementedException(); }
    protected override WriteOptions? WriteOptionsCore { get => null; set => throw new NotImplementedException(); }
    protected override AuthContext AuthContextCore => throw new NotImplementedException();

    protected override Task WriteResponseHeadersAsyncCore(Metadata responseHeaders)
        => Task.CompletedTask;

    protected override ContextPropagationToken CreatePropagationTokenCore(ContextPropagationOptions? options)
        => throw new NotImplementedException();
}
