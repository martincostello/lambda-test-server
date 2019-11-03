// Copyright (c) Martin Costello, 2019. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MartinCostello.Testing.AwsLambdaTestServer;
using Newtonsoft.Json;
using Xunit;

namespace MyFunctions
{
    public static class ReverseFunctionTests
    {
        [Fact]
        public static async Task Function_Reverses_Numbers()
        {
            // Arrange
            using var server = new LambdaTestServer();
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
            Assert.NotNull(response.Content);

            json = Encoding.UTF8.GetString(response.Content);
            int[] actual = JsonConvert.DeserializeObject<int[]>(json);

            Assert.Equal(new[] { 3, 2, 1 }, actual);
        }
    }
}
