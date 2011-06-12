using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PusherClientDotNet
{
    public class JsonData : Dictionary<string, object>
    {
        public JsonData() { }
        public JsonData(IDictionary<string, object> dictionary) : base(dictionary) { }
    }
}
