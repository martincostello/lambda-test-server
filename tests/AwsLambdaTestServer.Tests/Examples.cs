// Copyright (c) Martin Costello, 2019. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json;

namespace MartinCostello.Testing.AwsLambdaTestServer;

/// <summary>
/// Examples for using <c>MartinCostello.Testing.AwsLambdaTestServer</c>.
/// </summary>
public static class Examples
{
    [Fact]
    public static async Task Function_Can_Process_Request()
    {
        // Create a cancellation token that stops the server listening for new requests.
        // Auto-cancel the server after 5 seconds in case something goes wrong and the request is not handled.
        using var shutdownAfter = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(shutdownAfter.Token);

        // Arrange - Create a test server for the Lambda runtime to use
        using var server = new LambdaTestServer()
        {
            // Signal the token to shut down the server when the invocation is completed
            OnInvocationCompleted = async (_, _) => await shutdownAfter.CancelAsync(),
        };

        // Start the test server so it is ready to listen for requests from the Lambda runtime
        await server.StartAsync(linkedToken.Token);

        // Create a test request for the Lambda function being tested
        var value = new MyRequest()
        {
            Values = [1, 2, 3], // The function returns the sum of the specified numbers
        };

        string requestJson = JsonSerializer.Serialize(value);

        // Queue the request with the server to invoke the Lambda function and
        // store the ChannelReader into a variable to use to read the response.
        LambdaTestContext context = await server.EnqueueAsync(requestJson);

        // Create an HttpClient for the Lambda to use with LambdaBootstrap
        using var httpClient = server.CreateClient();

        // Act - Start the Lambda runtime and run until the cancellation token is signalled
        await MyFunctionEntrypoint.RunAsync(httpClient, linkedToken.Token);

        // Assert - The channel reader should have the response available
        context.Response.TryRead(out LambdaTestResponse? response).ShouldBeTrue("No Lambda response is available.");

        response!.IsSuccessful.ShouldBeTrue("The Lambda function failed to handle the request.");
        response.Content.ShouldNotBeEmpty("The Lambda function did not return any content.");

        string responseJson = await response.ReadAsStringAsync();
        var actual = JsonSerializer.Deserialize<MyResponse>(responseJson);

        actual.ShouldNotBeNull();
        actual.Sum.ShouldBe(6, "The Lambda function returned an incorrect response.");
    }
}
