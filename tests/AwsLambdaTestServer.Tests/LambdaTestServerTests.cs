// Copyright (c) Martin Costello, 2019. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json;
using Amazon.Lambda.RuntimeSupport;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace MartinCostello.Testing.AwsLambdaTestServer;

#pragma warning disable JSON002

[Collection<LambdaTestServerCollection>]
public class LambdaTestServerTests(ITestOutputHelper outputHelper) : FunctionTests(outputHelper)
{
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
        Assert.Throws<InvalidOperationException>(target.CreateClient);
    }

    [Fact]
    public void CreateClient_Throws_If_Disposed()
    {
        // Arrange
        var target = new LambdaTestServer();
        target.Dispose();

        // Act
        Assert.Throws<ObjectDisposedException>(target.CreateClient);
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
        var request = new LambdaTestRequest([]);

        // Act and Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => target.EnqueueAsync(request));
    }

    [Fact]
    public async Task EnqueueAsync_Throws_If_Disposed()
    {
        // Arrange
        var target = new LambdaTestServer();
        target.Dispose();

        var request = new LambdaTestRequest([]);

        // Act
        await Assert.ThrowsAsync<ObjectDisposedException>(() => target.EnqueueAsync(request));
    }

    [Fact]
    public async Task EnqueueAsync_Throws_If_Already_Enqueued()
    {
        // Arrange
        using var target = new LambdaTestServer();
        var request = new LambdaTestRequest([]);

        await target.StartAsync(TestContext.Current.CancellationToken);

        var context = await target.EnqueueAsync(request);

        context.ShouldNotBeNull();
        context.Request.ShouldBe(request);
        context.Response.ShouldNotBeNull();
        context.Response.Completion.IsCompleted.ShouldBeFalse();

        // Act and Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => target.EnqueueAsync(request));
    }

    [Fact]
    public async Task StartAsync_Throws_If_Already_Started()
    {
        // Act
        using var target = new LambdaTestServer();

        // Assert
        target.IsStarted.ShouldBeFalse();

        // Act
        await target.StartAsync(TestContext.Current.CancellationToken);

        // Assert
        target.IsStarted.ShouldBeTrue();

        // Act and Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => target.StartAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task StartAsync_Throws_If_Server_Has_No_Addresses()
    {
        // Arrange
        using var target = new NullServerAddressesLambdaTestServer();

        // Act and Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => target.StartAsync(TestContext.Current.CancellationToken));

        target.IsStarted.ShouldBeFalse();
        exception.Message.ShouldBe("No server addresses are available.");
    }

    [Fact]
    public async Task StartAsync_Throws_If_WebHostBuilder_Null()
    {
        // Arrange
        using var target = new NullWebHostBuilderLambdaTestServer();

        // Act and Assert
        await Assert.ThrowsAsync<ArgumentNullException>("builder", () => target.StartAsync(TestContext.Current.CancellationToken));
        target.IsStarted.ShouldBeFalse();
    }

    [Fact]
    public async Task Function_Can_Process_Request()
    {
        // Arrange
        using var server = new LambdaTestServer(ConfigureLogging);

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
    public async Task Function_Can_Process_Request_With_Mobile_Sdk_Headers()
    {
        // Arrange
        using var server = new LambdaTestServer(ConfigureLogging);

        await WithServerAsync(server, async static (server, cts) =>
        {
            byte[] content = Encoding.UTF8.GetBytes("""{"Values": [ 1, 2, 3 ]}""");

            var request = new LambdaTestRequest(content)
            {
                ClientContext = "{}",
                CognitoIdentity = "{}",
            };

            var context = await server.EnqueueAsync(request);

            using var httpClient = server.CreateClient();

            // Act
            await MyFunctionEntrypoint.RunAsync(httpClient, cts.Token);

            // Assert
            context.Response.TryRead(out var response).ShouldBeTrue();

            response.ShouldNotBeNull();
            response!.IsSuccessful.ShouldBeTrue();
        });
    }

    [Fact]
    public async Task Function_Can_Handle_Failed_Request()
    {
        // Arrange
        using var server = new LambdaTestServer(ConfigureLogging);

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
    public async Task Function_Can_Handle_Failed_Initialization()
    {
        // Arrange
        using var server = new LambdaTestServer(ConfigureLogging);

        await WithServerAsync(server, async static (server, cts) =>
        {
            await server.EnqueueAsync("""{"Values": null}""");

            using var httpClient = server.CreateClient();

            // Act and Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => FunctionRunner.RunAsync<MyFailedInitializationHandler>(httpClient, cts.Token));
        });
    }

    [Fact]
    public async Task Function_Can_Process_Multiple_Requests()
    {
        // Arrange
        using var server = new LambdaTestServer(ConfigureLogging);

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

    [Fact(Timeout = 5_000)]
    public async Task Function_Returns_If_No_Requests_Within_Timeout()
    {
        // Arrange
        using var server = new LambdaTestServer(ConfigureLogging);

        await WithServerAsync(server, async static (server, cts) =>
        {
            using var httpClient = server.CreateClient();

            // Act
            await MyFunctionEntrypoint.RunAsync(httpClient, cts.Token);

            // Assert
            cts.IsCancellationRequested.ShouldBeTrue();
        });
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

        await WithServerAsync(server, async (server, cts) =>
        {
            var request = new LambdaTestRequest([], "my-request-id")
            {
                ClientContext = """{"client":{"app_title":"my-app"}}""",
                CognitoIdentity = """{"cognitoIdentityId":"my-identity"}""",
            };

            var context = await server.EnqueueAsync(request);

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
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Can_Enforce_Memory_Limit(bool disableMemoryLimitCheck)
    {
        Assert.SkipWhen(OperatingSystem.IsMacOS(), "Changing the GC memory limits is not supported on macOS.");

        // Arrange
        LambdaTestServer.ClearLambdaEnvironmentVariables();
        AssemblyFixture.ResetMemoryLimits();

        var options = new LambdaTestServerOptions()
        {
            DisableMemoryLimitCheck = disableMemoryLimitCheck,
            FunctionMemorySize = 128,
        };

        using var server = new LambdaTestServer(options);

        await WithServerAsync(server, async (server, cts) =>
        {
            var request = new LambdaTestRequest([]);

            var context = await server.EnqueueAsync(request);

            using var httpClient = server.CreateClient();

            // Act
            await MemoryInfoFunction.RunAsync(httpClient, cts.Token);

            // Assert
            context.Response.TryRead(out var response).ShouldBeTrue();

            response.ShouldNotBeNull();
            response!.IsSuccessful.ShouldBeTrue();
            response.Content.ShouldNotBeNull();

            var lambdaContext = response.ReadAs<IDictionary<string, string>>();
            lambdaContext.ShouldContainKeyAndValue("MemoryLimitInMB", "128");

            lambdaContext.ShouldContainKey("TotalAvailableMemoryBytes");
            var availableMemory = lambdaContext["TotalAvailableMemoryBytes"];

            if (disableMemoryLimitCheck)
            {
                availableMemory.ShouldNotBe("134217728");
            }
            else
            {
                availableMemory.ShouldBe("134217728");
            }
        });
    }

    [Fact]
    [Obsolete("Tests obsolete CreateServer() method.")]
    public async Task CreateServer_Returns_A_Non_Functional_Server()
    {
        // Arrange
        var builder = Substitute.For<IWebHostBuilder>();
        var cancellationToken = TestContext.Current.CancellationToken;

        using var target = new ObsoleteLambdaTestServer();
        using var server = target.CreateTestServer(builder);

        // Act and Assert
        Assert.Throws<NotSupportedException>(() => server.Features);
        await Assert.ThrowsAsync<NotSupportedException>(() => server.StartAsync<object>(default!, cancellationToken));
        await Assert.ThrowsAsync<NotSupportedException>(() => server.StopAsync(TestContext.Current.CancellationToken));
    }

    private void ConfigureLogging(IServiceCollection services)
        => services.AddLogging((builder) => builder.AddXUnit(this));

    private static class CustomFunction
    {
        internal static async Task RunAsync(HttpClient httpClient, CancellationToken cancellationToken)
        {
            var handler = new CustomHandler();
            using var bootstrap = new LambdaBootstrap(httpClient, handler.InvokeAsync);

            await bootstrap.RunAsync(cancellationToken);
        }
    }

    private static class MemoryInfoFunction
    {
        internal static async Task RunAsync(HttpClient httpClient, CancellationToken cancellationToken)
        {
            var handler = new MemoryInfoHandler();
            using var bootstrap = new LambdaBootstrap(httpClient, handler.InvokeAsync);

            await bootstrap.RunAsync(cancellationToken);
        }
    }

    private sealed class CustomHandler
    {
#pragma warning disable CA1822
        public Task<InvocationResponse> InvokeAsync(InvocationRequest request)
#pragma warning restore CA1822
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

    private sealed class MemoryInfoHandler
    {
#pragma warning disable CA1822
        public Task<InvocationResponse> InvokeAsync(InvocationRequest request)
#pragma warning restore CA1822
        {
            GC.RefreshMemoryLimit();
            var memoryInfo = GC.GetGCMemoryInfo();

            var context = new Dictionary<string, string>()
            {
                ["MemoryLimitInMB"] = request.LambdaContext.MemoryLimitInMB.ToString(CultureInfo.InvariantCulture),
                ["TotalAvailableMemoryBytes"] = memoryInfo.TotalAvailableMemoryBytes.ToString(CultureInfo.InvariantCulture),
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

    private sealed class NullServerAddressesLambdaTestServer : LambdaTestServer
    {
        protected override void ConfigureServices(IServiceCollection services)
        {
            base.ConfigureServices(services);

            var serverAddresses = Substitute.For<IServerAddressesFeature>();

            serverAddresses.Addresses.Returns([]);

            var server = Substitute.For<IServer>();

            var featureCollection = new FeatureCollection();

            server.Features.Returns(featureCollection);

            services.AddSingleton(server);
        }
    }

    private sealed class ObsoleteLambdaTestServer() : LambdaTestServer(new LambdaTestServerOptions())
    {
        [Obsolete]
        public IServer CreateTestServer(IWebHostBuilder builder)
            => base.CreateServer(builder);
    }
}
