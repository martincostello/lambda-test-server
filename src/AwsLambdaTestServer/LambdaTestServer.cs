// Copyright (c) Martin Costello, 2019. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Threading.Channels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MartinCostello.Testing.AwsLambdaTestServer;

/// <summary>
/// A class representing a test AWS Lambda runtime HTTP server for an AWS Lambda function.
/// </summary>
public class LambdaTestServer : IDisposable
{
    private readonly CancellationTokenSource _onDisposed;

    private bool _disposed;
    private RuntimeHandler? _handler;
    private bool _isStarted;
    private LambdaEnvironmentVariables? _environment;
    private IHost? _host;
    private CancellationTokenSource? _onStopped;

    /// <summary>
    /// Initializes a new instance of the <see cref="LambdaTestServer"/> class.
    /// </summary>
    public LambdaTestServer()
        : this(new LambdaTestServerOptions())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LambdaTestServer"/> class.
    /// </summary>
    /// <param name="configure">An optional delegate to invoke when configuring the test Lambda runtime server.</param>
    public LambdaTestServer(Action<IServiceCollection> configure)
        : this(new LambdaTestServerOptions() { Configure = configure })
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LambdaTestServer"/> class.
    /// </summary>
    /// <param name="options">The options to use to configure the test Lambda runtime server.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    public LambdaTestServer(LambdaTestServerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Options = options;
        _onDisposed = new CancellationTokenSource();
    }

    /// <summary>
    /// Finalizes an instance of the <see cref="LambdaTestServer"/> class.
    /// </summary>
    ~LambdaTestServer() => Dispose(false);

    /// <summary>
    /// Gets a value indicating whether the test Lambda runtime server has been started.
    /// </summary>
    public bool IsStarted => _isStarted;

    /// <summary>
    /// Gets the options in use by the test Lambda runtime server.
    /// </summary>
    public LambdaTestServerOptions Options { get; }

    /// <summary>
    /// Gets or sets an optional delegate to invoke when a request to the Lambda function has completed
    /// processing that receives the AWS request ID and whether the invocation was successful to the invocation.
    /// </summary>
    public Func<string, bool, ValueTask>? OnInvocationCompleted { get; set; }

    /// <summary>
    /// Clears any AWS Lambda environment variables set by instances of <see cref="LambdaTestServer"/>.
    /// </summary>
    public static void ClearLambdaEnvironmentVariables()
    {
        Environment.SetEnvironmentVariable("AWS_LAMBDA_DOTNET_DISABLE_MEMORY_LIMIT_CHECK", null);
        Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_MEMORY_SIZE", null);
        Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME", null);
        Environment.SetEnvironmentVariable("AWS_LAMBDA_FUNCTION_VERSION", null);
        Environment.SetEnvironmentVariable("AWS_LAMBDA_LOG_GROUP_NAME", null);
        Environment.SetEnvironmentVariable("AWS_LAMBDA_LOG_STREAM_NAME", null);
        Environment.SetEnvironmentVariable("AWS_LAMBDA_RUNTIME_API", null);
        Environment.SetEnvironmentVariable("_HANDLER", null);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Creates an <see cref="HttpClient"/> to use to interact with the test Lambda runtime server.
    /// </summary>
    /// <returns>
    /// An <see cref="HttpClient"/> that can be used to process Lambda runtime HTTP requests.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// The test server has not been started.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The instance has been disposed.
    /// </exception>
    public virtual HttpClient CreateClient()
    {
        ThrowIfDisposed();
        ThrowIfNotStarted();

        if (_host.Services.GetService<IServer>() is TestServer testServer)
        {
            return testServer.CreateClient();
        }

        var baseAddress = GetServerAddress(_host);

        return new() { BaseAddress = baseAddress };
    }

    /// <summary>
    /// Enqueues a request for the Lambda function to process as an asynchronous operation.
    /// </summary>
    /// <param name="request">The request to invoke the function with.</param>
    /// <returns>
    /// A <see cref="Task{LambdaTestContext}"/> representing the asynchronous operation to
    /// enqueue the request which returns a context containing a <see cref="ChannelReader{LambdaTestResponse}"/>
    /// which completes once the request is processed by the function.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="request"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// A request with the Id specified by <paramref name="request"/> is currently in-flight or the test server has not been started.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The instance has been disposed.
    /// </exception>
    public async Task<LambdaTestContext> EnqueueAsync(LambdaTestRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        ThrowIfDisposed();
        ThrowIfNotStarted();

        var reader = await _handler!.EnqueueAsync(request, _onStopped!.Token).ConfigureAwait(false);

        return new LambdaTestContext(request, reader);
    }

    /// <summary>
    /// Starts the test Lambda runtime server as an asynchronous operation.
    /// </summary>
    /// <param name="cancellationToken">
    /// The optional cancellation token to use to signal the server should stop listening to invocation requests.
    /// </param>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation to start the test Lambda runtime server.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// The test server has already been started.
    /// </exception>
    /// <exception cref="ObjectDisposedException">
    /// The instance has been disposed.
    /// </exception>
    public virtual async Task StartAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_host != null)
        {
            throw new InvalidOperationException("The test server has already been started.");
        }

        _onStopped = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _onDisposed.Token);

        _handler = new RuntimeHandler(Options, _onStopped.Token)
        {
            OnInvocationHandled = async (requestId, successful) =>
            {
                if (OnInvocationCompleted is { } callback)
                {
                    await callback(requestId, successful).ConfigureAwait(false);
                }
            },
        };

        var builder = WebApplication.CreateSlimBuilder();

        ConfigureWebHost(builder.WebHost);
        ConfigureServices(builder.Services);

        // Ensure the server will have environment variable configuration available
        builder.Configuration.AddEnvironmentVariables();

        var app = builder.Build();

        try
        {
            Configure(app);

            await app.StartAsync(cancellationToken).ConfigureAwait(false);

            // Now the server address is known, we can set the Lambda environment variables
            _environment = new LambdaEnvironmentVariables(GetServerAddress(app), Options);

            // Reload the configuration to ensure it picks up the new environment variables
            if (app.Services.GetRequiredService<IConfiguration>() is IConfigurationRoot configuration)
            {
                configuration.Reload();
            }

            _host = app;
            _isStarted = true;
        }
        catch (Exception)
        {
            await app.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    /// <param name="disposing">
    /// <see langword="true" /> to release both managed and unmanaged resources;
    /// <see langword="false" /> to release only unmanaged resources.
    /// </param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                if (_onDisposed != null)
                {
                    // The token for _onStopped is linked to this token, so this will cancel both
                    if (!_onDisposed.IsCancellationRequested)
                    {
                        _onDisposed.Cancel();
                    }

                    _onDisposed.Dispose();
                    _onStopped?.Dispose();
                }

                _host?.Dispose();
                _handler?.Dispose();
                _environment?.Dispose();
            }

            _disposed = true;
        }
    }

    /// <summary>
    /// Configures the application for the test Lambda runtime server.
    /// </summary>
    /// <param name="app">The <see cref="IApplicationBuilder"/> to configure.</param>
    protected virtual void Configure(IApplicationBuilder app)
    {
        app.UseRouting();
        app.UseEndpoints((endpoints) =>
        {
#pragma warning disable ASP0018
            // See https://github.com/aws/aws-lambda-dotnet/blob/4f9142b95b376bd238bce6be43f4e1ec1f983592/Libraries/src/Amazon.Lambda.RuntimeSupport/Client/InternalClientAdapted.cs#L75
            endpoints.MapGet("/{LambdaVersion}/runtime/invocation/next", _handler!.HandleNextAsync);
            endpoints.MapPost("/{LambdaVersion}/runtime/init/error", _handler.HandleInitializationErrorAsync);
            endpoints.MapPost("/{LambdaVersion}/runtime/invocation/{AwsRequestId}/error", _handler.HandleInvocationErrorAsync);
            endpoints.MapPost("/{LambdaVersion}/runtime/invocation/{AwsRequestId}/response", _handler.HandleResponseAsync);
#pragma warning restore ASP0018
        });
    }

    /// <summary>
    /// Configures the services for the test Lambda runtime server application.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to use.</param>
    protected virtual void ConfigureServices(IServiceCollection services)
    {
        services.AddRouting();

        Options.Configure?.Invoke(services);
    }

    /// <summary>
    /// Configures the web host builder for the test Lambda runtime server.
    /// </summary>
    /// <param name="builder">The <see cref="IWebHostBuilder"/> to configure.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="builder"/> is <see langword="null"/>.
    /// </exception>
    protected virtual void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseTestServer();

        builder.UseContentRoot(Environment.CurrentDirectory);
        builder.UseShutdownTimeout(TimeSpan.Zero);
    }

    private static Uri GetServerAddress(IHost host)
    {
        var server = host.Services.GetRequiredService<IServer>();

        if (server is TestServer testServer)
        {
            return testServer.BaseAddress;
        }

        var serverAddresses = server?.Features.Get<IServerAddressesFeature>();
        var serverUrl = serverAddresses?.Addresses?.FirstOrDefault();

        return serverUrl is null
            ? throw new InvalidOperationException("No server addresses are available.")
            : new Uri(serverUrl, UriKind.Absolute);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    [System.Diagnostics.CodeAnalysis.MemberNotNull(nameof(_host))]
    private void ThrowIfNotStarted()
    {
        if (_host is null)
        {
            throw new InvalidOperationException("The test server has not been started.");
        }
    }

    private sealed class LambdaEnvironmentVariables : IDisposable
    {
        private readonly Dictionary<string, string?> _originalEnvironment = [];

        public LambdaEnvironmentVariables(Uri baseAddress, LambdaTestServerOptions options)
        {
            var provider = CultureInfo.InvariantCulture;

            // See https://github.com/aws/aws-lambda-dotnet/blob/4f9142b95b376bd238bce6be43f4e1ec1f983592/Libraries/src/Amazon.Lambda.RuntimeSupport/Context/LambdaEnvironment.cs#L46-L52
            ReplaceVariable("AWS_LAMBDA_FUNCTION_MEMORY_SIZE", options.FunctionMemorySize.ToString(provider));
            ReplaceVariable("AWS_LAMBDA_FUNCTION_NAME", options.FunctionName);
            ReplaceVariable("AWS_LAMBDA_FUNCTION_VERSION", options.FunctionVersion.ToString(provider));
            ReplaceVariable("AWS_LAMBDA_LOG_GROUP_NAME", options.LogGroupName);
            ReplaceVariable("AWS_LAMBDA_LOG_STREAM_NAME", options.LogStreamName);
            ReplaceVariable("AWS_LAMBDA_RUNTIME_API", $"{baseAddress.Host}:{baseAddress.Port}");
            ReplaceVariable("_HANDLER", options.FunctionHandler);

            // See https://github.com/aws/aws-lambda-dotnet/pull/1595
            if (options.DisableMemoryLimitCheck)
            {
                ReplaceVariable("AWS_LAMBDA_DOTNET_DISABLE_MEMORY_LIMIT_CHECK", bool.TrueString);
            }

            void ReplaceVariable(string name, string? value)
            {
                _originalEnvironment[name] = Environment.GetEnvironmentVariable(name);
                Environment.SetEnvironmentVariable(name, value);
            }
        }

        public void Dispose()
        {
            foreach ((var name, var originalValue) in _originalEnvironment)
            {
                Environment.SetEnvironmentVariable(name, originalValue);
            }
        }
    }
}
