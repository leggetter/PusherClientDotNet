using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using PusherClientDotNet;
using System.Text;
using System.Threading;
using System.ServiceModel.WebSockets;
using System.Net.Sockets;

namespace PusherSilverlightTestApp
{
    public partial class MainPage : UserControl
    {
        private Pusher _pusher;
        private Channel _channel;
        private bool _connected = false;
        Timer _timer;

        private const short MOUSE_MOVE_SENT_FREQUENCY_MILLIS = 500;

        private double _lastSentX = 0;
        private double _lastSentY = 0;
        private double _currentX = double.MinValue;
        private double _currentY = double.MinValue;

        private Guid _uniqueClientIdentifier = Guid.NewGuid();

        public MainPage()
        {            
            InitializeComponent();

            InitializePusher();

            this.MouseMove += new MouseEventHandler(MainPage_MouseMove);
        }

        void MainPage_MouseMove(object sender, MouseEventArgs e)
        {
            if (_connected)
            {
                System.Windows.Point position = e.GetPosition(this);
                _currentX = position.X;
                _currentY = position.Y;               
            }
            else
            {
                Log("Can't send mouse event - not connected");
            }
        }

        private void InitializePusher()
        {
            Pusher.host = "ws.staging.pusherapp.com";
            Pusher.ws_port = 4502;
            Pusher.wss_port = 4503;
            string rootUri = Application.Current.Host.Source.Scheme + "://" + Application.Current.Host.Source.Host;
            string authUri = rootUri + "/pusher/auth/";
            Pusher.channel_auth_endpoint = authUri;

            Pusher.OnLog += new PusherLogHandler(Pusher_OnLog);
            _pusher = new Pusher("YOUR_APP_KEY");

            _pusher.Bind("open", Opened);
        }        

        private void Opened(object obj)
        {
            _connected = true;

            _channel = _pusher.Subscribe("private-channel");
            _channel.Bind("client-mousemove", MouseMovedTriggered);

            _timer = new Timer(new TimerCallback(MousePositionChecker), null, 0, MOUSE_MOVE_SENT_FREQUENCY_MILLIS);
        }

        private void MousePositionChecker(object obj)
        {
            Log("Checking mouse position");

            bool inInitialState = (_currentX == double.MinValue && _currentY == double.MinValue);
            bool positionsChanged = (_lastSentX != _currentX || _lastSentY != _currentY);

            if (!inInitialState && positionsChanged)
            {
                var data = new Dictionary<string, object>(){
                    {"x", _currentX},
                    {"y", _currentY},
                    {"uid", _uniqueClientIdentifier.ToString()}
                };
                var jsonData =new JsonData(data);
                Log("Sending: " + Pusher.JSON.stringify(jsonData));
                _channel.Trigger("client-mousemove", jsonData);

                _lastSentX = _currentX;
                _lastSentY = _currentY;
            }
        }

        private void MouseMovedTriggered(object obj)
        {
            LogClientEvent(Pusher.JSON.stringify(obj));
        }

        #region Logging
        private void Pusher_OnLog(object sender, PusherLogEventArgs e)
        {
            StringBuilder msg = new StringBuilder();

            msg.AppendLine( e.Message );
            foreach (object obj in e.Additional)
            {
                if (obj is JsonData)
                    msg.AppendLine(Pusher.JSON.stringify(obj));
                else
                    msg.AppendLine(obj == null ? "null" : obj.ToString());
            }

            Log(msg.ToString());
        }

        private void LogClientEvent(string msg)
        {
            LogTo(msg, ClientLog);
        }

        private void Log(string msg)
        {
            LogTo(msg, DebugLog);
        }

        private void LogTo(string msg, TextBox logTo)
        {
            Dispatcher.BeginInvoke(() =>
            {
                string dateStr = DateTime.Now.ToString("HH:mm:ss");
                logTo.Text = string.Format("{0} - {1}{3}{2})",
                    dateStr,
                    msg,
                    logTo.Text,
                    Environment.NewLine);
            });
        }
        #endregion  
    }
}
