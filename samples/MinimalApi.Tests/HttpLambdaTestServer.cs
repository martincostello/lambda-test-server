// Copyright (c) Martin Costello, 2019. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using MartinCostello.Logging.XUnit;
using MartinCostello.Testing.AwsLambdaTestServer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MinimalApi;

internal sealed class HttpLambdaTestServer : LambdaTestServer, IAsyncLifetime, ITestOutputHelperAccessor
{
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;
    private IWebHost? _webHost;

    public ITestOutputHelper? OutputHelper { get; set; }

    public async ValueTask DisposeAsync()
    {
        if (_webHost is not null)
        {
            await _webHost.StopAsync();
        }

        Dispose();
    }

    public async ValueTask InitializeAsync()
        => await StartAsync(_cts.Token);

    protected override IServer CreateServer(WebHostBuilder builder)
    {
        _webHost = builder
            .UseKestrel()
            .ConfigureServices((services) => services.AddLogging((builder) => builder.AddXUnit(this)))
            .UseUrls("http://127.0.0.1:0")
            .Build();

        _webHost.Start();

        return _webHost.Services.GetRequiredService<IServer>();
    }

    protected override void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _webHost?.Dispose();

                _cts.Cancel();
                _cts.Dispose();
            }

            _disposed = true;
        }

        base.Dispose(disposing);
    }
}
