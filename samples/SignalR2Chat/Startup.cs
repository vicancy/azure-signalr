using System;
using System.Threading.Tasks;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Infrastructure;
using Microsoft.AspNet.SignalR.Messaging;
using Microsoft.AspNet.SignalR.Transports;
using Microsoft.Owin;
using Owin;

[assembly: OwinStartup(typeof(SignalR2Chat.Startup))]

namespace SignalR2Chat
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=316888
            app.MapSignalR();

            var configuration = new HubConfiguration();
            var serviceConnection = new ServiceConnection("http://localhost:5001/v2/server");
            configuration.Resolver.Register(typeof(ServiceConnection), () => serviceConnection);
            configuration.Resolver.Register(typeof(IProtectedData), () => new EmptyProtectedData());
            configuration.Resolver.Register(typeof(IMessageBus), () => new ServiceMessageBus(configuration.Resolver, serviceConnection));

            configuration.Resolver.Register(typeof(ITransportManager), () => new AzureTransportManager());
            serviceConnection.StartAsync(configuration).GetAwaiter();
        }
    }
}
