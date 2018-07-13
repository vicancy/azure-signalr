// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNet.SignalR.Tracing;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.AspNet.SignalR
{
    internal class TraceManagerLoggerFactory : ILoggerFactory
    {
        private readonly ITraceManager _traceManager;

        public TraceManagerLoggerFactory(ITraceManager traceManager)
        {
            _traceManager = traceManager;
        }

        public void AddProvider(ILoggerProvider provider)
        {
            throw new System.NotImplementedException();
        }

        public ILogger CreateLogger(string categoryName)
        {
            throw new System.NotImplementedException();
        }

        public void Dispose()
        {
            throw new System.NotImplementedException();
        }
    }
}
