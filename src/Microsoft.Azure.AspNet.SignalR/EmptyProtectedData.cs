using Microsoft.AspNet.SignalR.Infrastructure;

namespace Microsoft.Azure.AspNet.SignalR
{
    internal class EmptyProtectedData : IProtectedData
    {
        public string Protect(string data, string purpose)
        {
            return data;
        }

        public string Unprotect(string protectedValue, string purpose)
        {
            return protectedValue;
        }
    }
}