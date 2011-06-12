/**
 * PusherClientDotNet v1.8.3-incomplete
 * C# port by Richard Z.H. Wang <http://rewrite.name/>
 *
 * Copyright 2010, 2011, New Bamboo
 * Copyright 2011, Richard Z.H. Wang
 * Released under the MIT licence.
 */

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.ServiceModel.WebSockets;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using System.IO;

namespace PusherClientDotNet
{
    public class Pusher
    {
        // Pusher defaults
        public const string VERSION = "1.8.3";

        public static string host = "ws.pusherapp.com";
        public static int ws_port = 80;
        public static int wss_port = 443;

#if SILVERLIGHT
        // we can't get the host from the running application from within a library
        // so we set as null and check later. Setting this end point is essential for
        // Silverlight clients
        public static string channel_auth_endpoint = null;
#else
        public static string channel_auth_endpoint = "/pusher/auth";
#endif
        
        public static int connection_timeout = 5000;
        public static string cdn_http = "http://js.pusherapp.com/";
        public static string cdn_https = "https://d3ds63zw57jt09.cloudfront.net/";

        Dictionary<string, object> options = new Dictionary<string, object>();
        string path;
        string key;
        public string socket_id
        {
            get;
            private set;
        }
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
            this.global_channel = new Channel("pusher_global_channel");
            this.global_channel.global = true;
            this.secure = false;
            this.connected = false;
            this.retry_counter = 0;
            if (options != null && options.ContainsKey("encrypted"))
                this.encrypted = ((bool)this.options["encrypted"]) ? true : false;
            if (Pusher.isReady) this.Connect();
            Pusher.instances.Add(this);

            //This is the new namespaced version
            this.Bind("pusher:connection_established", d =>
            {
                JsonData data = (JsonData)d;
                this.connected = true;
                this.retry_counter = 0;
                this.socket_id = (string)data["socket_id"];
                this.SubscribeAll();
            });

            this.Bind("pusher:connection_disconnected", d =>
            {
                foreach (string channel_name in this.channels.Keys.ToList<string>())
                {
                    this.channels[channel_name].Disconnect();
                }
            });

            this.Bind("pusher:error", d =>
            {
                JsonData data = (JsonData)d;
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
            int interval = Pusher.connection_timeout + (this.retry_counter * 1000);

            var timerRef = new TimerRef();
            timerRef.Ref = new Timer(delegate(object state)
            {
                Pusher.Log("Pusher : connection timeout after " + interval + "ms");
                ws.Close();
                try { timerRef.Ref.Dispose(); }
                catch { }

            }, null, interval, interval);

            ws.OnData += (sender, e) => OnMessage(e);
            ws.OnClose += (sender, e) =>
            {
                try { timerRef.Ref.Dispose(); }
                catch { }
                OnClose();
            };
            ws.OnOpen += (sender, e) =>
            {
                try { timerRef.Ref.Dispose(); }
                catch { }
                OnOpen();
            };

            this.connection = ws;

            ws.Open();
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

        public Pusher Bind(string event_name, Action<object> callback)
        {
            this.global_channel.Bind(event_name, callback);
            return this;
        }

        public Pusher BindAll(string event_name, Action<object> callback)
        {
            this.global_channel.BindAll(callback);
            return this;
        }

        public void SubscribeAll()
        {
            foreach (string channel in this.channels.channels.Keys.ToList<string>())
            {
                if (this.channels.channels.ContainsKey(channel)) this.Subscribe(channel);
            }
        }

        public Channel Subscribe(string channel_name)
        {
            Channel channel = this.channels.Add(channel_name, this);
            if (this.connected)
            {
                new Thread(() =>

                    channel.Authorize(this, d =>
                    {
                        JsonData data = (JsonData)d;
                        this.SendEvent("pusher:subscribe", new JsonData()
                        {
                            { "channel", channel_name },
                            { "auth", data.ContainsKey("auth") ? data["auth"] : null },
                            { "channel_data", data.ContainsKey("channel_data") ? data["channel_data"] : null }
                        });
                    })

                ).Start();
            }
            return channel;
        }

        public void Unsubscribe(string channel_name)
        {
            this.channels.Remove(channel_name);

            if (this.connected)
            {
                this.SendEvent("pusher:unsubscribe", new JsonData()
                {
                    { "channel", channel_name }
                });
            }
        }

        internal void SendEvent(string event_name, JsonData data)
        {
            SendEvent(event_name, data, null);
        }
        internal Pusher SendEvent(string event_name, JsonData data, string channel)
        {
            Pusher.Log("Pusher : event sent (channel,event,data) : ", channel, event_name, data);

            var payload = new JsonData() {
                {"event", event_name},
                {"data", data}
            };
            if (channel != null) { payload["channel"] = channel; }

            this.connection.SendMessage(JSON.stringify(payload));
            return this;
        }

        private void SendLocalEvent(string event_name, object event_data)
        {
            SendLocalEvent(event_name, event_data, null);
        }
        private void SendLocalEvent(string event_name, object event_data, string channel_name)
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

        private void OnMessage(WebSocketEventArgs evt)
        {
            Pusher.Log("Pusher : OnMessage : ", evt.TextData);

            JsonData paramss = JSON.parse(evt.TextData);
            if (paramss.ContainsKey("socket_id") && paramss["socket_id"].ToString() == this.socket_id) return;
            // Try to parse the event data unless it has already been decoded
            if (paramss["data"] is string)
            {
                paramss["data"] = Pusher.Parser((string)paramss["data"]);
            }
            Pusher.Log("Pusher : received message : ", paramss);

            if (paramss.ContainsKey("channel"))
                this.SendLocalEvent((string)paramss["event"], paramss["data"], (string)paramss["channel"]);
            else
                this.SendLocalEvent((string)paramss["event"], paramss["data"]);
        }

        private void Reconnect()
        {
            new Thread(() => this.Connect()).Start();
        }

        private void RetryConnect()
        {
            // Unless we're ssl only, try toggling between ws & wss
            if (!this.encrypted)
            {
#if !SILVERLIGHT
                // not supported by silverlight
                this.ToggleSecure();
#endif
            }

            // Retry with increasing delay, with a maximum interval of 10s
            var retry_delay = Math.Min(this.retry_counter * 1000, 10000);
            Pusher.Log("Pusher : Retrying connection in " + retry_delay + "ms");

            int interval = Pusher.connection_timeout + (this.retry_counter * 1000);
            var timerRef = new TimerRef();
            timerRef.Ref = new Timer(delegate(object state)
            {
                this.Connect();

                try { timerRef.Ref.Dispose(); }
                catch { }

            }, timerRef, retry_delay, retry_delay);

            this.retry_counter = this.retry_counter + 1;
        }

        struct TimerRef
        {
            public Timer Ref;
        }

        private void OnClose()
        {
            this.global_channel.Dispatch("close", null);
            Pusher.Log("Pusher : Socket closed");
            if (this.connected)
            {
                this.SendLocalEvent("pusher:connection_disconnected", new JsonData());
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

        private void OnOpen()
        {
            this.global_channel.Dispatch("open", null);
        }



        public static event PusherLogHandler OnLog;
        internal static void Log(string message, params object[] additional)
        {
            if (OnLog != null) OnLog(null, new PusherLogEventArgs() { Message = message, Additional = additional });
        }

        public static object DataDecorator(string event_name, object event_data) { return event_data; } // wrap event_data before dispatching
        static bool allow_reconnect = true;
        //static string channel_auth_transport = "ajax";

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
            for (var i = 0; i < Pusher.instances.Count; i++)
            {
                if (!Pusher.instances[i].connected) Pusher.instances[i].Connect();
            }
        }

        public static CookieContainer AuthCookieContainer { get; set; }

        static bool _initialized = false;
        public static void Initialize()
        {
            if (!_initialized)
            {
                _initialized = true;
                Pusher.Ready();
            }
        }

        
        

        public static class JSON
        {
            public static JsonData parse(string str)
            {
                var obj = JsonConvert.DeserializeObject<IDictionary<string, object>>(str);
                return new JsonData(obj);
            }

            public static string stringify(object obj)
            {
                return JsonConvert.SerializeObject(obj);
            }
        }
    }

    public delegate void PusherLogHandler(object sender, PusherLogEventArgs e);
    public class PusherLogEventArgs : EventArgs
    {
        public string Message { get; internal set; }
        public object[] Additional { get; internal set; }
        public PusherLogEventArgs() { }
    }
}
