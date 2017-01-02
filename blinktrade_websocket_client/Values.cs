using System;
    
namespace Blinktrade
{
    // protocol defined values (FIX inherited)
    public class OrdType
    {
        public const char MARKET = '1';
        public const char LIMIT = '2';
        public const char STOP_LOSS = '3';
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
        public const char PARTICIPATE_DONT_INITIATE = '6';
    }
}