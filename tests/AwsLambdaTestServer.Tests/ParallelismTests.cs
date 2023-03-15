// Copyright (c) Martin Costello, 2019. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MyFunctions;
using Shouldly;
using Xunit;

namespace MartinCostello.Testing.AwsLambdaTestServer;

public static class ParallelismTests
{
    [Fact(Timeout = 30_000)]
    public static async Task Function_Can_Process_Multiple_Requests_On_Different_Threads()
    {
        // Arrange
        int messageCount = 1_000;
        int expected = Enumerable.Range(0, messageCount).Sum();

        using var server = new LambdaTestServer();
        using var cts = new CancellationTokenSource();

        await server.StartAsync(cts.Token);

        using var httpClient = server.CreateClient();

        // Enqueue the requests to process in the background
        var completedAdding = EnqueueInParallel(messageCount, server);

        // Start a task to consume the responses in the background
        var completedProcessing = Assert(completedAdding, messageCount, cts);

        // Act - Start the function processing
        await ReverseFunction.RunAsync(httpClient, cts.Token);

        // Assert
        int actual = await completedProcessing.Task;
        actual.ShouldBe(expected);
    }

    private static TaskCompletionSource<IReadOnlyCollection<LambdaTestContext>> EnqueueInParallel(
        int count,
        LambdaTestServer server)
    {
        var collection = new ConcurrentBag<LambdaTestContext>();
        var completionSource = new TaskCompletionSource<IReadOnlyCollection<LambdaTestContext>>();

        int queued = 0;

        // Enqueue the specified number of items in parallel
        Parallel.For(0, count, async (i) =>
        {
            var context = await server.EnqueueAsync(new[] { i });

            collection.Add(context);

            if (Interlocked.Increment(ref queued) == count)
            {
                completionSource.SetResult(collection);
            }
        });

        return completionSource;
    }

    private static TaskCompletionSource<int> Assert(
        TaskCompletionSource<IReadOnlyCollection<LambdaTestContext>> completedAdding,
        int messages,
        CancellationTokenSource cts)
    {
        var completionSource = new TaskCompletionSource<int>();

        _ = Task.Run(async () =>
        {
            var collection = await completedAdding.Task;
            collection.Count.ShouldBe(messages);

            int actual = 0;

            foreach (var context in collection)
            {
                await context.Response.WaitToReadAsync();

                var result = await context.Response.ReadAsync();

                var response = result.ReadAs<int[]>();

                actual += response[0];
            }

            completionSource.SetResult(actual);
            await cts.CancelAsync();
        });

        return completionSource;
    }
}
