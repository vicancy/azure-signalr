// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Protocol;
namespace Microsoft.Azure.SignalR.AspNet
{
    internal class ServiceConnectionManager : IServiceConnectionManager
    {
        private const char DotChar = '.';
        private readonly ConcurrentDictionary<string, IServiceConnection> _serviceConnections = new ConcurrentDictionary<string, IServiceConnection>();
        private readonly HashSet<string> _hubNameWithDots = new HashSet<string>();

        public IServiceConnection AppConnection { get; set; }

        public void AddConnection(string hubName, IServiceConnection connection)
        {
            _serviceConnections.TryAdd(hubName, connection);
            // It is possible that the hub contains dot character, while the fully qualified name is formed as {HubName}.{Name} (Name can be connectionId or userId or groupId)
            // So keep a copy of the hub names containing dots and return all the possible combinations when the fully qualified name is provided
            if (hubName.IndexOf(DotChar) > -1)
            {
                lock (_hubNameWithDots)
                {
                    _hubNameWithDots.Add(hubName);
                }
            }
        }

        public string HubName => string.Empty;
        public IServiceConnection WithHub(string hubName)
        {
            if (!_serviceConnections.TryGetValue(hubName, out var connection))
            {
                throw new KeyNotFoundException($"Service connection with Hub {hubName} does not exist");
            }
            return connection;
        }

        /// <summary>
        /// The fully qualified name is as {HubName}.{Name}
        /// </summary>
        /// <param name="nameWithHubPrefix"></param>
        /// <returns>The connection and the name without hub prefix</returns>
        public IEnumerable<(IServiceConnection, string)> GetPossibleConnections(string nameWithHubPrefix)
        {
            var index = nameWithHubPrefix.IndexOf(DotChar);
            if (index == -1)
            {
                throw new InvalidDataException($"Name {nameWithHubPrefix} does not contain the required separator {DotChar}");
            }
            // It is rare that hubname contains '.'
            foreach (var hub in _hubNameWithDots)
            {
                if (nameWithHubPrefix.Length > hub.Length + 1
                    && nameWithHubPrefix[hub.Length] == DotChar
                    && hub == nameWithHubPrefix.Substring(0, hub.Length))
                {
                    yield return (_serviceConnections[hub], nameWithHubPrefix.Substring(hub.Length + 1));
                }
            }
            var hubName = nameWithHubPrefix.Substring(0, index);
            var name = nameWithHubPrefix.Substring(index + 1);
            yield return (WithHub(hubName), name);
        }

        public Task StartAsync()
        {
            return Task.WhenAll(GetConnections().Select(s => s.StartAsync()));
        }

        public Task WriteAsync(ServiceMessage serviceMessage)
        {
            return AppConnection?.WriteAsync(serviceMessage);
        }

        private IEnumerable<IServiceConnection> GetConnections()
        {
            var appConnection = AppConnection;
            if (appConnection != null)
            {
                yield return appConnection;
            }

            foreach (var conn in _serviceConnections)
            {
                yield return conn.Value;
            }
        }
    }
}
