// Copyright (c) Martin Costello, 2019. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Threading.Tasks;
using Newtonsoft.Json;

namespace MartinCostello.Testing.AwsLambdaTestServer
{
    internal static class TestExtensions
    {
        internal static async Task<LambdaTestContext> EnqueueAsync<T>(this LambdaTestServer server, T value)
            where T : class
        {
            string json = JsonConvert.SerializeObject(value);
            return await server.EnqueueAsync(json);
        }

        internal static T ReadAs<T>(this LambdaTestResponse response)
            where T : class
        {
            string json = System.Text.Encoding.UTF8.GetString(response.Content);
            return JsonConvert.DeserializeObject<T>(json);
        }
    }
}
