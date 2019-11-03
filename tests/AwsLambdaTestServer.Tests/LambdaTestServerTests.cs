// Copyright (c) Martin Costello, 2019. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MartinCostello.Logging.XUnit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shouldly;
using Xunit;
using Xunit.Abstractions;

namespace MartinCostello.Testing.AwsLambdaTestServer
{
    public class LambdaTestServerTests : ITestOutputHelperAccessor
    {
        public LambdaTestServerTests(ITestOutputHelper outputHelper)
        {
            OutputHelper = outputHelper;
        }

        public ITestOutputHelper OutputHelper { get; set; }

        [Fact]
        public void Constructor_Validates_Parameters()
        {
            // Arrange
            LambdaTestServerOptions options = null;

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
            LambdaTestRequest request = null;

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
            await target.EnqueueAsync(request);

            // Act and Assert
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await target.EnqueueAsync(request));
        }

        [Fact]
        public async Task StartAsync_Throws_If_Already_Started()
        {
            // Arrange
            using var target = new LambdaTestServer();
            await target.StartAsync();

            // Act and Assert
            await Assert.ThrowsAsync<InvalidOperationException>(async () => await target.StartAsync());
        }

        [Fact]
        public async Task StartAsync_Throws_If_WebHostBuilder_Null()
        {
            // Arrange
            using var target = new NullWebHostBuilderLambdaTestServer();

            // Act and Assert
            await Assert.ThrowsAsync<ArgumentNullException>("builder",  () => target.StartAsync());
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
            response.IsSuccessful.ShouldBeTrue();
            response.Content.ShouldNotBeNull();
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
            response.IsSuccessful.ShouldBeTrue();
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
            response.IsSuccessful.ShouldBeFalse();
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

            var channels = new List<(int expected, LambdaTestContext context)>();

            for (int i = 0; i < 10; i++)
            {
                var request = new MyRequest()
                {
                    Values = Enumerable.Range(1, i + 1).ToArray(),
                };

                string json = JsonSerializer.Serialize(request);

                channels.Add((request.Values.Sum(), await server.EnqueueAsync(json)));
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
                response.IsSuccessful.ShouldBeTrue();
                response.Content.ShouldNotBeNull();

                var deserialized = JsonSerializer.Deserialize<MyResponse>(response.Content);
                deserialized.Sum.ShouldBe(expected);
            }
        }

        [Fact]
        public void Finalizer_Does_Not_Throw()
        {
#pragma warning disable CA2000
            // Act (no Assert)
            _ = new LambdaTestServer();
#pragma warning restore CA2000
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
                base.ConfigureWebHost(null);
            }
        }
    }
}
