// Copyright (c) Martin Costello, 2019. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Threading.Tasks;

namespace MartinCostello.Testing.AwsLambdaTestServer
{
    internal static class TestExtensions
    {
        internal static async Task<LambdaTestContext> EnqueueAsync<T>(this LambdaTestServer server, T value)
            where T : class
        {
            string json = JsonSerializer.Serialize(value);
            return await server.EnqueueAsync(json);
        }

        internal static async Task<T> ReadAsAsync<T>(this LambdaTestResponse response)
            where T : class
        {
            string json = await response.ReadAsStringAsync();
            return JsonSerializer.Deserialize<T>(json);
        }
    }
}
