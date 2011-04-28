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
        Channel.Channels channels;
        Channel global_channel;
        bool secure;
        bool connected;
        int retry_counter;
        bool encrypted;
        WebSocket connection;

        public Pusher(string application_key) : this(application_key, null) { }
        public Pusher(string application_key, Dictionary<string, object> options)
        {
            Pusher.Initialize();
            if (options != null)
                this.options = options;
            this.path = "/app/" + application_key + "?client=js&version=" + Pusher.VERSION;
            this.key = application_key;
            this.channels = new Channel.Channels();
            this.global_channel = new Pusher.Channel("pusher_global_channel");
            this.global_channel.global = true;
            this.secure = false;
            this.connected = false;
            this.retry_counter = 0;
            if (options != null && options.ContainsKey("encrypted"))
                this.encrypted = ((bool)this.options["encrypted"]) ? true : false;
            if(Pusher.isReady) this.Connect();
            Pusher.instances.Add(this);

            //This is the new namespaced version
            this.Bind("pusher:connection_established", (data) =>
            {
                this.connected = true;
                this.retry_counter = 0;
                this.socket_id = (string)data["socket_id"];
                this.SubscribeAll();
            }/*.scopedTo(this)*/);

            this.Bind("pusher:connection_disconnected", (data) =>
            {
                foreach (string channel_name in this.channels.Keys.ToList<string>())
                {
                    this.channels[channel_name].Disconnect();
                }
            }/*.scopedTo(this)*/);

            this.Bind("pusher:error", (data) =>
            {
                Pusher.Log("Pusher : error : " + (string)data["message"]);
            });
        }

        static List<Pusher> instances = new List<Pusher>();
        
        public Channel GetChannel(string name)
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
            System.Timers.Timer connectionTimeout = new System.Timers.Timer();
            new Thread(() =>
            {
                connectionTimeout.Interval = Pusher.connection_timeout + (this.retry_counter * 1000);
                connectionTimeout.Elapsed += (sender, e) =>
                {
                    connectionTimeout.Stop();
                    Pusher.Log("Pusher : connection timeout after " + connectionTimeout.Interval + "ms");
                    ws.Close();
                };
                connectionTimeout.Start();
            }).Start();

            ws.OnData += (sender, e) => OnMessage(e);
            ws.OnClose += (sender, e) =>
            {
                connectionTimeout.Stop();
                OnClose();
            };
            ws.OnOpen += (sender, e) =>
            {
                connectionTimeout.Stop();
                OnOpen();
            };

            this.connection = ws;
        }

        public void ToggleSecure()
        {
            if (this.secure == false)
            {
                this.secure = true;
                Pusher.Log("Pusher : switching to wss:// connection");
            }
            else
            {
                this.secure = false;
                Pusher.Log("Pusher : switching to ws:// connection");
            };
        }

        public void Disconnect()
        {
            Pusher.Log("Pusher : disconnecting");
            Pusher.allow_reconnect = false;
            this.retry_counter = 0;
            this.connection.Close();
        }

        public Pusher Bind(string event_name, Action<Data> callback)
        {
            this.global_channel.Bind(event_name, callback);
            return this;
        }

        public Pusher BindAll(string event_name, Action<Data> callback)
        {
            this.global_channel.BindAll(callback);
            return this;
        }

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
        public Pusher SendEvent(string event_name, Data data, string channel)
        {
            Pusher.Log("Pusher : event sent (channel,event,data) : ", channel, event_name, data);

            var payload = new Data() {
                {"event", event_name},
                {"data", data}
            };
            if (channel != null) { payload["channel"] = channel; }

            this.connection.SendMessage(JSON.stringify(payload));
            return this;
        }

        public void SendLocalEvent(string event_name, Data event_data)
        {
            SendLocalEvent(event_name, event_data, null);
        }
        public void SendLocalEvent(string event_name, Data event_data, string channel_name)
        {
            event_data = Pusher.DataDecorator(event_name, event_data);
            if (channel_name != null)
            {
                if (this.channels.ContainsKey(channel_name))
                {
                    Channel channel = this.GetChannel(channel_name);
                    channel.DispatchWithAll(event_name, event_data);
                }
            }
            else
            {
                // Bit hacky but these events won't get logged otherwise
                Pusher.Log("Pusher : event recd (event,data) :", event_name, event_data);
            }

            this.global_channel.DispatchWithAll(event_name, event_data);
        }

        public void OnMessage(WebSocketEventArgs evt)
        {
            System.Windows.Forms.MessageBox.Show("woah");
            Data paramss = JSON.parse(evt.TextData);
            if (paramss.ContainsKey("socket_id") && paramss["socket_id"].ToString() == this.socket_id) return;
            // Try to parse the event data unless it has already been decoded
            if (paramss["data"] is string)
            {
                paramss["data"] = Pusher.Parser((string)paramss["data"]);
            }
            Pusher.Log("Pusher : received message : ", paramss);

            this.SendLocalEvent((string)paramss["event"], (Data)paramss["data"], (string)paramss["channel"]);
        }

        public void Reconnect()
        {
            new Thread(() => this.Connect()).Start();
        }

        public void RetryConnect()
        {
            // Unless we're ssl only, try toggling between ws & wss
            if (!this.encrypted)
            {
                this.ToggleSecure();
            }

            // Retry with increasing delay, with a maximum interval of 10s
            var retry_delay = Math.Min(this.retry_counter * 1000, 10000);
            Pusher.Log("Pusher : Retrying connection in " + retry_delay + "ms");
            System.Timers.Timer retryTimer = new System.Timers.Timer();
            new Thread(() =>
            {
                retryTimer.Interval = retry_delay;
                retryTimer.Elapsed += (sender, e) =>
                {
                    retryTimer.Stop();
                    this.Connect();
                };
                retryTimer.Start();
            });

            this.retry_counter = this.retry_counter + 1;
        }

        public void OnClose()
        {
            this.global_channel.Dispatch("close", null);
            Pusher.Log("Pusher : Socket closed");
            if (this.connected)
            {
                this.SendLocalEvent("pusher:connection_disconnected", new Data());
                if (Pusher.allow_reconnect)
                {
                    Pusher.Log("Pusher : Connection broken, trying to reconnect");
                    this.Reconnect();
                }
            }
            else
            {
                this.SendLocalEvent("pusher:connection_failed", null);
                this.RetryConnect();
            }
            this.connected = false;
        }

        public void OnOpen()
        {
            this.global_channel.Dispatch("open", null);
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
        public static Data DataDecorator(string event_name, Data event_data) { return event_data; } // wrap event_data before dispatching
        static bool allow_reconnect = true;
        const string channel_auth_transport = "ajax";

        public static object Parser(string data)
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
        public static void Ready()
        {
            Pusher.isReady = true;
            for(var i = 0; i < Pusher.instances.Count; i++) {
                if(!Pusher.instances[i].connected) Pusher.instances[i].Connect();
            }
        }

        public class Channel
        {
            public class Channels : Dictionary<string, Channel>
            {
                public Channels() { }
                public Channel Add(string channel_name, Pusher pusher)
                {
                    if (!this.ContainsKey(channel_name))
                    {
                        var channel = /*Pusher.Channel.factory(channel_name, pusher)*/ new Channel("SALKSDJKLJFDSF");
                        this[channel_name] = channel;
                        return channel;
                    }
                    else
                    {
                        return this[channel_name];
                    }
                }
            }

            Pusher pusher;
            string name;
            Dictionary<string, Callbacks> callbacks;
            Callbacks global_callbacks;
            bool subscribed;

            public bool global;
            public Channel(string channel_name) : this(channel_name, null) { }
            public Channel(string channel_name, Pusher pusher)
            {
                this.pusher = pusher;
                this.name = channel_name;
                this.callbacks = new Dictionary<string, Callbacks>();
                this.global_callbacks = new Callbacks();
                this.subscribed = false;
            }

            public void Disconnect() { }

            public void AcknowledgeSubscription(Data data)
            {
                this.subscribed = true;
            }

            public Channel Bind(string event_name, Action<Data> callback)
            {
                if (!this.callbacks.ContainsKey(event_name))
                    this.callbacks[event_name] = new Callbacks();
                this.callbacks[event_name].Add(callback);
                return this;
            }

            public Channel BindAll(Action<Data> callback)
            {
                this.global_callbacks.Add(callback);
                return this;
            }

            public Channel Trigger(string event_name, Data data)
            {
                this.pusher.SendEvent(event_name, data, this.name);
                return this;
            }

            public void DispatchWithAll(string event_name, Data data)
            {
                if (this.name != "pusher_global_channel")
                {
                    Pusher.Log("Pusher : event recd (channel,event,data)", this.name, event_name, data);
                }
                this.Dispatch(event_name, data);
                this.DispatchGlobalCallbacks(event_name, data);
            }

            public void Dispatch(string event_name, Data event_data)
            {
                if (this.callbacks.ContainsKey(event_name))
                {
                    foreach (Action<Data> callback in this.callbacks[event_name])
                    {
                        callback(event_data);
                    }
                }
                else if (!this.global)
                {
                    Pusher.Log("Pusher : No callbacks for " + event_name);
                }
            }

            public void DispatchGlobalCallbacks(string event_name, Data event_data)
            {
                foreach (Action<Data> callback in this.global_callbacks)
                {
                    // Is this correct or not? The JS passes both params...
                    callback(event_data);
                }
            }

            public bool IsPrivate() { return false; }

            public bool IsPresense() { return false; }

            public void Authorize(Pusher pusher, Action<Data> callback)
            {
                callback(new Data());
            }
        }

        static bool _initialized = false;
        public static void Initialize()
        {
            if (!_initialized)
            {
                _initialized = true;
                Pusher.Ready();
            }
        }

        public class Callbacks : List<Action<Data>>
        {
            public Callbacks() { }
        }
        public class Data : Dictionary<string, object>
        {
            public Data() { }
        }

        public static class JSON
        {
            static JavaScriptSerializer _serializer = new JavaScriptSerializer() { MaxJsonLength = int.MaxValue };

            public static Data parse(string str)
            {
                return (Data)_serializer.DeserializeObject(str);
            }

            public static string stringify(object obj)
            {
                return _serializer.Serialize(obj);
            }
        }
    }
}
