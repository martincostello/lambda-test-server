// Copyright (c) Martin Costello, 2019. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System;
using Shouldly;
using Xunit;

namespace MartinCostello.Testing.AwsLambdaTestServer
{
    public static class LambdaTestServerOptionsTests
    {
        [Fact]
        public static void Constructor_Initializes_Defaults()
        {
            // Act
            var actual = new LambdaTestServerOptions();

            // Assert
            actual.Configure.ShouldBeNull();
            actual.FunctionArn.ShouldNotBeNullOrEmpty();
            actual.FunctionHandler.ShouldBe(string.Empty);
            actual.FunctionMemorySize.ShouldBe(128);
            actual.FunctionTimeout.ShouldBe(TimeSpan.FromSeconds(3));
            actual.FunctionVersion.ShouldBe(1);
            actual.LogGroupName.ShouldNotBeNullOrEmpty();
            actual.LogStreamName.ShouldNotBeNullOrEmpty();
        }
    }
}
