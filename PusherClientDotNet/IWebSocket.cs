using System;
namespace PusherClientDotNet
{
    interface IWebSocket
    {
        void Close();
        event EventHandler<EventArgs> OnClose;
        event EventHandler<global::System.ServiceModel.WebSockets.WebSocketEventArgs> OnData;
        event EventHandler<EventArgs> OnOpen;
        void Open();
        void SendMessage(string data);
    }
}
