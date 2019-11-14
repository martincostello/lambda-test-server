// Copyright (c) Martin Costello, 2019. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.Json;

namespace MyFunctions
{
    public static class ReverseFunction
    {
        public static async Task Main()
            => await RunAsync();

        public static async Task RunAsync(
            HttpClient? httpClient = null,
            CancellationToken cancellationToken = default)
        {
            var serializer = new JsonSerializer();

            using var handlerWrapper = HandlerWrapper.GetHandlerWrapper<int[], int[]>(ReverseAsync, serializer);
            using var bootstrap = new LambdaBootstrap(httpClient ?? new HttpClient(), handlerWrapper);

            await bootstrap.RunAsync(cancellationToken);
        }

        public static Task<int[]> ReverseAsync(int[] values)
        {
            return Task.FromResult(values.Reverse().ToArray());
        }
    }
}
