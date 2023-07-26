// Copyright (c) Martin Costello, 2019. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json;
using MartinCostello.Testing.AwsLambdaTestServer;

namespace MyFunctions;

public static class ReverseFunctionWithMobileSdkTests
{
    [Fact]
    public static async Task Function_Reverses_Numbers_With_Mobile_Sdk()
    {
        // Arrange
        using var server = new LambdaTestServer();
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        await server.StartAsync(cancellationTokenSource.Token);

        int[] value = new[] { 1, 2, 3 };
        byte[] content = JsonSerializer.SerializeToUtf8Bytes(value);

        var request = new LambdaTestRequest(content)
        {
            ClientContext = JsonSerializer.Serialize(new { client = new { app_title = "my-app" } }),
            CognitoIdentity = JsonSerializer.Serialize(new { identityId = "my-identity" }),
        };

        LambdaTestContext context = await server.EnqueueAsync(content);

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
