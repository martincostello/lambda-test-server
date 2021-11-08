// Copyright (c) Martin Costello, 2019. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace MartinCostello.Testing.AwsLambdaTestServer;

internal static class MyFunctionEntrypoint
{
    internal static async Task RunAsync(
        HttpClient? httpClient = null,
        CancellationToken cancellationToken = default)
    {
        await FunctionRunner.RunAsync<MyHandler>(httpClient, cancellationToken);
    }
}
