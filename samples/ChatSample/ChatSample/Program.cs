using System;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;

namespace ChatSample.CoreApp3
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var task = CreateHostBuilder(args).Build().StartAsync();
            var message = Console.ReadLine();
            while (!string.IsNullOrEmpty(message))
            {
                if (Startup.HubContext != null)
                {
                    await Startup.HubContext.Clients.All.SendAsync("broadcastMessage", "a", message);
                }

                message = Console.ReadLine();
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
