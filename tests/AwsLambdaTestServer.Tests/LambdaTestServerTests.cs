// Copyright (c) Martin Costello, 2019. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using MartinCostello.Logging.XUnit;
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

            var reader = await server.EnqueueAsync(@"{""Values"": [ 1, 2, 3 ]}");

            _ = Task.Run(async () =>
            {
                await reader.WaitToReadAsync(cts.Token);

                if (!cts.IsCancellationRequested)
                {
                    cts.Cancel();
                }
            });

            using var httpClient = server.CreateClient();

            // Act
            await MyFunctionEntrypoint.RunAsync(httpClient, cts.Token);

            // Assert
            reader.TryRead(out var response).ShouldBeTrue();

            response.ShouldNotBeNull();
            response.IsSuccessful.ShouldBeTrue();
            response.Content.ShouldNotBeNull();
            Encoding.UTF8.GetString(response.Content).ShouldBe(@"{""Sum"":6}");
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

            var channels = new List<(int expected, ChannelReader<LambdaTestResponse> reader)>();

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
                foreach ((var _, var reader) in channels)
                {
                    await reader.WaitToReadAsync(cts.Token);
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
            foreach ((int expected, var channel) in channels)
            {
                channel.TryRead(out var response).ShouldBeTrue();

                response.ShouldNotBeNull();
                response.IsSuccessful.ShouldBeTrue();
                response.Content.ShouldNotBeNull();

                var deserialized = JsonSerializer.Deserialize<MyResponse>(response.Content);
                deserialized.Sum.ShouldBe(expected);
            }
        }
    }
}
