// Copyright (c) Martin Costello, 2019. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Testing.AwsLambdaTestServer;

public static class LambdaTestResponseExtensionsTests
{
    [Fact]
    public static async Task EnqueueAsync_Validates_Parameters()
    {
        // Arrange
        LambdaTestResponse response = null!;

        // Act
        await Assert.ThrowsAsync<ArgumentNullException>("response", () => response.ReadAsStringAsync());
    }
}
