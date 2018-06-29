// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.AspNet.SignalR
{
    internal class ServiceConnectionManager : IServiceConnectionManager
    {
        private Dictionary<string, IServiceConnection> _serviceConnections = new Dictionary<string, IServiceConnection>();
        private HashSet<string> _hubNamesWithDot = new HashSet<string>();
        public void AddConnection(string hubName, IServiceConnection connection)
        {
            _serviceConnections[hubName] = connection;
            if (hubName.Contains('.'))
            {
                _hubNamesWithDot.Add(hubName);
            }
        }

        public Task StartAsync()
        {
            var tasks = _serviceConnections.Values.Select(s => s.StartAsync());
            return Task.WhenAll(tasks);
        }

        public Task WriteAsync(ServiceMessage serviceMessage)
        {
            var tasks = _serviceConnections.Values.Select(s => s.WriteAsync(serviceMessage));
            return Task.WhenAll(tasks);
        }

        public IServiceConnection WithHub(string hubName)
        {
            if (!_serviceConnections.TryGetValue(hubName, out var connection))
            {
                throw new KeyNotFoundException($"Service connection with Hub {hubName} does not exist");
            }

            return connection;
        }

        public IReadOnlyCollection<string> HubNamesWithDot => _hubNamesWithDot;
    }

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
