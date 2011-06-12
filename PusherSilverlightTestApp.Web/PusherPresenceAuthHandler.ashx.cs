using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Pusher.Authentication;
using System.Configuration;

namespace PusherSilverlightTestApp.Web
{
    /// <summary>
    /// Summary description for PusherAuth
    /// </summary>
    public class PusherPresenceAuthHandler : IHttpHandler
    {
        private string socketID = string.Empty;
        private string channelName = string.Empty;
        private string applicationId = string.Empty;
        private string applicationKey = string.Empty;
        private string applicationSecret = string.Empty;
        private string memberName = string.Empty;

        public void SetupDefaultProvider(HttpContext context)
        {
            applicationId = ConfigurationManager.AppSettings["pusher-application-id"];
            applicationKey = ConfigurationManager.AppSettings["pusher-application-key"];
            applicationSecret = ConfigurationManager.AppSettings["pusher-application-secret"];

            socketID = context.Request["socket_id"].ToString();
            channelName = context.Request["channel_name"].ToString();
            if (context.Session != null && context.Session["member_name"] != null)
            {
                memberName = context.Session["member_name"].ToString();
            }
            else
            {
                memberName = "Guest " + DateTime.Now.Ticks;
            }
        }

        public void ProcessRequest(HttpContext context)
        {
            SetupDefaultProvider(context);

            PresenceChannelData channelData = new PresenceChannelData { user_id = socketID, user_info = new BasicUserInfo { name = memberName } };
            var helper = new PusherAuthenticationHelper(applicationId, applicationKey, applicationSecret);
            string authJson = helper.CreateAuthenticatedString(socketID, channelName, channelData);

            context.Response.Write(authJson);
        }

        public bool IsReusable
        {
            get
            {
                return false;
            }
        }
    }
}