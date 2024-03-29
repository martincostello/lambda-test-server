// Copyright (c) Martin Costello, 2019. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Testing.AwsLambdaTestServer;

#pragma warning disable CA1819

/// <summary>
/// A class representing a test response from an AWS Lambda function. This class cannot be inherited.
/// </summary>
public sealed class LambdaTestResponse
{
    /// <summary>
    /// Initializes a new instance of the <see cref="LambdaTestResponse"/> class.
    /// </summary>
    /// <param name="content">The raw content of the response from the Lambda function.</param>
    /// <param name="isSuccessful">Whether the response indicates the request was successfully handled.</param>
    /// <param name="duration">The duration of the Lambda function invocation.</param>
    internal LambdaTestResponse(byte[] content, bool isSuccessful, TimeSpan duration)
    {
        Content = content;
        Duration = duration;
        IsSuccessful = isSuccessful;
    }

    /// <summary>
    /// Gets the raw byte content of the response from the function.
    /// </summary>
    public byte[] Content { get; }

    /// <summary>
    /// Gets the approximate duration of the Lambda function invocation.
    /// </summary>
    public TimeSpan Duration { get; }

    /// <summary>
    /// Gets a value indicating whether the response indicates the request was successfully handled.
    /// </summary>
    public bool IsSuccessful { get; }
}
