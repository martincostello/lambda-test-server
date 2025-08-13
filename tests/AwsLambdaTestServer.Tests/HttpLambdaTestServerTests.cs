// Copyright (c) Martin Costello, 2019. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MartinCostello.Testing.AwsLambdaTestServer;

#pragma warning disable JSON002

[Collection<LambdaTestServerCollection>]
public class HttpLambdaTestServerTests(ITestOutputHelper outputHelper) : FunctionTests(outputHelper)
{
    [Fact]
    public async Task Function_Can_Process_Request()
    {
        // Arrange
        void Configure(IServiceCollection services)
            => services.AddLogging((builder) => builder.AddXUnit(this));

        using var server = new HttpLambdaTestServer(Configure);

        await WithServerAsync(server, async static (server, cts) =>
        {
            var context = await server.EnqueueAsync("""{"Values": [ 1, 2, 3 ]}""");

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
        });
    }

    [Fact]
    public async Task Function_Can_Handle_Failed_Request()
    {
        // Arrange
        void Configure(IServiceCollection services)
            => services.AddLogging((builder) => builder.AddXUnit(this));

        using var server = new LambdaTestServer(Configure);

        await WithServerAsync(server, async static (server, cts) =>
        {
            var context = await server.EnqueueAsync("""{"Values": null}""");

            using var httpClient = server.CreateClient();

            // Act
            await MyFunctionEntrypoint.RunAsync(httpClient, cts.Token);

            // Assert
            context.Response.TryRead(out var response).ShouldBeTrue();

            response.ShouldNotBeNull();
            response!.IsSuccessful.ShouldBeFalse();
            response.Content.ShouldNotBeNull();
        });
    }

    [Fact]
    public async Task Function_Can_Process_Multiple_Requests()
    {
        // Arrange
        void Configure(IServiceCollection services)
            => services.AddLogging((builder) => builder.AddXUnit(this));

        using var server = new LambdaTestServer(Configure);

        await WithServerAsync(server, async static (server, cts) =>
        {
            int received = 0;
            int count = 10;

            server.OnInvocationCompleted = async (_, _) =>
            {
                if (Interlocked.Increment(ref received) == count)
                {
                    await cts.CancelAsync();
                }
            };

            var channels = new List<(int Expected, LambdaTestContext Context)>();

            for (int i = 0; i < count; i++)
            {
                var request = new MyRequest()
                {
                    Values = [.. Enumerable.Range(1, i + 1)],
                };

                channels.Add((request.Values.Sum(), await server.EnqueueAsync(request)));
            }

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

            received.ShouldBe(count, "Not all requests were processed.");
        });
    }
}
