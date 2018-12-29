// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Options;

namespace Microsoft.Azure.SignalR
{
    internal class StaticEndpointManger : IEndpointManager
    {
        private readonly IReadOnlyList<ServiceEndpoint> _availableEndpoints;
        private readonly IReadOnlyList<ServiceEndpoint> _primaryEndpoints;

        public StaticEndpointManger(IOptions<ServiceOptions> options)
        {
            var endpoints = options.Value.Endpoints;
            _availableEndpoints = endpoints;
            _primaryEndpoints = endpoints;
        }

        public StaticEndpointManger(IReadOnlyList<ServiceEndpoint> endpoints)
        {
            _availableEndpoints = endpoints;
            _primaryEndpoints = endpoints;
        }

        public IReadOnlyList<ServiceEndpoint> GetAvailableEndpoints()
        {
            return _availableEndpoints;
        }

        public IReadOnlyList<ServiceEndpoint> GetPrimaryEndpoints()
        {
            return _primaryEndpoints;
        }

        public bool TryOfflineEndpoints(IReadOnlyList<ServiceEndpoint> endpoints)
        {
            throw new NotSupportedException();
        }

        public bool TryUpdateEndpoints(IReadOnlyList<ServiceEndpoint> endpoints)
        {
            throw new NotSupportedException();
        }
    }
}
