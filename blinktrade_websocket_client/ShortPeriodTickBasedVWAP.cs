using System;
using System.Collections.Generic;

namespace Blinktrade
{
	// vwap: the average price weighted by volume
	public class ShortPeriodTickBasedVWAP
	{
		public struct Trade
		{
			public Trade(ulong id, string symbol, ulong price, ulong size, string created)
			{
				tradeID = id;
				this.symbol = symbol;
				this.price = price;
				this.size = size;
				this.created = DateTime.ParseExact(created, "yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
			}
			public ulong tradeID;
			public string symbol;
			public ulong price;
			public ulong size;
			public DateTime created;
		}

		private List<Trade> _lastTrades = new List<Trade>();
		private string _symbol;
		private TimeSpan _minutesOffset;
		private double _cum_price_mul_size;
		private double _cum_volume;


		public ShortPeriodTickBasedVWAP(string symbol, ulong periodInMinutes = 0)
		{
			_symbol = symbol;
			_cum_price_mul_size = 0;
			_cum_volume = 0;
			setPeriod(periodInMinutes);
		}

		public void setPeriod(ulong periodInMinutes)
		{
			ulong offset;
			if (periodInMinutes < 1) 
				offset = 1; // minimum 1 minute
			else if (periodInMinutes > 1440) 
				offset = 1440; // max intraday
			else
				offset = periodInMinutes;

			_minutesOffset = new TimeSpan(0, (int)(offset), 0);
		}

		public void pushTrade(Trade trade)
		{
            if ( trade.symbol == this._symbol ) 
			{
                // TODO: prevent reinsertion of trades
                /*
                if (_lastTrades.Count > 0 && trade.tradeID < _lastTrades[_lastTrades.Count - 1].tradeID) {
                    return;
                }
                */
           
                // update the cumulative values
                _cum_price_mul_size += ((double)(trade.price / 1e8) * (double)(trade.size / 1e8));
				_cum_volume += (double)(trade.size / 1e8);

				// purge the old trades based in the desired vwap period
				DateTime dtLimit = trade.created - _minutesOffset;
				while (_lastTrades.Count > 0)
				{
					var oldestTrade = _lastTrades[0];
					if ( oldestTrade.created < dtLimit )
					{
						// subtract the old trade
						_cum_price_mul_size -= ((double)(oldestTrade.price / 1e8) * (double)(oldestTrade.size / 1e8));
						_cum_volume -= (double)(oldestTrade.size / 1e8);
						_lastTrades.RemoveAt(0);
						continue;
					}
					break;
				}
				_lastTrades.Add(trade);
			}
		}

		public ulong calculateVWAP()
		{
			if ( _cum_volume > 0 ) 
				return (ulong)(Math.Round(_cum_price_mul_size / _cum_volume, 2) * 1e8);
			else
				return 0;
		}


		public ulong getLastPx()
		{
			return _lastTrades.Count > 0 ? _lastTrades[_lastTrades.Count-1].price : 0;
		}

		public void PrintTradesAndTheVWAP()
		{
			foreach (var t in this._lastTrades) {
				Console.WriteLine ("{0} | {1} | {2} | {3}", t.tradeID, t.created.ToString (), t.price, t.size);
			}
			Console.WriteLine ("VWAP = {0}", calculateVWAP ());
		}


	}
}

