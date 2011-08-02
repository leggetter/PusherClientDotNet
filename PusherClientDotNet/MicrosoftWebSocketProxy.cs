using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PusherClientDotNet
{
    public class MicrosoftWebSocketProxy: System.ServiceModel.WebSockets.WebSocket, IWebSocket
    {
        public MicrosoftWebSocketProxy(string url) :
            base(url)
        {
        }
    }
}
