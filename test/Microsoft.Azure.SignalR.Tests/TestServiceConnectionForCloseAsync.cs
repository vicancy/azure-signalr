using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.SignalR.Protocol;
using Microsoft.Azure.SignalR.Tests.Common;

namespace Microsoft.Azure.SignalR.Tests
{
    internal class TestServiceConnectionForCloseAsync : TestServiceConnection
    {
        public TestServiceConnectionForCloseAsync() : base(ServiceConnectionStatus.Connected, false, clientInvocationManager: new DefaultClientInvocationManager())
        {
        }

        /**
         * Register an outgoing Task.
         */
        protected override Task<Task> OnClientConnectedAsync(OpenConnectionMessage openConnectionMessage, CancellationToken cancellationToken)
        {
            return Task.FromResult(Task.CompletedTask);
        }
    }
}