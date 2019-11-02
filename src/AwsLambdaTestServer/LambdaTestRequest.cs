// Copyright (c) Martin Costello, 2019. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System;

namespace MartinCostello.Testing.AwsLambdaTestServer
{
    /// <summary>
    /// A class representing a test request to an AWS Lambda function.
    /// </summary>
    public class LambdaTestRequest
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LambdaTestRequest"/> class.
        /// </summary>
        /// <param name="content">The raw content of the request to invoke the Lambda function with.</param>
        /// <param name="awsRequestId">The optional AWS request Id to invoke the Lambda function with.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="content"/> is <see langword="null"/>.
        /// </exception>
        public LambdaTestRequest(byte[] content, string awsRequestId = null)
        {
            Content = content ?? throw new ArgumentNullException(nameof(content));
            AwsRequestId = awsRequestId ?? Guid.NewGuid().ToString();
        }

        /// <summary>
        /// Gets the AWS request Id for the request to the function.
        /// </summary>
        public string AwsRequestId { get; }

        /// <summary>
        /// Gets the raw byte content of the request to the function.
        /// </summary>
#pragma warning disable CA1819
        public byte[] Content { get; }
#pragma warning restore CA1819

        /// <summary>
        /// Gets or sets an optional string containing the serialized JSON
        /// for the client context when invoked through the AWS Mobile SDK.
        /// </summary>
        public string ClientContext { get; set; }

        /// <summary>
        /// Gets or sets an optional string containing the serialized JSON for the
        /// Amazon Cognito identity provider when invoked through the AWS Mobile SDK.
        /// </summary>
        public string CognitoIdentity { get; set; }
    }
}
