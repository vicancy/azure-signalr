using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNet.SignalR;
using Microsoft.AspNet.SignalR.Hubs;

namespace SignalR2Chat2
{
    [HubName("Chat.A")]
    [Authorize]
    public class Chat : Hub
    {
        [Authorize(Roles = "Admin")]
        public void Hello(string message)
        {
            string name;
            var user = Context.User;
            if (user.Identity.IsAuthenticated)
            {
                name = user.Identity.Name;
            }
            else
            {
                var role = ((ClaimsIdentity)user.Identity).Claims
                .Where(c => c.Type == ClaimTypes.Role)
                .Select(c => c.Value);

                throw new UnauthorizedAccessException($"User is in role {role}");
            }

            Clients.Caller.hello("Successfully logged in", name);

            for(int i = 0; i < 10; i++)
            {
                Clients.All.hello(name, $"round {i}: {message}");
            }
        }
    }
}