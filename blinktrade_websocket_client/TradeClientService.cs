using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blinktrade
{
    public interface ITradeClientService
    {
        MiniOMS miniOMS { get; }
        
		OrderBook GetOrderBook(string symbol);

		SecurityStatus GetSecurityStatus(string market, string symbol);
        
		ulong UserId { get; }
        
		int BrokerId { get; }
        
		string SendOrder(IWebSocketClientConnection connection, 
			string symbol, ulong qty, ulong price, 
			char side, int broker_id, string client_order_id, 
			char order_type = OrdType.LIMIT, char execInst = default(char));
        
		bool CancelOrderByClOrdID(IWebSocketClientConnection connection, string clOrdID);
        
		ulong GetBalance(string currency);
    }
}