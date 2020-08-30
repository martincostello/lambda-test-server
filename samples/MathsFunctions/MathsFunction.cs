// Copyright (c) Martin Costello, 2019. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.Json;

namespace MathsFunctions
{
    public static class MathsFunction
    {
        public static async Task Main()
            => await RunAsync();

        public static async Task RunAsync(HttpClient? httpClient = null, CancellationToken cancellationToken = default)
        {
            var serializer = new JsonSerializer();

            using var handlerWrapper = HandlerWrapper.GetHandlerWrapper<MathsRequest, MathsResponse>(Evaluate, serializer);
            using var bootstrap = new LambdaBootstrap(httpClient ?? new HttpClient(), handlerWrapper);

            await bootstrap.RunAsync(cancellationToken);
        }

        private static MathsResponse Evaluate(MathsRequest request)
        {
            double result = request.Operator switch
            {
                "+" => request.Left + request.Right,
                "-" => request.Left - request.Right,
                "*" => request.Left * request.Right,
                "/" => request.Left / request.Right,
                "%" => request.Left % request.Right,
                "^" => Math.Pow(request.Left, request.Right),
                _ => throw new NotSupportedException($"The '{request.Operator}' operator is not supported."),
            };

            return new MathsResponse() { Result = result };
        }
    }
}
