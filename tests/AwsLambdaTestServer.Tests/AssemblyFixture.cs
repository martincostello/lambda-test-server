// Copyright (c) Martin Costello, 2019. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using MartinCostello.Testing.AwsLambdaTestServer;

[assembly: AssemblyFixture(typeof(AssemblyFixture))]

namespace MartinCostello.Testing.AwsLambdaTestServer;

public sealed class AssemblyFixture
{
    // Read the default memory limits before any of the tests execute any code that may change it.
    // The cast to ulong is required for the setting to be respected by the runtime.
    // See https://github.com/aws/aws-lambda-dotnet/pull/1595#issuecomment-1771747410.
    private static readonly ulong DefaultMemory = (ulong)GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;

    public AssemblyFixture()
    {
        ResetMemoryLimits();
    }

    public static void ResetMemoryLimits()
    {
        Debug.Assert(DefaultMemory != 134217728, "The default value of TotalAvailableMemoryBytes should not be 128MB.");

        // Undo any changes that Amazon.Lambda.RuntimeSupport makes internally
        AppContext.SetData("GCHeapHardLimit", DefaultMemory);
        GC.RefreshMemoryLimit();
    }
}
