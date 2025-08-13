// Copyright (c) Martin Costello, 2019. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using MartinCostello.Logging.XUnit;

namespace MartinCostello.Testing.AwsLambdaTestServer;

public abstract class FunctionTests(ITestOutputHelper outputHelper) : ITestOutputHelperAccessor
{
    public ITestOutputHelper? OutputHelper
    {
        get => outputHelper;
        set => throw new NotSupportedException();
    }

    protected virtual TimeSpan Timeout => TimeSpan.FromSeconds(2);

    protected static CancellationTokenSource CancellationTokenSourceForTimeout(CancellationTokenSource other)
        => CancellationTokenSource.CreateLinkedTokenSource(other.Token, TestContext.Current.CancellationToken);

    protected CancellationTokenSource CancellationTokenSourceForShutdown() => new(Timeout);

    protected async Task WithServerAsync(LambdaTestServer server, Func<LambdaTestServer, CancellationTokenSource, Task> action)
    {
        using var shutdownAfter = CancellationTokenSourceForShutdown();
        using var linkedToken = CancellationTokenSourceForTimeout(shutdownAfter);

        server.OnInvocationCompleted = async (_, _) => await linkedToken.CancelAsync();

        await server.StartAsync(linkedToken.Token);

        await action(server, linkedToken);
    }
}
