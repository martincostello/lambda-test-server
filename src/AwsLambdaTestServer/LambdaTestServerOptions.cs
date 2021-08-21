// Copyright (c) Martin Costello, 2019. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.Extensions.DependencyInjection;

namespace MartinCostello.Testing.AwsLambdaTestServer
{
    /// <summary>
    /// A class representing options for the <see cref="LambdaTestServer"/> class. This class cannot be inherited.
    /// </summary>
    public sealed class LambdaTestServerOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LambdaTestServerOptions"/> class.
        /// </summary>
        public LambdaTestServerOptions()
        {
            FunctionName = "test-function";
            FunctionArn = $"arn:aws:lambda:eu-west-1:123456789012:function:{FunctionName}";
            LogGroupName = "test-function-log-group";
            LogStreamName = "test-function-log-stream";

            FunctionHandler = string.Empty;
            FunctionMemorySize = 128;
            FunctionTimeout = TimeSpan.FromSeconds(3);
            FunctionVersion = 1;
        }

        /// <summary>
        /// Gets or sets an optional delegate to invoke when configuring the test Lambda runtime server.
        /// </summary>
        public Action<IServiceCollection>? Configure { get; set; }

        /// <summary>
        /// Gets or sets the ARN of the Lambda function being tested.
        /// </summary>
        public string FunctionArn { get; set; }

        /// <summary>
        /// Gets or sets the optional handler for the Lambda function being tested.
        /// </summary>
        public string FunctionHandler { get; set; }

        /// <summary>
        /// Gets or sets the amount of memory available to the function in megabytes during execution. The default value is 128.
        /// </summary>
        /// <remarks>
        /// This limit is not enforced and is only used for reporting into the Lambda context.
        /// </remarks>
        public int FunctionMemorySize { get; set; }

        /// <summary>
        /// Gets or sets the name of the Lambda function being tested.
        /// </summary>
        public string FunctionName { get; set; }

        /// <summary>
        /// Gets or sets the function's timeout. The default value is 3 seconds.
        /// </summary>
        /// <remarks>
        /// This limit is not enforced and is only used for reporting into the Lambda context.
        /// </remarks>
        public TimeSpan FunctionTimeout { get; set; }

        /// <summary>
        /// Gets or sets the version of the Lambda function being tested.
        /// </summary>
        public int FunctionVersion { get; set; }

        /// <summary>
        /// Gets or sets the name of the log group for the Lambda function being tested.
        /// </summary>
        public string LogGroupName { get; set; }

        /// <summary>
        /// Gets or sets the name of the log stream for the Lambda function being tested.
        /// </summary>
        public string LogStreamName { get; set; }
    }
}
