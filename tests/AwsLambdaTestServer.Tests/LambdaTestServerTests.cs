// Copyright (c) Martin Costello, 2019. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text;
using System.Text.Json;
using Amazon.Lambda.RuntimeSupport;
using MartinCostello.Logging.XUnit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MartinCostello.Testing.AwsLambdaTestServer;

public class LambdaTestServerTests : ITestOutputHelperAccessor
{
    public LambdaTestServerTests(ITestOutputHelper outputHelper)
    {
        OutputHelper = outputHelper;
    }

    public ITestOutputHelper? OutputHelper { get; set; }

    [Fact]
    public void Constructor_Validates_Parameters()
    {
        // Arrange
        LambdaTestServerOptions options = null!;

        // Act and Assert
        Assert.Throws<ArgumentNullException>("options", () => new LambdaTestServer(options));
    }

    [Fact]
    public void CreateClient_Throws_If_Not_Started()
    {
        // Arrange
        using var target = new LambdaTestServer();

        // Act
        Assert.Throws<InvalidOperationException>(() => target.CreateClient());
    }

    [Fact]
    public void CreateClient_Throws_If_Disposed()
    {
        // Arrange
        var target = new LambdaTestServer();
        target.Dispose();

        // Act
        Assert.Throws<ObjectDisposedException>(() => target.CreateClient());
    }

    [Fact]
    public async Task EnqueueAsync_Validates_Parameters()
    {
        // Arrange
        using var target = new LambdaTestServer();
        LambdaTestRequest request = null!;

        // Act
        await Assert.ThrowsAsync<ArgumentNullException>("request", () => target.EnqueueAsync(request));
    }

    [Fact]
    public async Task EnqueueAsync_Throws_If_Not_Started()
    {
        // Arrange
        using var target = new LambdaTestServer();
        var request = new LambdaTestRequest(Array.Empty<byte>());

        // Act and Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await target.EnqueueAsync(request));
    }

    [Fact]
    public async Task EnqueueAsync_Throws_If_Disposed()
    {
        // Arrange
        var target = new LambdaTestServer();
        target.Dispose();

        var request = new LambdaTestRequest(Array.Empty<byte>());

        // Act
        await Assert.ThrowsAsync<ObjectDisposedException>(() => target.EnqueueAsync(request));
    }

    [Fact]
    public async Task EnqueueAsync_Throws_If_Already_Enqueued()
    {
        // Arrange
        using var target = new LambdaTestServer();
        var request = new LambdaTestRequest(Array.Empty<byte>());

        await target.StartAsync();

        var context = await target.EnqueueAsync(request);

        context.ShouldNotBeNull();
        context.Request.ShouldBe(request);
        context.Response.ShouldNotBeNull();
        context.Response.Completion.IsCompleted.ShouldBeFalse();

        // Act and Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await target.EnqueueAsync(request));
    }

    [Fact]
    public async Task StartAsync_Throws_If_Already_Started()
    {
        // Act
        using var target = new LambdaTestServer();

        // Assert
        target.IsStarted.ShouldBeFalse();

        // Act
        await target.StartAsync();

        // Assert
        target.IsStarted.ShouldBeTrue();

        // Act and Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await target.StartAsync());
    }

    [Fact]
    public async Task StartAsync_Throws_If_WebHostBuilder_Null()
    {
        // Arrange
        using var target = new NullWebHostBuilderLambdaTestServer();

        // Act and Assert
        await Assert.ThrowsAsync<ArgumentNullException>("builder", () => target.StartAsync());
        target.IsStarted.ShouldBeFalse();
    }

    [Fact]
    public async Task Function_Can_Process_Request()
    {
        // Arrange
        void Configure(IServiceCollection services)
        {
            services.AddLogging((builder) => builder.AddXUnit(this));
        }

        using var server = new LambdaTestServer(Configure);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        await server.StartAsync(cts.Token);

        var context = await server.EnqueueAsync(@"{""Values"": [ 1, 2, 3 ]}");

        _ = Task.Run(async () =>
        {
            await context.Response.WaitToReadAsync(cts.Token);

            if (!cts.IsCancellationRequested)
            {
                cts.Cancel();
            }
        });

        using var httpClient = server.CreateClient();

        // Act
        await MyFunctionEntrypoint.RunAsync(httpClient, cts.Token);

        // Assert
        context.Response.TryRead(out var response).ShouldBeTrue();

        response.ShouldNotBeNull();
        response!.IsSuccessful.ShouldBeTrue();
        response.Content.ShouldNotBeNull();
        response.Duration.ShouldBeGreaterThan(TimeSpan.Zero);
        Encoding.UTF8.GetString(response.Content).ShouldBe(@"{""Sum"":6}");
    }

    [Fact]
    public async Task Function_Can_Process_Request_With_Mobile_Sdk_Headers()
    {
        // Arrange
        void Configure(IServiceCollection services)
        {
            services.AddLogging((builder) => builder.AddXUnit(this));
        }

        using var server = new LambdaTestServer(Configure);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        await server.StartAsync(cts.Token);

        byte[] content = Encoding.UTF8.GetBytes(@"{""Values"": [ 1, 2, 3 ]}");

        var request = new LambdaTestRequest(content)
        {
            ClientContext = "{}",
            CognitoIdentity = "{}",
        };

        var context = await server.EnqueueAsync(request);

        _ = Task.Run(async () =>
        {
            await context.Response.WaitToReadAsync(cts.Token);

            if (!cts.IsCancellationRequested)
            {
                cts.Cancel();
            }
        });

        using var httpClient = server.CreateClient();

        // Act
        await MyFunctionEntrypoint.RunAsync(httpClient, cts.Token);

        // Assert
        context.Response.TryRead(out var response).ShouldBeTrue();

        response.ShouldNotBeNull();
        response!.IsSuccessful.ShouldBeTrue();
    }

    [Fact]
    public async Task Function_Can_Handle_Failed_Request()
    {
        // Arrange
        void Configure(IServiceCollection services)
        {
            services.AddLogging((builder) => builder.AddXUnit(this));
        }

        using var server = new LambdaTestServer(Configure);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        await server.StartAsync(cts.Token);

        var context = await server.EnqueueAsync(@"{""Values"": null}");

        _ = Task.Run(async () =>
        {
            await context.Response.WaitToReadAsync(cts.Token);

            if (!cts.IsCancellationRequested)
            {
                cts.Cancel();
            }
        });

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
    public async Task Function_Can_Handle_Failed_Initialization()
    {
        // Arrange
        void Configure(IServiceCollection services)
        {
            services.AddLogging((builder) => builder.AddXUnit(this));
        }

        using var server = new LambdaTestServer(Configure);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        await server.StartAsync(cts.Token);

        var context = await server.EnqueueAsync(@"{""Values"": null}");

        _ = Task.Run(async () =>
        {
            await context.Response.WaitToReadAsync(cts.Token);

            if (!cts.IsCancellationRequested)
            {
                cts.Cancel();
            }
        });

        using var httpClient = server.CreateClient();

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => FunctionRunner.RunAsync<MyFailedInitializationHandler>(httpClient, cts.Token));
    }

    [Fact]
    public async Task Function_Can_Process_Multiple_Requests()
    {
        // Arrange
        void Configure(IServiceCollection services)
        {
            services.AddLogging((builder) => builder.AddXUnit(this));
        }

        using var server = new LambdaTestServer(Configure);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        await server.StartAsync(cts.Token);

        var channels = new List<(int Expected, LambdaTestContext Context)>();

        for (int i = 0; i < 10; i++)
        {
            var request = new MyRequest()
            {
                Values = Enumerable.Range(1, i + 1).ToArray(),
            };

            channels.Add((request.Values.Sum(), await server.EnqueueAsync(request)));
        }

        _ = Task.Run(async () =>
        {
            foreach ((var _, var context) in channels)
            {
                await context.Response.WaitToReadAsync(cts.Token);
            }

            if (!cts.IsCancellationRequested)
            {
                cts.Cancel();
            }
        });

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

    [Fact(Timeout = 5_000)]
    public async Task Function_Returns_If_No_Requests_Within_Timeout()
    {
        // Arrange
        void Configure(IServiceCollection services)
        {
            services.AddLogging((builder) => builder.AddXUnit(this));
        }

        using var server = new LambdaTestServer(Configure);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        await server.StartAsync(cts.Token);

        using var httpClient = server.CreateClient();

        // Act
        await MyFunctionEntrypoint.RunAsync(httpClient, cts.Token);

        // Assert
        cts.IsCancellationRequested.ShouldBeTrue();
    }

    [Fact]
    public void Finalizer_Does_Not_Throw()
    {
#pragma warning disable CA2000
        // Act (no Assert)
        _ = new LambdaTestServer();
#pragma warning restore CA2000
    }

    [Fact]
    public async Task Can_Use_Custom_Function_Variables()
    {
        // Arrange
        LambdaTestServer.ClearLambdaEnvironmentVariables();

        var options = new LambdaTestServerOptions()
        {
            FunctionArn = "my-custom-arn",
            FunctionHandler = "my-custom-handler",
            FunctionMemorySize = 1024,
            FunctionName = "my-function-name",
            FunctionTimeout = TimeSpan.FromSeconds(119),
            FunctionVersion = 42,
            LogGroupName = "my-log-group",
            LogStreamName = "my-log-stream",
        };

        using var server = new LambdaTestServer(options);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        await server.StartAsync(cts.Token);

        var request = new LambdaTestRequest(Array.Empty<byte>(), "my-request-id")
        {
            ClientContext = @"{""client"":{""app_title"":""my-app""}}",
            CognitoIdentity = @"{""identityId"":""my-identity""}",
        };

        var context = await server.EnqueueAsync(request);

        _ = Task.Run(async () =>
        {
            await context.Response.WaitToReadAsync(cts.Token);

            if (!cts.IsCancellationRequested)
            {
                cts.Cancel();
            }
        });

        using var httpClient = server.CreateClient();

        // Act
        await CustomFunction.RunAsync(httpClient, cts.Token);

        // Assert
        context.Response.TryRead(out var response).ShouldBeTrue();

        response.ShouldNotBeNull();
        response!.IsSuccessful.ShouldBeTrue();
        response.Content.ShouldNotBeNull();

        var lambdaContext = response.ReadAs<IDictionary<string, string>>();
        lambdaContext.ShouldContainKeyAndValue("AwsRequestId", request.AwsRequestId);
        lambdaContext.ShouldContainKeyAndValue("ClientContext", "my-app");
        lambdaContext.ShouldContainKeyAndValue("FunctionName", options.FunctionName);
        lambdaContext.ShouldContainKeyAndValue("FunctionVersion", "42");
        lambdaContext.ShouldContainKeyAndValue("IdentityId", "my-identity");
        lambdaContext.ShouldContainKeyAndValue("InvokedFunctionArn", options.FunctionArn);
        lambdaContext.ShouldContainKeyAndValue("LogGroupName", options.LogGroupName);
        lambdaContext.ShouldContainKeyAndValue("LogStreamName", options.LogStreamName);
        lambdaContext.ShouldContainKeyAndValue("MemoryLimitInMB", "1024");

        lambdaContext.ShouldContainKey("RemainingTime");
        string remainingTimeString = lambdaContext["RemainingTime"];

        TimeSpan.TryParse(remainingTimeString, out var remainingTime).ShouldBeTrue();

        remainingTime.Minutes.ShouldBe(options.FunctionTimeout.Minutes);
    }

    private static class CustomFunction
    {
        internal static async Task RunAsync(HttpClient httpClient, CancellationToken cancellationToken)
        {
            var handler = new CustomHandler();
            using var bootstrap = new LambdaBootstrap(httpClient, handler.InvokeAsync);

            await bootstrap.RunAsync(cancellationToken);
        }
    }

    private class CustomHandler
    {
        public virtual Task<InvocationResponse> InvokeAsync(InvocationRequest request)
        {
            var context = new Dictionary<string, string>()
            {
                ["AwsRequestId"] = request.LambdaContext.AwsRequestId,
                ["ClientContext"] = request.LambdaContext.ClientContext.Client.AppTitle,
                ["FunctionName"] = request.LambdaContext.FunctionName,
                ["FunctionVersion"] = request.LambdaContext.FunctionVersion,
                ["IdentityId"] = request.LambdaContext.Identity.IdentityId,
                ["InvokedFunctionArn"] = request.LambdaContext.InvokedFunctionArn,
                ["LogGroupName"] = request.LambdaContext.LogGroupName,
                ["LogStreamName"] = request.LambdaContext.LogStreamName,
                ["MemoryLimitInMB"] = request.LambdaContext.MemoryLimitInMB.ToString(CultureInfo.InvariantCulture),
                ["RemainingTime"] = request.LambdaContext.RemainingTime.ToString("G", CultureInfo.InvariantCulture),
            };

            byte[] json = JsonSerializer.SerializeToUtf8Bytes(context);

            var stream = new MemoryStream(json);

            return Task.FromResult(new InvocationResponse(stream, true));
        }
    }

    private sealed class MyFailedInitializationHandler : MyHandler
    {
        public override Task<bool> InitializeAsync()
        {
            throw new InvalidOperationException("Failed to initialize handler.");
        }
    }

    private sealed class NullWebHostBuilderLambdaTestServer : LambdaTestServer
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(null!);
        }
    }
}
