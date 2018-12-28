// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.Azure.SignalR
{
    internal interface IEndpointManager
    {
        IReadOnlyList<ServiceEndpoint> GetAvailableEndpoints();

        IReadOnlyList<ServiceEndpoint> GetPrimaryEndpoints();

        bool TryUpdateEndpoints(IReadOnlyList<ServiceEndpoint> endpoints);

        bool TryOfflineEndpoints(IReadOnlyList<ServiceEndpoint> endpoints);
    }
}
