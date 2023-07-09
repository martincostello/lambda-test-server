// Copyright (c) Martin Costello, 2019. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Amazon;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;

namespace MartinCostello.Testing.AwsLambdaTestServer;

public static class AwsIntegrationTests
{
    [SkippableFact]
    public static async Task Runtime_Generates_Valid_Aws_Trace_Id()
    {
        // Arrange
        Skip.If(GetAwsCredentials() is null, "No AWS credentials are configured.");

        using var server = new LambdaTestServer();
        using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await server.StartAsync(cancellationTokenSource.Token);

        var request = new QueueExistsRequest()
        {
            QueueName = Guid.NewGuid().ToString(),
        };

        var json = System.Text.Json.JsonSerializer.Serialize(request);
        var context = await server.EnqueueAsync(json);

        _ = Task.Run(async () =>
        {
            await context.Response.WaitToReadAsync(cancellationTokenSource.Token);

            if (!cancellationTokenSource.IsCancellationRequested)
            {
                cancellationTokenSource.Cancel();
            }
        });

        using var httpClient = server.CreateClient();

        // Act
        await RunAsync(httpClient, cancellationTokenSource.Token);

        // Assert
        context.Response.TryRead(out var response).ShouldBeTrue();
        response.ShouldNotBeNull();
        response.IsSuccessful.ShouldBeTrue();
    }

    private static async Task RunAsync(HttpClient? httpClient, CancellationToken cancellationToken)
    {
        var serializer = new Amazon.Lambda.Serialization.Json.JsonSerializer();

        using var handlerWrapper = HandlerWrapper.GetHandlerWrapper<QueueExistsRequest>(SqsFunction.QueueExistsAsync, serializer);
        using var bootstrap = new LambdaBootstrap(httpClient, handlerWrapper, SqsFunction.InitializeAsync);

        await bootstrap.RunAsync(cancellationToken);
    }

    private static AWSCredentials? GetAwsCredentials()
    {
        try
        {
            return new EnvironmentVariablesAWSCredentials();
        }
        catch (InvalidOperationException)
        {
            // Not configured
        }

        try
        {
            return AssumeRoleWithWebIdentityCredentials.FromEnvironmentVariables();
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static class SqsFunction
    {
        public static Task<bool> InitializeAsync() => Task.FromResult(true);

        public static async Task<bool> QueueExistsAsync(QueueExistsRequest request, ILambdaContext context)
        {
            context.Logger.LogLine($"Handling AWS request Id {context.AwsRequestId} to check if SQS queue ${request.QueueName} exists.");

            var credentials = GetAwsCredentials();
            var region = RegionEndpoint.EUWest2;

            bool exists;

            try
            {
                using var client = new AmazonSQSClient(credentials, region);
                _ = await client.GetQueueUrlAsync(request.QueueName);

                exists = true;
            }
            catch (QueueDoesNotExistException)
            {
                exists = false;
            }

            context.Logger.LogLine($"SQS queue ${request.QueueName} {(exists ? "exists" : "does not exist")}.");

            return exists;
        }
    }

    private sealed class QueueExistsRequest
    {
        public string QueueName { get; set; } = string.Empty;
    }
}
