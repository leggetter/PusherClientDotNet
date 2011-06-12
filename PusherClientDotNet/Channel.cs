using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.Threading;

namespace PusherClientDotNet
{
    public class Channel
    {
        public class Channels : Dictionary<string, Channel>
        {
            internal Channels channels { get { return this; } }

            public Channels() { }
            public Channel Add(string channel_name, Pusher pusher)
            {
                if (!this.ContainsKey(channel_name))
                {
                    var channel = Channel.factory(channel_name, pusher);
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

        public void AcknowledgeSubscription(JsonData data)
        {
            this.subscribed = true;
        }

        public Channel Bind(string event_name, Action<object> callback)
        {
            if (!this.callbacks.ContainsKey(event_name))
                this.callbacks[event_name] = new Callbacks();
            this.callbacks[event_name].Add(callback);
            return this;
        }

        public Channel BindAll(Action<object> callback)
        {
            this.global_callbacks.Add(callback);
            return this;
        }

        public Channel Trigger(string event_name, JsonData data)
        {
            this.pusher.SendEvent(event_name, data, this.name);
            return this;
        }

        internal void DispatchWithAll(string event_name, object data)
        {
            if (this.name != "pusher_global_channel")
            {
                Pusher.Log("Pusher : event recd (channel,event,data)", this.name, event_name, data);
            }
            this.Dispatch(event_name, data);
            this.DispatchGlobalCallbacks(event_name, data);
        }

        internal void Dispatch(string event_name, object event_data)
        {
            if (this.callbacks.ContainsKey(event_name))
            {
                foreach (Action<object> callback in this.callbacks[event_name])
                {
                    callback(event_data);
                }
            }
            else if (!this.global)
            {
                Pusher.Log("Pusher : No callbacks for " + event_name);
            }
        }

        public void DispatchGlobalCallbacks(string event_name, object event_data)
        {
            foreach (Action<object> callback in this.global_callbacks)
            {
                // Is this correct or not? The JS passes both params...
                callback(event_data);
            }
        }

        public bool IsPrivate { get; internal set; }

        public bool IsPresence { get; internal set; }

        public void Authorize(Pusher pusher, Action<object> callback)
        {
            JsonData data;

            if (IsPrivate)
            {
                if (string.IsNullOrWhiteSpace(Pusher.channel_auth_endpoint))
                {
                    throw new InvalidOperationException("Pusher.channel_auth_endpoint must be set to authorize a channel");
                }

                string url = Pusher.channel_auth_endpoint + "?socket_id=" + pusher.socket_id + "&channel_name=" + this.name;

                AutoResetEvent downloadedEvent = new AutoResetEvent(false);
                string response = string.Empty;

                try
                {
                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                    request.Method = "POST";
                    request.Accept = "application/json";
                    request.ContentType = "application/x-www-form-urlencoded";

                    bool supportsCookieContainer = true;
#if SILVERLIGHT
                        supportsCookieContainer = request.SupportsCookieContainer;
#endif
                    if (Pusher.AuthCookieContainer != null && supportsCookieContainer)
                    {
                        request.CookieContainer = Pusher.AuthCookieContainer;
                    }

                    request.BeginGetResponse(delegate(IAsyncResult asynchronousResult)
                    {
                        HttpWebResponse resp = (HttpWebResponse)request.EndGetResponse(asynchronousResult);
                        Stream stream = resp.GetResponseStream();
                        var sr = new StreamReader(stream);
                        response = sr.ReadToEnd();
                        downloadedEvent.Set();
                    }, null);

                    bool triggered = downloadedEvent.WaitOne(Pusher.connection_timeout);
                    if (!triggered)
                    {
                        Pusher.Log("Auth call timed out after {0} milliseconds", Pusher.connection_timeout);
                        data = new JsonData();
                    }
                    else
                    {
                        data = (JsonData)Pusher.Parser(response);
                    }
                }
                catch (Exception ex)
                {
                    Pusher.Log("Exception occurred in Auth call. {0}", ex);
                    data = new JsonData();
                }
            }
            else
            {
                data = new JsonData();
            }
            callback(data);
        }

        internal static Channel factory(string channel_name, Pusher pusher)
        {
            var channel = new Channel(channel_name, pusher);
            if (channel_name.IndexOf(Channel.private_prefix) == 0)
            {
                channel.IsPrivate = true;
            }
            else if (channel_name.IndexOf(Channel.presence_prefix) == 0)
            {
                throw new Exception("PusherClientDotNet: Presense channels not implemented yet");
                //Pusher.Util.extend(channel, Pusher.Channel.PrivateChannel);
                //Pusher.Util.extend(channel, Pusher.Channel.PresenceChannel);
            };
            //channel.Init();// inheritable constructor
            return channel;
        }

        const string private_prefix = "private-";
        const string presence_prefix = "presence-";
    }

    public class Callbacks : List<Action<object>>
    {
        public Callbacks() { }
    }

}
