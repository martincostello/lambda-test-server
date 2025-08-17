// Copyright (c) Martin Costello, 2019. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using MartinCostello.Logging.XUnit;
using MartinCostello.Testing.AwsLambdaTestServer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MinimalApi;

internal sealed class HttpLambdaTestServer : LambdaTestServer, IAsyncLifetime, ITestOutputHelperAccessor
{
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;

    public ITestOutputHelper? OutputHelper { get; set; }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    public async ValueTask InitializeAsync()
        => await StartAsync(_cts.Token);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.UseKestrel((p) => p.Listen(System.Net.IPAddress.Loopback, 0))
               .ConfigureServices((services) => services.AddLogging((builder) => builder.AddXUnit(this)));
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _cts.Cancel();
                _cts.Dispose();
            }

            _disposed = true;
        }

        base.Dispose(disposing);
    }
}
