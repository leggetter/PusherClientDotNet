using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel.WebSockets;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace WindowsFormsApplication1
{
    class Pusher
    {
        Dictionary<string, object> options = new Dictionary<string,object>();
        string path;
        string key;
        string socket_id;
        Channels channels;
        Channel global_channel;
        bool secure;
        bool connected;
        int retry_counter;
        bool encrypted;
        WebSocket connection;

        public Pusher(string application_key, Dictionary<string, object> options)
        {
            if (options != null)
                this.options = options;
            this.path = "/app/" + application_key + "?client=js&version=" + Pusher.VERSION;
            this.key = application_key;
            this.channels = new Channels();
            this.global_channel = new Pusher.Channel("pusher_global_channel");
            this.global_channel.global = true;
            this.secure = false;
            this.connected = false;
            this.retry_counter = 0;
            if (options.ContainsKey("encrypted"))
                this.encrypted = ((bool)this.options["encrypted"]) ? true : false;
            if(Pusher.isReady) this.Connect();
            Pusher.instances.Add(this);

  ////This is the new namespaced version
  //this.bind('pusher:connection_established', function(data) {
  //  this.connected = true;
  //  this.retry_counter = 0;
  //  this.socket_id = data.socket_id;
  //  this.subscribeAll();
  //}.scopedTo(this));
  
  //this.bind('pusher:connection_disconnected', function(){
  //  for(var channel_name in this.channels.channels){
  //    this.channels.channels[channel_name].disconnect()
  //  }
  //}.scopedTo(this));

  //this.bind('pusher:error', function(data) {
  //  Pusher.log("Pusher : error : " + data.message);
  //});
        }

        static List<Pusher> instances = new List<Pusher>();
        
        public Channel Channels(string name)
        {
            return this.channels[name];
        }

        public void Connect()
        {
            string url;
            if (this.encrypted || this.secure)
            {
                url = "wss://" + Pusher.host + ":" + Pusher.wss_port + this.path;
            }
            else
            {
                url = "ws://" + Pusher.host + ":" + Pusher.ws_port + this.path;
            }

            Pusher.allow_reconnect = true;
            Pusher.Log("Pusher : connecting : " + url);

            var self = this;

            var ws = new WebSocket(url);

            // Timeout for the connection to handle silently hanging connections
            // Increase the timeout after each retry in case of extreme latencies
            new Thread(() =>
            {
                System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
                timer.Interval = Pusher.connection_timeout + (this.retry_counter * 1000);
                timer.Tick += (sender, e) =>
                {
                    timer.Stop();
                    Pusher.Log("Pusher : connection timeout after " + timer.Interval + "ms");
                };
                ws.Close();
            }).Start();

            ws.OnData += (sender, e) =>
            {
                OnMessage(e);
            };
            //ws.onmessage = function() {
            //  self.onmessage.apply(self, arguments);
            //};
            //ws.onclose = function() {
            //  window.clearTimeout(connectionTimeout);
            //  self.onclose.apply(self, arguments);
            //};
            //ws.onopen = function() {
            //  window.clearTimeout(connectionTimeout);
            //  self.onopen.apply(self, arguments);
            //};

            this.connection = ws;
        }

        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////// toggle_secure

        public void Disconnect()
        {
            Pusher.Log("Pusher : disconnecting");
            Pusher.allow_reconnect = false;
            this.retry_counter = 0;
            this.connection.Close();
        }

        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////// bind

        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////// bind_all

        public void SubscribeAll()
        {
            foreach (Channel channel in this.channels.Values.ToList<Channel>())
            {
                //if (this.channels.channels.hasOwnProperty(channel)) this.subscribe(channel);
            }
        }

        public Channel Subscribe(string channel_name)
        {
            Channel channel = this.channels.Add(channel_name, this);
            if (this.connected)
            {
                channel.Authorize(this, (data) =>
                {
                    this.SendEvent("pusher:subscribe", new Data()
                    {
                        { "channel", channel_name },
                        { "auth", data["auth"] },
                        { "channel_data", data["channel_data"] }
                    });
                }/*.scopedTo(this)*/);
            }
            return channel;
        }

        public void Unsubscribe(string channel_name)
        {
            this.channels.Remove(channel_name);

            if (this.connected)
            {
                this.SendEvent("pusher:unsubscribe", new Data()
                {
                    { "channel", channel_name }
                });
            }
        }

        public void SendEvent(string event_name, Data data)
        {
            SendEvent(event_name, data, null);
        }
        public Pusher SendEvent(string event_name, Data data, Channel channel)
        {
            Pusher.Log("Pusher : event sent (channel,event,data) : ", channel, event_name, data);

            var payload = new Data() {
                {"event", event_name},
                {"data", data}
            };
            if (channel != null) { payload["channel"] = channel; }

            this.connection.SendMessage(""/*JSON.stringify(payload)*/);
            return this;
        }

        ///////////////////////////////////////////////////////////////////////////////////////////////////////////////// send_local_event

        public void SendLocalEvent(string event_name, Data event_data, string channel_name)
        {
        }

        public void OnMessage(WebSocketEventArgs e)
        {
            Data paramss = /*JSON.parse(evt.data)*/ new Data();
            if (paramss.ContainsKey("socket_id") && paramss["socket_id"] == this.socket_id) return;
            // Try to parse the event data unless it has already been decoded
            if (paramss["data"] is string)
            {
                //paramss["data"] = Pusher.parser(params.data);
            }
            // Pusher.log("Pusher : received message : ", params)

            this.SendLocalEvent((string)paramss["event"], (Data)paramss["data"], (string)paramss["channel"]);
        }

        // Pusher defaults
        const string VERSION = "1.8.3";

        const string host = "ws.pusherapp.com";
        const int ws_port = 80;
        const int wss_port = 443;
        const string channel_auth_endpoint = "/pusher/auth";
        const int connection_timeout = 5000;
        const string cdn_http = "http://js.pusherapp.com/";
        const string cdn_https = "https://d3ds63zw57jt09.cloudfront.net/";
        private static void Log(string message) { }
        private static void Log(string message, object message2) { }
        private static void Log(string message, object message2, object message3) { }
        private static void Log(string message, object message2, object message3, object message4) { }
//Pusher.data_decorator = function(event_name, event_data){ return event_data }; // wrap event_data before dispatching
        static bool allow_reconnect = true;
        const string channel_auth_transport = "ajax";

        public object Parser(string data)
        {
            try
            {
                return JSON.parse(data);
            }
            catch
            {
                Pusher.Log("Pusher : data attribute not valid JSON - you may wish to implement your own Pusher.parser");
                return data;
            }
        }

        static bool isReady = false;
        public void ready()
        {
            Pusher.isReady = true;
            for(var i = 0; i < Pusher.instances.Count; i++) {
                if(!Pusher.instances[i].connected) Pusher.instances[i].Connect();
            }
        }

        public class Channel
        {
            public bool global;
            public Channel(string name)
            {
            }

            public void Authorize(Pusher pusher, Action<Data> data)
            {
            }
        }

        public class Data : Dictionary<string, object>
        {
            public Data() { }
        }
        public class Channels : Dictionary<string, Channel>
        {
            public Channels() { }
            public Channel Add(string channel_name, Pusher pusher)
            {
                return new Channel("SALKSDJKLJFDSF");
            }
        }

        private static class JSON
        {
            static JavaScriptSerializer _serializer = new JavaScriptSerializer() { MaxJsonLength = int.MaxValue };

            public static Data parse(string str)
            {
                return (Data)_serializer.DeserializeObject(str);
            }
        }
    }
}
