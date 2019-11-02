// Copyright (c) Martin Costello, 2019. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System;
using Shouldly;
using Xunit;

namespace MartinCostello.Testing.AwsLambdaTestServer
{
    public static class LambdaTestRequestTests
    {
        [Fact]
        public static void Constructor_Throws_If_Content_Null()
        {
            // Arrange
            byte[] content = null;

            // Act and Assert
            Assert.Throws<ArgumentNullException>("content", () => new LambdaTestRequest(content));
        }

        [Fact]
        public static void Constructor_Sets_Properties()
        {
            // Arrange
            byte[] content = new[] { (byte)1 };

            // Act
            var actual = new LambdaTestRequest(content);

            // Assert
            actual.Content.ShouldBeSameAs(content);
            actual.AwsRequestId.ShouldNotBeNullOrEmpty();
            Guid.TryParse(actual.AwsRequestId, out var requestId).ShouldBeTrue();
            requestId.ShouldNotBe(Guid.Empty);

            // Arrange
            string awsRequestId = "my-request-id";

            // Act
            actual = new LambdaTestRequest(content, awsRequestId);

            // Assert
            actual.Content.ShouldBeSameAs(content);
            actual.AwsRequestId.ShouldBe(awsRequestId);
        }
    }
}
