﻿// Copyright (c) Martin Costello, 2019. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MartinCostello.Logging.XUnit;
using MartinCostello.Testing.AwsLambdaTestServer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace MyFunctions
{
    public class ReverseFunctionWithLoggingTests : ITestOutputHelperAccessor
    {
        public ReverseFunctionWithLoggingTests(ITestOutputHelper outputHelper)
        {
            OutputHelper = outputHelper;
        }

        public ITestOutputHelper? OutputHelper { get; set; }

        [Fact]
        public async Task Function_Reverses_Numbers_With_Logging()
        {
            // Arrange
            using var server = new LambdaTestServer(
                (services) => services.AddLogging(
                    (builder) => builder.AddXUnit(this)));

            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(1));

            await server.StartAsync(cancellationTokenSource.Token);

            int[] value = new[] { 1, 2, 3 };
            byte[] json = JsonSerializer.SerializeToUtf8Bytes(value);

            LambdaTestContext context = await server.EnqueueAsync(json);

            using var httpClient = server.CreateClient();

            // Act
            await ReverseFunction.RunAsync(httpClient, cancellationTokenSource.Token);

            // Assert
            Assert.True(context.Response.TryRead(out LambdaTestResponse? response));
            Assert.NotNull(response);
            Assert.True(response!.IsSuccessful);

            var actual = JsonSerializer.Deserialize<int[]>(response.Content);

            Assert.NotNull(actual);
            Assert.Equal(new[] { 3, 2, 1 }, actual);
        }
    }
}
