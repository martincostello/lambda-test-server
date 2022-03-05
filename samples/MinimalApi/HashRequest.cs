// Copyright (c) Martin Costello, 2019. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

namespace MinimalApi;

public class HashRequest
{
    public string Algorithm { get; set; } = string.Empty;

    public string Format { get; set; } = string.Empty;

    public string Plaintext { get; set; } = string.Empty;
}
