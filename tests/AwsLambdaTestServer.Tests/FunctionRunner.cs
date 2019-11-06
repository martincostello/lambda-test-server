// Copyright (c) Martin Costello, 2019. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.Json;

namespace MartinCostello.Testing.AwsLambdaTestServer
{
    internal static class FunctionRunner
    {
        internal static async Task RunAsync<T>(HttpClient httpClient, CancellationToken cancellationToken)
            where T : MyHandler, new()
        {
            var handler = new T();
            var serializer = new JsonSerializer();

            using var handlerWrapper = HandlerWrapper.GetHandlerWrapper<MyRequest, MyResponse>(handler.SumAsync, serializer);
            using var bootstrap = new LambdaBootstrap(httpClient, handlerWrapper, handler.InitializeAsync);

            await bootstrap.RunAsync(cancellationToken);
        }
    }
}
