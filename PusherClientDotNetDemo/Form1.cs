using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using PusherClientDotNet;

namespace PusherClientDotNetDemo
{
    public partial class Form1 : Form
    {
        Pusher _pusher;
        Channel _pusherChannel;

        public Form1()
        {
            InitializeComponent();
            Pusher.OnLog += new PusherLogHandler(Pusher_OnLog);
        }

        void Pusher_OnLog(object sender, PusherLogEventArgs e)
        {
            if (textBoxConsole.InvokeRequired)
            {
                textBoxConsole.Invoke((Action)(() => Pusher_OnLog(sender, e)));
                return;
            }

            textBoxConsole.Text += e.Message;
            foreach (object obj in e.Additional)
            {
                textBoxConsole.Text += " | ";
                if (obj is JsonData)
                    textBoxConsole.Text += Pusher.JSON.stringify(obj);
                else
                    textBoxConsole.Text += obj == null ? "null" : obj.ToString();
            }

            textBoxConsole.Text += Environment.NewLine;
            textBoxConsole.Select(textBoxConsole.Text.Length, 0);
        }

        private void buttonConnect_Click(object sender, EventArgs e)
        {
            _pusher = new Pusher(textBoxApiKey.Text);
            _pusherChannel = _pusher.Subscribe(textBoxChannel.Text);
            foreach (string channel in textBoxEvents.Text.Split(','))
                _pusherChannel.Bind(channel.Trim(), d => MessageBox.Show(Pusher.JSON.stringify(d)));
            buttonConnect.Enabled = false;
            buttonDisconnect.Enabled = true;
        }

        private void buttonDisconnect_Click(object sender, EventArgs e)
        {
            _pusher.Disconnect();
            buttonConnect.Enabled = true;
            buttonDisconnect.Enabled = false;
        }
    }
}
