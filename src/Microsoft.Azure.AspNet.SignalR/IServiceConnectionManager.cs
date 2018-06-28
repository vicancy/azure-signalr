// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


namespace Microsoft.Azure.AspNet.SignalR
{
    internal interface IServiceConnectionManager : IServiceConnection
    {
        void AddConnection(string hubName, IServiceConnection connection);

        IServiceConnection WithHub(string hubName);
    }
}