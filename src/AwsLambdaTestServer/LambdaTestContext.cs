// Copyright (c) Martin Costello, 2019. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Threading.Channels;

namespace MartinCostello.Testing.AwsLambdaTestServer
{
    /// <summary>
    /// A class representing the context for an AWS request enqueued to be processed by an AWS Lambda function. This class cannot be inherited.
    /// </summary>
    public sealed class LambdaTestContext
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LambdaTestContext"/> class.
        /// </summary>
        /// <param name="request">The request to invoke the Lambda function with.</param>
        /// <param name="reader">The channel reader associated with the response.</param>
        internal LambdaTestContext(LambdaTestRequest request, ChannelReader<LambdaTestResponse> reader)
        {
            Request = request;
            Response = reader;
        }

        /// <summary>
        /// Gets the request to invoke the Lambda function with.
        /// </summary>
        public LambdaTestRequest Request { get; }

        /// <summary>
        /// Gets the channel reader which completes once the request is processed by the function.
        /// </summary>
        public ChannelReader<LambdaTestResponse> Response { get; }
    }
}
