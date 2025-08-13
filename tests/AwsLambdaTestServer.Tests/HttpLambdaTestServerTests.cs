// Copyright (c) Martin Costello, 2019. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using MartinCostello.Logging.XUnit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MartinCostello.Testing.AwsLambdaTestServer;

#pragma warning disable JSON002

[Collection<LambdaTestServerCollection>]
public class HttpLambdaTestServerTests(ITestOutputHelper outputHelper) : ITestOutputHelperAccessor
{
    public ITestOutputHelper? OutputHelper { get; set; } = outputHelper;

    [Fact]
    public async Task Function_Can_Process_Request()
    {
        // Arrange
        void Configure(IServiceCollection services)
            => services.AddLogging((builder) => builder.AddXUnit(this));

        using var server = new HttpLambdaTestServer(Configure);
        using var cts = new CancellationTokenSource();

        await server.StartAsync(cts.Token);

        cts.CancelAfter(TimeSpan.FromSeconds(2));

        var context = await server.EnqueueAsync("""{"Values": [ 1, 2, 3 ]}""");

        _ = Task.Run(
            async () =>
            {
                await context.Response.WaitToReadAsync(cts.Token);

                if (!cts.IsCancellationRequested)
                {
                    await cts.CancelAsync();
                }
            },
            cts.Token);

        using var httpClient = server.CreateClient();

        // Act
        await MyFunctionEntrypoint.RunAsync(httpClient, cts.Token);

        // Assert
        context.Response.TryRead(out var response).ShouldBeTrue();

        response.ShouldNotBeNull();
        response!.IsSuccessful.ShouldBeTrue();
        response.Content.ShouldNotBeNull();
        response.Duration.ShouldBeGreaterThan(TimeSpan.Zero);
        Encoding.UTF8.GetString(response.Content).ShouldBe("""{"Sum":6}""");
    }

    [Fact]
    public async Task Function_Can_Handle_Failed_Request()
    {
        // Arrange
        void Configure(IServiceCollection services)
            => services.AddLogging((builder) => builder.AddXUnit(this));

        using var server = new LambdaTestServer(Configure);
        using var cts = new CancellationTokenSource();

        await server.StartAsync(cts.Token);

        cts.CancelAfter(TimeSpan.FromSeconds(2));

        var context = await server.EnqueueAsync("""{"Values": null}""");

        _ = Task.Run(
            async () =>
            {
                await context.Response.WaitToReadAsync(cts.Token);

                if (!cts.IsCancellationRequested)
                {
                    await cts.CancelAsync();
                }
            },
            cts.Token);

        using var httpClient = server.CreateClient();

        // Act
        await MyFunctionEntrypoint.RunAsync(httpClient, cts.Token);

        // Assert
        context.Response.TryRead(out var response).ShouldBeTrue();

        response.ShouldNotBeNull();
        response!.IsSuccessful.ShouldBeFalse();
        response.Content.ShouldNotBeNull();
    }

    [Fact]
    public async Task Function_Can_Process_Multiple_Requests()
    {
        // Arrange
        void Configure(IServiceCollection services)
            => services.AddLogging((builder) => builder.AddXUnit(this));

        using var server = new LambdaTestServer(Configure);
        using var cts = new CancellationTokenSource();

        await server.StartAsync(cts.Token);

        cts.CancelAfter(TimeSpan.FromSeconds(2));

        var channels = new List<(int Expected, LambdaTestContext Context)>();

        for (int i = 0; i < 10; i++)
        {
            var request = new MyRequest()
            {
                Values = [.. Enumerable.Range(1, i + 1)],
            };

            channels.Add((request.Values.Sum(), await server.EnqueueAsync(request)));
        }

        _ = Task.Run(
            async () =>
            {
                foreach ((var _, var context) in channels)
                {
                    await context.Response.WaitToReadAsync(cts.Token);
                }

                if (!cts.IsCancellationRequested)
                {
                    await cts.CancelAsync();
                }
            },
            cts.Token);

        using var httpClient = server.CreateClient();

        // Act
        await MyFunctionEntrypoint.RunAsync(httpClient, cts.Token);

        // Assert
        foreach ((int expected, var context) in channels)
        {
            context.Response.TryRead(out var response).ShouldBeTrue();

            response.ShouldNotBeNull();
            response!.IsSuccessful.ShouldBeTrue();
            response.Content.ShouldNotBeNull();

            var deserialized = response.ReadAs<MyResponse>();
            deserialized.Sum.ShouldBe(expected);
        }
    }
}
