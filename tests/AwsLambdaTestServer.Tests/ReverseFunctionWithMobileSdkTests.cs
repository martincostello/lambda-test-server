// Copyright (c) Martin Costello, 2019. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MartinCostello.Testing.AwsLambdaTestServer;
using Xunit;

namespace MyFunctions
{
    public static class ReverseFunctionWithMobileSdkTests
    {
        [Fact]
        public static async Task Function_Reverses_Numbers_With_Mobile_Sdk()
        {
            // Arrange
            using var server = new LambdaTestServer();
            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(1));

            await server.StartAsync(cancellationTokenSource.Token);

            int[] value = new[] { 1, 2, 3 };
            string json = JsonSerializer.Serialize(value);
            byte[] content = Encoding.UTF8.GetBytes(json);

            var request = new LambdaTestRequest(content)
            {
                ClientContext = @"{ ""client"": { ""app_title"": ""my-app"" } }",
                CognitoIdentity = @"{ ""identityId"": ""my-identity"" }",
            };

            LambdaTestContext context = await server.EnqueueAsync(json);

            using var httpClient = server.CreateClient();

            // Act
            await ReverseFunction.RunAsync(httpClient, cancellationTokenSource.Token);

            // Assert
            Assert.True(context.Response.TryRead(out LambdaTestResponse response));
            Assert.True(response.IsSuccessful);

            json = await response.ReadAsStringAsync();
            int[] actual = JsonSerializer.Deserialize<int[]>(json);

            Assert.Equal(new[] { 3, 2, 1 }, actual);
        }
    }
}
