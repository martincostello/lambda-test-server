﻿// Copyright (c) Martin Costello, 2019. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Text;
using MinimalApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAWSLambdaHosting(LambdaEventSource.RestApi);

var app = builder.Build();

app.MapGet("/", () => "Hello World!");

app.MapPost("/hash", async (HttpRequest httpRequest) =>
{
    var request = await httpRequest.ReadFromJsonAsync<HashRequest>();

    if (string.IsNullOrWhiteSpace(request?.Algorithm))
    {
        return Results.Problem(
            "No hash algorithm name specified.",
            statusCode: StatusCodes.Status400BadRequest);
    }

    if (string.IsNullOrWhiteSpace(request.Format))
    {
        return Results.Problem(
            "No hash output format specified.",
            statusCode: StatusCodes.Status400BadRequest);
    }

    bool? formatAsBase64 = request.Format.ToUpperInvariant() switch
    {
        "BASE64" => true,
        "HEXADECIMAL" => false,
        _ => null,
    };

    if (formatAsBase64 is null)
    {
        return Results.Problem(
            $"The specified hash format '{request.Format}' is invalid.",
            statusCode: StatusCodes.Status400BadRequest);
    }

    const int MaxPlaintextLength = 4096;

    if (request.Plaintext?.Length > MaxPlaintextLength)
    {
        return Results.Problem(
            $"The plaintext to hash cannot be more than {MaxPlaintextLength} characters in length.",
            statusCode: StatusCodes.Status400BadRequest);
    }

    byte[] buffer = Encoding.UTF8.GetBytes(request.Plaintext ?? string.Empty);
    HashAlgorithmName? hashAlgorithm = request.Algorithm.ToUpperInvariant() switch
    {
        "MD5" => HashAlgorithmName.MD5,
        "SHA1" => HashAlgorithmName.SHA1,
        "SHA256" => HashAlgorithmName.SHA256,
        "SHA384" => HashAlgorithmName.SHA384,
        "SHA512" => HashAlgorithmName.SHA512,
        _ => null,
    };

    if (hashAlgorithm is not { } algorithm)
    {
        return Results.Problem(
            $"The specified hash algorithm '{request.Algorithm}' is not supported.",
            statusCode: StatusCodes.Status400BadRequest);
    }

    byte[] hash = CryptographicOperations.HashData(algorithm, buffer);

    var result = new HashResponse()
    {
        Hash = formatAsBase64 == true ? Convert.ToBase64String(hash) : Convert.ToHexString(hash),
    };

    return Results.Json(result);
});

app.Run();
