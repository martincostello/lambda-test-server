﻿// Copyright (c) Martin Costello, 2019. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using MartinCostello.Testing.AwsLambdaTestServer;
using Newtonsoft.Json;
using Xunit;

namespace MyFunctions
{
    public static class ReverseFunctionWithCustomOptionsTests
    {
        [Fact]
        public static async Task Function_Reverses_Numbers_With_Custom_Options()
        {
            // Arrange
            var options = new LambdaTestServerOptions()
            {
                FunctionMemorySize = 256,
                FunctionTimeout = TimeSpan.FromSeconds(30),
                FunctionVersion = 42,
            };

            using var server = new LambdaTestServer(options);
            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(1));

            await server.StartAsync(cancellationTokenSource.Token);

            int[] value = new[] { 1, 2, 3 };
            string json = JsonConvert.SerializeObject(value);

            LambdaTestContext context = await server.EnqueueAsync(json);

            using var httpClient = server.CreateClient();

            // Act
            await ReverseFunction.RunAsync(httpClient, cancellationTokenSource.Token);

            // Assert
            Assert.True(context.Response.TryRead(out LambdaTestResponse response));
            Assert.NotNull(response);
            Assert.True(response.IsSuccessful);

            json = await response.ReadAsStringAsync();
            int[] actual = JsonConvert.DeserializeObject<int[]>(json);

            Assert.Equal(new[] { 3, 2, 1 }, actual);
        }
    }
}
