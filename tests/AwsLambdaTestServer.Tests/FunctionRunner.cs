// Copyright (c) Martin Costello, 2019. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Net.Http;
using System.Reflection;
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
            using var bootstrap = new LambdaBootstrap(handlerWrapper, handler.InitializeAsync);

            if (httpClient != null)
            {
                // Replace the internal runtime API client with one using the specified HttpClient.
                // See https://github.com/aws/aws-lambda-dotnet/blob/4f9142b95b376bd238bce6be43f4e1ec1f983592/Libraries/src/Amazon.Lambda.RuntimeSupport/Bootstrap/LambdaBootstrap.cs#L41
                var client = new RuntimeApiClient(httpClient);

                var property = typeof(LambdaBootstrap).GetProperty("Client", BindingFlags.Instance | BindingFlags.NonPublic);
                property.SetValue(bootstrap, client);
            }

            await bootstrap.RunAsync(cancellationToken);
        }
    }
}
