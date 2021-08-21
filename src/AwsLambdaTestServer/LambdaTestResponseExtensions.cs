// Copyright (c) Martin Costello, 2019. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.ComponentModel;

namespace MartinCostello.Testing.AwsLambdaTestServer
{
    /// <summary>
    /// A class containing extension methods for the <see cref="LambdaTestResponse"/> class. This class cannot be inherited.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class LambdaTestResponseExtensions
    {
        /// <summary>
        /// Reads the content of the specified response as a string as an asynchronous operation.
        /// </summary>
        /// <param name="response">The response to read as a string.</param>
        /// <returns>
        /// A <see cref="Task{TResult}"/> representing the asynchronous operation to
        /// read the content of the specified response as a <see langword="string"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="response"/> is <see langword="null"/>.
        /// </exception>
        public static async Task<string> ReadAsStringAsync(this LambdaTestResponse response)
        {
            if (response == null)
            {
                throw new ArgumentNullException(nameof(response));
            }

            using var stream = new MemoryStream(response.Content);
            using var reader = new StreamReader(stream);

            return await reader.ReadToEndAsync().ConfigureAwait(false);
        }
    }
}
