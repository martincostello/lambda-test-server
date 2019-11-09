// Copyright (c) Martin Costello, 2019. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using MartinCostello.Testing.AwsLambdaTestServer;
using Newtonsoft.Json;
using Xunit;

namespace MathsFunctions
{
    public static class MathsFunctionTests
    {
        [Theory]
        [InlineData(1, "+", 1, 2)]
        [InlineData(9, "-", 6, 3)]
        [InlineData(2, "*", 2, 4)]
        [InlineData(9, "/", 3, 3)]
        [InlineData(7, "%", 2, 1)]
        public static async Task Function_Computes_Results(double left, string op, double right, double expected)
        {
            // Arrange
            using var server = new LambdaTestServer();
            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(0.2));

            await server.StartAsync(cancellationTokenSource.Token);

            var value = new MathsRequest() { Left = left, Operator = op, Right = right };
            string json = JsonConvert.SerializeObject(value);

            var context = await server.EnqueueAsync(json);

            using var httpClient = server.CreateClient();

            // Act
            await MathsFunction.RunAsync(httpClient, cancellationTokenSource.Token);

            // Assert
            Assert.True(context.Response.TryRead(out var response));
            Assert.True(response.IsSuccessful);

            json = await response.ReadAsStringAsync();
            var actual = JsonConvert.DeserializeObject<MathsResponse>(json);

            Assert.Equal(expected, actual.Result);
        }
    }
}
