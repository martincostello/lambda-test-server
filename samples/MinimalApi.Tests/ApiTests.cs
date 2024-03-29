// Copyright (c) Martin Costello, 2019. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;
using Amazon.Lambda.APIGatewayEvents;
using MartinCostello.Testing.AwsLambdaTestServer;
using Microsoft.AspNetCore.Http;

namespace MinimalApi;

public sealed class ApiTests : IAsyncLifetime, IDisposable
{
    private readonly HttpLambdaTestServer _server;

    public ApiTests(ITestOutputHelper outputHelper)
    {
        _server = new() { OutputHelper = outputHelper };
    }

    public void Dispose()
        => _server.Dispose();

    public async Task DisposeAsync()
        => await _server.DisposeAsync();

    public async Task InitializeAsync()
        => await _server.InitializeAsync();

    [Fact(Timeout = 5_000)]
    public async Task Can_Hash_String()
    {
        // Arrange
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        var body = new
        {
            algorithm = "sha256",
            format = "base64",
            plaintext = "ASP.NET Core",
        };

        var request = new APIGatewayProxyRequest()
        {
            Body = JsonSerializer.Serialize(body),
            Headers = new Dictionary<string, string>()
            {
                ["content-type"] = "application/json",
            },
            HttpMethod = HttpMethods.Post,
            Path = "/hash",
        };

        // Arrange
        string json = JsonSerializer.Serialize(request, options);

        LambdaTestContext context = await _server.EnqueueAsync(json);

        using var cts = GetCancellationTokenSourceForResponseAvailable(context);

        // Act
        _ = Task.Run(
            () =>
            {
                try
                {
                    typeof(HashRequest).Assembly.EntryPoint!.Invoke(null, [Array.Empty<string>()]);
                }
                catch (Exception ex) when (LambdaServerWasShutDown(ex))
                {
                    // The Lambda runtime server was shut down
                }
            },
            cts.Token);

        // Assert
        await context.Response.WaitToReadAsync(cts.IsCancellationRequested ? default : cts.Token);

        context.Response.TryRead(out LambdaTestResponse? response).ShouldBeTrue();
        response.IsSuccessful.ShouldBeTrue($"Failed to process request: {await response.ReadAsStringAsync()}");
        response.Duration.ShouldBeInRange(TimeSpan.Zero, TimeSpan.FromSeconds(2));
        response.Content.ShouldNotBeEmpty();

        // Assert
        var actual = JsonSerializer.Deserialize<APIGatewayProxyResponse>(response.Content, options);

        actual.ShouldNotBeNull();

        actual.ShouldNotBeNull();
        actual.StatusCode.ShouldBe(StatusCodes.Status200OK);
        actual.MultiValueHeaders.ShouldContainKey("Content-Type");
        actual.MultiValueHeaders["Content-Type"].ShouldBe(["application/json; charset=utf-8"]);

        var hash = JsonSerializer.Deserialize<HashResponse>(actual.Body, options);

        hash.ShouldNotBeNull();
        hash.Hash.ShouldBe("XXE/IcKhlw/yjLTH7cCWPSr7JfOw5LuYXeBuE5skNfA=");
    }

    private static CancellationTokenSource GetCancellationTokenSourceForResponseAvailable(
        LambdaTestContext context,
        TimeSpan? timeout = null)
    {
        if (timeout == null)
        {
            timeout = System.Diagnostics.Debugger.IsAttached ? Timeout.InfiniteTimeSpan : TimeSpan.FromSeconds(3);
        }

        var cts = new CancellationTokenSource(timeout.Value);

        // Queue a task to stop the test server from listening as soon as the response is available
        _ = Task.Run(
            async () =>
            {
                await context.Response.WaitToReadAsync(cts.Token);

                if (!cts.IsCancellationRequested)
                {
                    await cts.CancelAsync();
                }
            },
            cts.Token);

        return cts;
    }

    private static bool LambdaServerWasShutDown(Exception exception)
    {
        if (exception is not TargetInvocationException targetException ||
            targetException.InnerException is not HttpRequestException httpException ||
            httpException.InnerException is not SocketException socketException)
        {
            return false;
        }

        return socketException.SocketErrorCode == SocketError.ConnectionRefused;
    }
}
