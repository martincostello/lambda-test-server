// Copyright (c) Martin Costello, 2019. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json;
using MartinCostello.Testing.AwsLambdaTestServer;

#pragma warning disable IDE0130
namespace MyFunctions;

public class ReverseFunctionWithMobileSdkTests(ITestOutputHelper outputHelper) : FunctionTests(outputHelper)
{
    [Fact]
    public async Task Function_Reverses_Numbers_With_Mobile_Sdk()
    {
        // Arrange
        using var server = new LambdaTestServer();

        await WithServerAsync(server, async static (server, cts) =>
        {
            int[] value = [1, 2, 3];
            byte[] content = JsonSerializer.SerializeToUtf8Bytes(value);

            LambdaTestContext context = await server.EnqueueAsync(content);

            using var httpClient = server.CreateClient();

            // Act
            await ReverseFunction.RunAsync(httpClient, cts.Token);

            // Assert
            Assert.True(context.Response.TryRead(out LambdaTestResponse? response));
            Assert.NotNull(response);
            Assert.True(response!.IsSuccessful);

            var actual = JsonSerializer.Deserialize<int[]>(response.Content);

            Assert.NotNull(actual);
            int[] expected = [3, 2, 1];
            Assert.Equal(expected, actual);
        });
    }
}
