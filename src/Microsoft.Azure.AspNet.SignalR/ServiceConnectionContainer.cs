// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR.AspNet
{
    internal class ServiceConnectionContainer : IServiceConnection
    {
        private readonly List<ServiceConnection> _serviceConnections;

        public ServiceConnectionContainer(List<ServiceConnection> connections)
        {
            _serviceConnections = connections ?? new List<ServiceConnection>();
        }

        public Task StartAsync()
        {
            var tasks = _serviceConnections.Select(c => c.StartAsync());
            return Task.WhenAll(tasks);
        }

        public Task WriteAsync(ServiceMessage serviceMessage)
        {
            var index = StaticRandom.Next(_serviceConnections.Count);
            return _serviceConnections[index].WriteAsync(serviceMessage);
        }
    }
}
