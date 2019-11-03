// Copyright (c) Martin Costello, 2019. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Threading.Channels;

namespace MartinCostello.Testing.AwsLambdaTestServer
{
    /// <summary>
    /// A class representing a message enqueued to be processed by an AWS Lambda function. This class cannot be inherited.
    /// </summary>
    public sealed class LambdaTestMessage
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LambdaTestMessage"/> class.
        /// </summary>
        /// <param name="awsRequestId">The AWS request Id to invoke the Lambda function with.</param>
        /// <param name="reader">The channel reader associated with the response.</param>
        internal LambdaTestMessage(string awsRequestId, ChannelReader<LambdaTestResponse> reader)
        {
            AwsRequestId = awsRequestId;
            Response = reader;
        }

        /// <summary>
        /// Gets the AWS request Id for the request to the function.
        /// </summary>
        public string AwsRequestId { get; }

        /// <summary>
        /// Gets the channel reader which completes once the request is processed by the function.
        /// </summary>
        public ChannelReader<LambdaTestResponse> Response { get; }
    }
}
