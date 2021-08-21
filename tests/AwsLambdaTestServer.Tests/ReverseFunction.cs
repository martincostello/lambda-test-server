// Copyright (c) Martin Costello, 2019. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.Json;

namespace MyFunctions
{
    public static class ReverseFunction
    {
        public static async Task RunAsync(
            HttpClient? httpClient = null,
            CancellationToken cancellationToken = default)
        {
            var serializer = new JsonSerializer();

#pragma warning disable CA2000
            using var handlerWrapper = HandlerWrapper.GetHandlerWrapper<int[], int[]>(ReverseAsync, serializer);
            using var bootstrap = new LambdaBootstrap(httpClient ?? new HttpClient(), handlerWrapper);
#pragma warning restore CA2000

            await bootstrap.RunAsync(cancellationToken);
        }

        public static Task<int[]> ReverseAsync(int[] values)
        {
            return Task.FromResult(values.Reverse().ToArray());
        }
    }
}
