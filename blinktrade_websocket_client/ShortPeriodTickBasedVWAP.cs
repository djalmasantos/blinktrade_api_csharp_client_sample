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
				this.created = created;
			}
			public ulong tradeID;
			public string symbol;
			public ulong price;
			public ulong size;
			public string created;
		}

		private List<Trade> _lastTrades = new List<Trade>();
		private string _symbol;
		private ulong _allowedPeriodInMin;
		private double _cum_price_mul_size;
		private double _cum_volume;

		public ShortPeriodTickBasedVWAP(string symbol, ulong periodInMinutes)
		{
			_symbol = symbol;
			_allowedPeriodInMin = periodInMinutes;
			reset ();
		}

		public void reset(List<Trade> listOftrades = null)
		{
			_cum_price_mul_size = 0;
			_cum_volume = 0;
			_lastTrades.Clear();
			if (listOftrades != null && listOftrades.Count > 0) 
			{
				// TODO: sort the listOftrades chronologically and push the trades
			}

		}

		public void pushTrade(Trade trade)
		{
			if ( trade.symbol == this._symbol )
			{
				_cum_price_mul_size += ((double)(trade.price / 1e8) * (double)(trade.size / 1e8));
				_cum_volume += (double)(trade.size / 1e8);
				//_cum_price_mul_size += (trade.price * trade.size);
				//_cum_volume += trade.size;
				double vwap = _cum_price_mul_size / _cum_volume;
				ulong vwapUlong = (ulong)(vwap * 1e8);
				Console.WriteLine("{0} | {1} | {2} | {3} | {4}", trade.price, vwapUlong, vwap, trade.tradeID, trade.created);
				_lastTrades.Add(trade);
				// TODO: purge the old trades based in _allowedPeriodInMin (subtract trade data in the calc)
			}
		}

		public ulong calculateVWAP()
		{
			if (_cum_price_mul_size > 0 && _cum_volume > 0) 
				return (ulong)(_cum_price_mul_size / _cum_volume * 1e8);
			else
				return 0;
		}

		/*
		public ulong getLastPx()
		{
			return 0;
		}
		*/

	}
}

