using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Blinktrade
{
    public class UserDevice
    {
        private const string _fingerPrint = "1730142891";
        private JObject _stuntip = new JObject();
        public UserDevice()
        {
            _stuntip["local"] = Util.GetLocalIPAddress();
            _stuntip["public"] = new JArray(Util.GetExternalIpAddress());
        }

        public string FingerPrint
        {
            get { return _fingerPrint; }
        }

        public JObject Stuntip
        {
            get { return _stuntip; }
        }
    }
}
