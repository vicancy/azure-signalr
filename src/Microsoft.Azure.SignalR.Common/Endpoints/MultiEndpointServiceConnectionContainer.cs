// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Common;
using Microsoft.Azure.SignalR.Protocol;

namespace Microsoft.Azure.SignalR
{
    internal class MultiEndpointServiceConnectionContainer : IServiceConnectionContainer
    {
        private readonly IEndpointManager _endpointManager;
        private readonly IRouter _router;
        public Dictionary<ServiceEndpoint, IServiceConnectionContainer> Connections { get; }

        public MultiEndpointServiceConnectionContainer(Func<ServiceEndpoint, IServiceConnectionContainer> generator, IEndpointManager endpointManager, IRouter router)
        {
            _endpointManager = endpointManager;
            _router = router;
            Connections = endpointManager.GetAvailableEndpoints().ToDictionary(s => s, s => generator(s));
        }

        public ServiceConnectionStatus Status => ServiceConnectionStatus.Connected;

        public Task StartAsync()
        {
            return Task.WhenAll(Connections.Select(s => s.Value.StartAsync()));
        }

        public Task StopAsync()
        {
            return Task.WhenAll(Connections.Select(s => s.Value.StopAsync()));
        }

        public Task WriteAsync(string partitionKey, ServiceMessage serviceMessage)
        {
            var routed = GetRoutedEndpoints(serviceMessage, _endpointManager.GetAvailableEndpoints());

            if (routed.Count == 0)
            {
                throw new AzureSignalRNotConnectedException();
            }

            return Task.WhenAll(routed.Select(s => Connections[s]).Select(s => s.WriteAsync(partitionKey, serviceMessage)));
        }

        public Task WriteAsync(ServiceMessage serviceMessage)
        {
            var routed = GetRoutedEndpoints(serviceMessage, _endpointManager.GetAvailableEndpoints());

            if (routed.Count == 0)
            {
                throw new AzureSignalRNotConnectedException();
            }

            return Task.WhenAll(routed.Select(s => Connections[s]).Select(s => s.WriteAsync(serviceMessage)));
        }

        private IReadOnlyList<ServiceEndpoint> GetRoutedEndpoints(ServiceMessage message, IReadOnlyList<ServiceEndpoint> availableEndpoints)
        {
            switch (message)
            {
                case BroadcastDataMessage bdm:
                    return _router.GetEndpointsForBroadcast(availableEndpoints);
                case GroupBroadcastDataMessage gbdm:
                    return _router.GetEndpointsForGroup(gbdm.GroupName, availableEndpoints);
                case JoinGroupMessage jgm:
                    return _router.GetEndpointsForGroup(jgm.GroupName, availableEndpoints);
                case LeaveGroupMessage lgm:
                    return _router.GetEndpointsForGroup(lgm.GroupName, availableEndpoints);
                case MultiGroupBroadcastDataMessage mgbdm:
                    return _router.GetEndpointsForGroups(mgbdm.GroupList, availableEndpoints);
                case ConnectionDataMessage cdm:
                    return _router.GetEndpointsForConnection(cdm.ConnectionId, availableEndpoints);
                case UserDataMessage udm:
                    return _router.GetEndpointsForUser(udm.UserId, availableEndpoints);
                case MultiUserDataMessage mudm:
                    return _router.GetEndpointsForUsers(mudm.UserList, availableEndpoints);
                default:
                    throw new NotSupportedException(message.GetType().Name);
            }
        }
    }
}
