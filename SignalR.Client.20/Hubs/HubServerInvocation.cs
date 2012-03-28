using System.Collections.Generic;

namespace SignalR.Client._20.Hubs
{
    // Consolidate this and HubClientInvocation (they're the same just have slightly different property names)
    public class HubServerInvocation
    {
        public string Hub { get; set; }
        public string Action { get; set; }
        public object[] Data { get; set; }
        public Dictionary<string, object> State { get; set; }
    }
}
