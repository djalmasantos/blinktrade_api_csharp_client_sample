using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blinktrade
{
    public class UserAccountCredentials
    {
        private int _brokerId;
        private string _username;
        private string _password;
        private string _secondFactor;

        public UserAccountCredentials(int broker_id, string username, string password, string second_factor = null)
        {
            _brokerId = broker_id;
            _username = username;
            _password = password;
            _secondFactor = second_factor;
        }

        public int BrokerId
        {
            get { return _brokerId; }
        }

        public string Username
        {
            get { return _username; }
        }

        public string Password
        {
            get { return _password; }
            set { _secondFactor = value; }
        }

        public string SecondFactor
        {
            get { return _secondFactor; }
            set { _secondFactor = value; }
        }
    }
}