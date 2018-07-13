using Microsoft.AspNet.SignalR;
using Microsoft.Owin;
using Microsoft.Owin.Cors;
using Owin;
using System.Configuration;
using System.Diagnostics;
using System.Web.Http;

[assembly: OwinStartup(typeof(SignalR2Chat.Startup))]

namespace SignalR2Chat
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            // Turn tracing on programmatically
            GlobalHost.TraceManager.Switch.Level = SourceLevels.Information;

            ConfigureAuth(app);
            app
                .Map("/api", map =>
                {
                    HttpConfiguration config = new HttpConfiguration();
                    config.MapHttpAttributeRoutes();
                    map.UseWebApi(config);
                })
                .Map("/signalr", map =>
            {
                // Turns cors support on allowing everything
                // In real applications, the origins should be locked down
                var config = new HubConfiguration
                {
                    EnableDetailedErrors = true
                };
                map.UseCors(CorsOptions.AllowAll)
                   .RunAzureSignalR(config);
            });
        }
    }
}
