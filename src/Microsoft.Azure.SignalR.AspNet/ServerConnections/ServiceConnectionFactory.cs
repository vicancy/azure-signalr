using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.SignalR.AspNet
{
    internal class ServiceConnectionFactory : IServiceConnectionFactory
    {
        // Use a dummy options to add a direct assembly reference to Microsoft.AspNetCore.Http.Connections.Client to prevent netstandard20 assembly binding issues when used in netframework projects 
        private static readonly HttpConnectionOptions _dummy;
        private readonly IServiceProtocol _serviceProtocol;
        private readonly IClientConnectionManager _clientConnectionManager;
        private readonly ILoggerFactory _logger;

        public ServiceConnectionFactory(IServiceProtocol serviceProtocol,
            IClientConnectionManager clientConnectionManager,
            ILoggerFactory logger)
        {
            _serviceProtocol = serviceProtocol;
            _clientConnectionManager = clientConnectionManager;
            _logger = logger;
        }

        public IServiceConnection Create(IConnectionFactory connectionFactory, IServiceMessageHandler serviceMessageHandler, ServerConnectionType type)
        {
            return new ServiceConnection(Guid.NewGuid().ToString(), _serviceProtocol, connectionFactory, _clientConnectionManager, _logger, serviceMessageHandler, type);
        }
    }
}
