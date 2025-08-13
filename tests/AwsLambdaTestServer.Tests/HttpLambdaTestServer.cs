// Copyright (c) Martin Costello, 2019. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using MartinCostello.Logging.XUnit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MartinCostello.Testing.AwsLambdaTestServer;

internal sealed class HttpLambdaTestServer : LambdaTestServer, IAsyncLifetime, ITestOutputHelperAccessor
{
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;
    private IWebHost? _webHost;

    public HttpLambdaTestServer()
        : base()
    {
    }

    public HttpLambdaTestServer(Action<IServiceCollection> configure)
        : base(configure)
    {
    }

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
    {
        Options.Configure = (services) =>
            services.AddLogging((builder) => builder.AddXUnit(this));

        await StartAsync(_cts.Token);
    }

    protected override IServer CreateServer(IWebHostBuilder builder)
    {
        _webHost = builder
            .UseKestrel((p) => p.Listen(System.Net.IPAddress.Loopback, 0))
            .ConfigureServices((services) => services.AddLogging((builder) => builder.AddXUnit(this)))
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
