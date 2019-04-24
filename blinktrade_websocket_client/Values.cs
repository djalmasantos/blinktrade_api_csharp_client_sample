using System;
    
namespace Blinktrade
{
    // protocol defined values (most of them FIX inherited values)
    public class OrdType
    {
        public const char MARKET = '1';
        public const char LIMIT = '2';
        public const char STOP_MARKET = '3';
        public const char STOP_LIMIT = '4';
    }
    
    public class OrdStatus
    {
        public const char NEW = '0';
        public const char PARTIALLY_FILLED = '1';
        public const char FILLED = '2';
        public const char CANCELED = '4';
        public const char PENDING_CANCEL = '6'; // client control - in the case no response was received for a Cancel Request
        public const char STOPPED = '7';
        public const char REJECTED = '8';
        public const char PENDING_NEW = 'A'; // client control - in the case no response was received
    }

    public class OrderSide 
    { 
        public const char BUY  = '1';
        public const char SELL = '2';
    }

    public class TimeInForce
    { 
        public const char GOOD_TILL_CANCEL = '1';
    }

    public class ExecInst 
    {
        public const char DEFAULT = default(char);
        public const char PARTICIPATE_DONT_INITIATE = '6';
    }

	// Bitwise Cancel Open Orders Flag
	public enum COOFlag { 
		DO_NOT_CANCEL_OPEN_ORDERS = 0,
		CANCEL_ON_LOGON = 1,
		CANCEL_ON_APP_EXIT = 2, 
		CANCEL_ON_DISCONNECT = 4,
		DEFAULT = CANCEL_ON_LOGON | CANCEL_ON_APP_EXIT | CANCEL_ON_DISCONNECT
	};

}