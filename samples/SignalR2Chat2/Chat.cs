using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.AspNet.SignalR;

namespace SignalR2Chat2
{
    public class Chat : Hub
    {
        public void Hello(string name, string message)
        {
            Clients.All.hello(name, message);
        }
    }
}