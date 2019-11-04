// Copyright (c) Martin Costello, 2019. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Mime;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace MartinCostello.Testing.AwsLambdaTestServer
{
    /// <summary>
    /// A class representing a handler for AWS Lambda runtime HTTP requests. This class cannot be inherited.
    /// </summary>
    internal sealed class RuntimeHandler : IDisposable
    {
        /// <summary>
        /// The cancellation token that is signalled when request listening should stop. This field is read-only.
        /// </summary>
        private readonly CancellationToken _cancellationToken;

        /// <summary>
        /// The test server's options. This field is read-only.
        /// </summary>
        private readonly LambdaTestServerOptions _options;

        /// <summary>
        /// The channel of function requests to process. This field is read-only.
        /// </summary>
        private readonly Channel<LambdaTestRequest> _requests;

        /// <summary>
        /// A dictionary containing channels for the responses for enqueued requests. This field is read-only.
        /// </summary>
        private readonly ConcurrentDictionary<string, ResponseContext> _responses;

        /// <summary>
        /// Whether the instance has been disposed.
        /// </summary>
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="RuntimeHandler"/> class.
        /// </summary>
        /// <param name="options">The test server's options.</param>
        /// <param name="cancellationToken">The cancellation token that is signalled when request listening should stop.</param>
        internal RuntimeHandler(LambdaTestServerOptions options, CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;
            _options = options;

            // Support multi-threaded access to the request queue, although the default
            // usage scenario would be a single reader and writer from a test method.
            var channelOptions = new UnboundedChannelOptions()
            {
                SingleReader = false,
                SingleWriter = false,
            };

            _requests = Channel.CreateUnbounded<LambdaTestRequest>(channelOptions);
            _responses = new ConcurrentDictionary<string, ResponseContext>(StringComparer.Ordinal);
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="RuntimeHandler"/> class.
        /// </summary>
        ~RuntimeHandler()
        {
            Dispose(false);
        }

        /// <summary>
        /// Gets or sets the logger to use.
        /// </summary>
        internal ILogger Logger { get; set; }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Enqueues a request for the Lambda function to process as an asynchronous operation.
        /// </summary>
        /// <param name="request">The request to invoke the function with.</param>
        /// <param name="cancellationToken">The cancellation token to use when enqueuing the item.</param>
        /// <returns>
        /// A <see cref="Task"/> representing the asynchronous operation to enqueue the request
        /// which returns a channel reader which completes once the request is processed by the function.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// A request with the Id specified by <paramref name="request"/> is currently in-flight.
        /// </exception>
        internal async Task<ChannelReader<LambdaTestResponse>> EnqueueAsync(
            LambdaTestRequest request,
            CancellationToken cancellationToken)
        {
            // There is only one response per request, so the channel is bounded to one item
            var channel = Channel.CreateBounded<LambdaTestResponse>(1);
            var context = new ResponseContext(channel);

            if (!_responses.TryAdd(request.AwsRequestId, context))
            {
                throw new InvalidOperationException($"A request with AWS request Id '{request.AwsRequestId}' is currently in-flight.");
            }

            // Enqueue the request for the Lambda runtime to process
            await _requests.Writer.WriteAsync(request, cancellationToken);

            // Return the reader to the caller to await the function being handled
            return channel.Reader;
        }

        /// <summary>
        /// Handles a request for the next invocation for the Lambda function.
        /// </summary>
        /// <param name="httpContext">The HTTP context.</param>
        /// <returns>
        /// A <see cref="Task"/> representing the asynchronous operation to get the next invocation request.
        /// </returns>
        internal async Task HandleNextAsync(HttpContext httpContext)
        {
            Logger.LogInformation(
                "Waiting for new request for Lambda function with ARN {FunctionArn}.",
                _options.FunctionArn);

            LambdaTestRequest request;

            try
            {
                // Additionally cancel the listen loop if the processing is stopped
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(httpContext.RequestAborted, _cancellationToken);

                // Wait until there is a request to process
                await _requests.Reader.WaitToReadAsync(cts.Token);
                request = await _requests.Reader.ReadAsync();
            }
            catch (Exception ex) when (ex is OperationCanceledException || ex is ChannelClosedException)
            {
                Logger.LogInformation(
                    ex,
                    "Stopped listening for additional requests for Lambda function with ARN {FunctionArn}.",
                    _options.FunctionArn);

                // Send a dummy response to prevent the listen loop from erroring
                request = new LambdaTestRequest(new[] { (byte)'{', (byte)'}' }, "xx-lambda-test-server-stopped-xx");

                // This dummy request wasn't enqueued, so it needs manually adding
                _responses.GetOrAdd(request.AwsRequestId, (_) => new ResponseContext(Channel.CreateBounded<LambdaTestResponse>(1)));
            }

            // Write the response for the Lambda runtime to pass to the function to invoke
            string traceId = Guid.NewGuid().ToString();

            Logger.LogInformation(
                "Invoking Lambda function with ARN {FunctionArn} for request Id {AwsRequestId} and trace Id {AwsTraceId}.",
                _options.FunctionArn,
                request.AwsRequestId,
                traceId);

            // These headers are required, as otherwise an exception is thrown
            httpContext.Response.Headers.Add("Lambda-Runtime-Aws-Request-Id", request.AwsRequestId);
            httpContext.Response.Headers.Add("Lambda-Runtime-Invoked-Function-Arn", _options.FunctionArn);

            // These headers are optional
            httpContext.Response.Headers.Add("Lambda-Runtime-Trace-Id", traceId);

            if (request.ClientContext != null)
            {
                httpContext.Response.Headers.Add("Lambda-Runtime-Client-Context", request.ClientContext);
            }

            if (request.CognitoIdentity != null)
            {
                httpContext.Response.Headers.Add("Lambda-Runtime-Cognito-Identity", request.CognitoIdentity);
            }

            var deadline = DateTimeOffset.UtcNow.Add(_options.FunctionTimeout).ToUnixTimeMilliseconds();

            // Record the current time for the response to have the duration measured
            _responses[request.AwsRequestId].DurationTimer = Stopwatch.StartNew();

            httpContext.Response.Headers.Add("Lambda-Runtime-Deadline-Ms", deadline.ToString("F0", CultureInfo.InvariantCulture));

            httpContext.Response.ContentType = MediaTypeNames.Application.Json;
            httpContext.Response.StatusCode = StatusCodes.Status200OK;

            await httpContext.Response.BodyWriter.WriteAsync(request.Content, httpContext.RequestAborted);
        }

        /// <summary>
        /// Handles an successful response for an invocation of the Lambda function.
        /// </summary>
        /// <param name="httpContext">The HTTP context.</param>
        /// <returns>
        /// A <see cref="Task"/> representing the asynchronous operation to handle the response.
        /// </returns>
        internal async Task HandleResponseAsync(HttpContext httpContext)
        {
            string awsRequestId = httpContext.Request.RouteValues["AwsRequestId"] as string;

            byte[] content = await ReadContentAsync(httpContext, httpContext.RequestAborted).ConfigureAwait(false);

            Logger.LogInformation(
                "Invoked Lambda function with ARN {FunctionArn} for request Id {AwsRequestId}: {ResponseContent}.",
                _options.FunctionArn,
                awsRequestId,
                ToString(content));

            await CompleteRequestChannelAsync(
                awsRequestId,
                content,
                isSuccessful: true,
                httpContext.RequestAborted).ConfigureAwait(false);

            httpContext.Response.StatusCode = StatusCodes.Status204NoContent;
        }

        /// <summary>
        /// Handles an error response for an invocation of the Lambda function.
        /// </summary>
        /// <param name="httpContext">The HTTP context.</param>
        /// <returns>
        /// A <see cref="Task"/> representing the asynchronous operation to handle the response.
        /// </returns>
        internal async Task HandleInvocationErrorAsync(HttpContext httpContext)
        {
            string awsRequestId = httpContext.Request.RouteValues["AwsRequestId"] as string;

            byte[] content = await ReadContentAsync(httpContext, httpContext.RequestAborted).ConfigureAwait(false);

            Logger.LogError(
                "Error invoking Lambda function with ARN {FunctionArn} for request Id {AwsRequestId}: {ErrorContent}",
                _options.FunctionArn,
                awsRequestId,
                ToString(content));

            await CompleteRequestChannelAsync(
                awsRequestId,
                content,
                isSuccessful: false,
                httpContext.RequestAborted).ConfigureAwait(false);

            httpContext.Response.StatusCode = StatusCodes.Status204NoContent;
        }

        /// <summary>
        /// Handles an error response for the failed initialization of the Lambda function.
        /// </summary>
        /// <param name="httpContext">The HTTP context.</param>
        /// <returns>
        /// A <see cref="Task"/> representing the asynchronous operation to handle the response.
        /// </returns>
        internal async Task HandleInitializationErrorAsync(HttpContext httpContext)
        {
            byte[] content = await ReadContentAsync(httpContext, httpContext.RequestAborted).ConfigureAwait(false);

            Logger.LogError(
                "Error initializing Lambda function with ARN {FunctionArn}: {ErrorContent}",
                _options.FunctionArn,
                ToString(content));

            httpContext.Response.StatusCode = StatusCodes.Status204NoContent;
        }

        /// <summary>
        /// Reads the HTTP request body as an asynchronous operation.
        /// </summary>
        /// <param name="httpContext">The HTTP request to read the body from.</param>
        /// <param name="cancellationToken">The cancellation token to use.</param>
        /// <returns>
        /// A <see cref="Task"/> representing the asynchronous operation to read the
        /// request body that returns a byte array containing the request content.
        /// </returns>
        private static async Task<byte[]> ReadContentAsync(HttpContext httpContext, CancellationToken cancellationToken)
        {
            using var stream = new MemoryStream();

            await httpContext.Request.BodyReader.CopyToAsync(stream, cancellationToken).ConfigureAwait(false);

            return stream.ToArray();
        }

        /// <summary>
        /// Converts the specified byte array to a string.
        /// </summary>
        /// <param name="content">The array to convert to a string.</param>
        /// <returns>
        /// The UTF-8 representation of <paramref name="content"/>.
        /// </returns>
        private static string ToString(byte[] content)
        {
            return Encoding.UTF8.GetString(content);
        }

        /// <summary>
        /// Completes the request channel for the specified request.
        /// </summary>
        /// <param name="awsRequestId">The AWS request Id to complete the response for.</param>
        /// <param name="content">The raw content associated with the request's response.</param>
        /// <param name="isSuccessful">Whether the response indicates the request was successfully handled.</param>
        /// <param name="cancellationToken">The cancellation token to use.</param>
        /// <returns>
        /// A <see cref="Task"/> representing the asynchronous operation.
        /// </returns>
        private async Task CompleteRequestChannelAsync(
            string awsRequestId,
            byte[] content,
            bool isSuccessful,
            CancellationToken cancellationToken)
        {
            if (!_responses.TryRemove(awsRequestId, out var context))
            {
                Logger.LogError(
                    "Could not find response channel with AWS request Id {AwsRequestId} for Lambda function with ARN {FunctionArn}.",
                    awsRequestId,
                    _options.FunctionArn);

                return;
            }

            context.DurationTimer.Stop();

            // Make the response available to read by the enqueuer
            var response = new LambdaTestResponse(content, isSuccessful, context.DurationTimer.Elapsed);
            await context.Channel.Writer.WriteAsync(response, cancellationToken);

            // Mark the channel as complete as there will be no more responses written
            context.Channel.Writer.Complete();
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Complete the channels so that any callers who do not know
                    // the server has been disposed do not wait indefinitely for a
                    // channel to complete that will now never be written to again.
                    _requests.Writer.TryComplete();

                    var contexts = _responses.Values;
                    _responses.Clear();

                    foreach (var context in contexts)
                    {
                        context.Channel.Writer.TryComplete();
                    }
                }

                _disposed = true;
            }
        }

        private sealed class ResponseContext
        {
            internal ResponseContext(Channel<LambdaTestResponse> channel)
            {
                Channel = channel;
            }

            internal Channel<LambdaTestResponse> Channel { get; }

            internal Stopwatch DurationTimer { get; set; }
        }
    }
}
