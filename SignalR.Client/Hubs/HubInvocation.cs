using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace SignalR.Client.Hubs
{
    public class HubInvocation
    {
        public string Hub { get; set; }
        public string Method { get; set; }
        public JToken[] Args { get; set; }
        public Dictionary<string, JToken> State { get; set; }
    }
}
