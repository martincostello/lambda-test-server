﻿// Copyright (c) Martin Costello, 2019. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace MartinCostello.Testing.AwsLambdaTestServer
{
    internal sealed class MyRequest
    {
        public ICollection<int> Values { get; set; }
    }
}
