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
		private string _fingerPrint;
        private JObject _stuntip = new JObject();
		public UserDevice(string finger_print)
        {
            _stuntip["local"] = Util.GetLocalIPAddress();
            _stuntip["public"] = new JArray(Util.GetExternalIpAddress());
			_fingerPrint = finger_print != null ? finger_print : "123456789";
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
