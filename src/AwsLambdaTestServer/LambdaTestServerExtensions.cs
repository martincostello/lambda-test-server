// Copyright (c) Martin Costello, 2019. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Text;
using System.Threading.Channels;

namespace MartinCostello.Testing.AwsLambdaTestServer;

/// <summary>
/// A class containing extension methods for the <see cref="LambdaTestServer"/> class. This class cannot be inherited.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class LambdaTestServerExtensions
{
    /// <summary>
    /// Enqueues a request for the Lambda function to process as an asynchronous operation.
    /// </summary>
    /// <param name="server">The server to enqueue the request with.</param>
    /// <param name="value">The request content to process.</param>
    /// <returns>
    /// A <see cref="Task{LambdaTestContext}"/> representing the asynchronous operation to
    /// enqueue the request which returns a context containg a <see cref="ChannelReader{LambdaTestResponse}"/>
    /// which completes once the request is processed by the function.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="server"/> or <paramref name="value"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The instance has been disposed.
    /// </exception>
    public static async Task<LambdaTestContext> EnqueueAsync(
        this LambdaTestServer server,
        string value)
    {
        if (server == null)
        {
            throw new ArgumentNullException(nameof(server));
        }

        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        byte[] content = Encoding.UTF8.GetBytes(value);

        return await server.EnqueueAsync(content).ConfigureAwait(false);
    }

    /// <summary>
    /// Enqueues a request for the Lambda function to process as an asynchronous operation.
    /// </summary>
    /// <param name="server">The server to enqueue the request with.</param>
    /// <param name="content">The request content to process.</param>
    /// <returns>
    /// A <see cref="Task{LambdaTestContext}"/> representing the asynchronous operation to
    /// enqueue the request which returns a context containg a <see cref="ChannelReader{LambdaTestResponse}"/>
    /// which completes once the request is processed by the function.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="server"/> or <paramref name="content"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The instance has been disposed.
    /// </exception>
    public static async Task<LambdaTestContext> EnqueueAsync(
        this LambdaTestServer server,
        byte[] content)
    {
        if (server == null)
        {
            throw new ArgumentNullException(nameof(server));
        }

        var request = new LambdaTestRequest(content);

        return await server.EnqueueAsync(request).ConfigureAwait(false);
    }
}
