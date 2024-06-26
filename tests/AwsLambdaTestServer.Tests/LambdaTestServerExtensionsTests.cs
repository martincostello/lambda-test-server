﻿// Copyright (c) Martin Costello, 2019. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MartinCostello.Testing.AwsLambdaTestServer;

public static class LambdaTestServerExtensionsTests
{
    [Fact]
    public static async Task EnqueueAsync_Validates_Parameters()
    {
        // Arrange
        using var server = new LambdaTestServer();
        using LambdaTestServer nullServer = null!;
        byte[] content = null!;
        string value = null!;

        byte[] emptyBytes = [];

        // Act
        await Assert.ThrowsAsync<ArgumentNullException>("content", () => server.EnqueueAsync(content));
        await Assert.ThrowsAsync<ArgumentNullException>("value", () => server.EnqueueAsync(value));
        await Assert.ThrowsAsync<ArgumentNullException>("server", () => nullServer.EnqueueAsync(emptyBytes));
        await Assert.ThrowsAsync<ArgumentNullException>("server", () => nullServer.EnqueueAsync(string.Empty));
    }
}
