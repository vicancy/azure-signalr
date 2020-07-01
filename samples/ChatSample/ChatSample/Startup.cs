using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ChatSample.CoreApp3
{
    public class Startup
    {
        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc();
            services.AddSingleton(typeof(IEndpointRouter), typeof(CustomRouter));
            services.AddSignalR()
                .AddAzureSignalR(s=>s.Endpoints = new Microsoft.Azure.SignalR.ServiceEndpoint[] { 
                new Microsoft.Azure.SignalR.ServiceEndpoint("Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789ABCDEFGH;Port=8080;Version=1.0;", name: "8080"),
                new Microsoft.Azure.SignalR.ServiceEndpoint("Endpoint=http://localhost;AccessKey=ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789ABCDEFGH;Port=8081;Version=1.0;", name: "8081"),
                })
                .AddMessagePackProtocol();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            app.UseFileServer();
            app.UseRouting();
            app.UseAuthorization();

            app.UseEndpoints(routes =>
            {
                routes.MapHub<Chat>("/chat");
                routes.MapHub<BenchHub>("/signalrbench");
            });
        }
        private class CustomRouter : EndpointRouterDecorator
        {
            public override ServiceEndpoint GetNegotiateEndpoint(HttpContext context, IEnumerable<ServiceEndpoint> endpoints)
            {
                // Override the negotiate behavior to get the endpoint from query string
                var endpointName = context.Request.Query["endpoint"];
                if (endpointName.Count == 0)
                {
                    return base.GetNegotiateEndpoint(context, endpoints);
                }

                return endpoints.FirstOrDefault(s => s.Name == endpointName) // Get the endpoint with name matching the incoming request
                       ?? base.GetNegotiateEndpoint(context, endpoints); // Or fallback to the default behavior to randomly select one from primary endpoints, or fallback to secondary when no primary ones are online
            }
        }
    }
}
