using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;

namespace SignalR2Chat2
{
    [HubName("Chat.A")]
    public class Chat : Hub
    {
        public void Hello(string name, string message)
        {
            int count = 10;
            int i = 0;
            Enumerable.Repeat<Func<int>>(
                    () =>
                    {
                        Clients.All.hello(name, $"round {i}: {message}");
                        i++;
                        return i;
                    }
                , count).Select(s => s()).ToList();
        }
    }
}