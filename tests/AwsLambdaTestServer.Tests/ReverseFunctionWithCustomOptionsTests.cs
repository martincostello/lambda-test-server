// Copyright (c) Martin Costello, 2019. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json;
using MartinCostello.Testing.AwsLambdaTestServer;

#pragma warning disable IDE0130
namespace MyFunctions;

public static class ReverseFunctionWithCustomOptionsTests
{
    [RetryFact]
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
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        await server.StartAsync(cancellationTokenSource.Token);

        int[] value = [1, 2, 3];
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(value);

        LambdaTestContext context = await server.EnqueueAsync(json);

        using var httpClient = server.CreateClient();

        // Act
        await ReverseFunction.RunAsync(httpClient, cancellationTokenSource.Token);

        // Assert
        Assert.True(context.Response.TryRead(out LambdaTestResponse? response));
        Assert.True(response!.IsSuccessful);

        var actual = JsonSerializer.Deserialize<int[]>(response.Content);

        Assert.NotNull(actual);
        int[] expected = [3, 2, 1];
        Assert.Equal(expected, actual);
    }
}
