// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Connections;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.AspNet.SignalR
{
    internal interface IServiceConnection
    {
        Task StartAsync();

        Task WriteAsync(ServiceMessage serviceMessage);
    }
}