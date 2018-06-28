using Microsoft.AspNet.SignalR.Hosting;
using Microsoft.AspNet.SignalR.Transports;
using System;
using System.Threading.Tasks;

namespace Microsoft.Azure.AspNet.SignalR
{
    internal class AzureTransport : ITransport
    {
        private readonly TaskCompletionSource<object> _lifetimeTcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ServiceConnection _serviceConnection;

        public AzureTransport(HostContext context)
        {
            _serviceConnection = (ServiceConnection)context.Environment[ContextConstants.AzureServiceConnectionKey];
            context.Environment[ContextConstants.AzureSignalRTransportKey] = this;
        }

        public Func<string, Task> Received { get; set; }

        public Func<Task> Connected { get; set; }

        public Func<Task> Reconnected { get; set; }

        public Func<bool, Task> Disconnected { get; set; }

        public string ConnectionId { get; set; }

        public Task<string> GetGroupsToken()
        {
            return Task.FromResult<string>(null);
        }

        public async Task ProcessRequest(ITransportConnection connection)
        {
            var connected = Connected;
            if (connected != null)
            {
                await connected();
            }

            await _lifetimeTcs.Task;

            var disconnected = Disconnected;
            if (disconnected != null)
            {
                await disconnected(true);
            }
        }

        public Task Send(object value)
        {
            return _serviceConnection.WriteAsync(ConnectionId, value);
        }

        public void OnReceived(string value)
        {
            var received = Received;
            if (received != null)
            {
                _ = received(value);
            }
        }

        public void OnDisconnected() => _lifetimeTcs.TrySetResult(null);
    }
}