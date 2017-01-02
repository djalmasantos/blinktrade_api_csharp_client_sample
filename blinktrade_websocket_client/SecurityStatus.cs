using System;

namespace Blinktrade
{
	public class SecurityStatus
	{
		private ulong _sellVolume;
		private ulong _lowPx;
		private ulong _lastPx;
		private ulong _bestAsk;
		private ulong _highPx;
		private ulong _buyVolume;
		private ulong _bestBid;
		private string _symbol;
		private string _market;

		public ulong LastPx
		{
			get { return _lastPx;}
			set { _lastPx = value; }
		}

		public ulong BestAsk
		{
			get { return _bestAsk;}
			set { _bestAsk = value; }
		}

		public ulong BestBid
		{
			get { return _bestBid;}
			set { _bestBid = value; }
		}

		public ulong HighPx
		{
			get { return _highPx;}
			set { _highPx = value; }
		}

		public ulong LowPx
		{
			get { return _lowPx;}
			set { _lowPx = value; }
		}

		public ulong SellVolume
		{
			get { return _sellVolume; }
			set { _sellVolume = value; }
		}

		public ulong BuyVolume
		{
			get { return _buyVolume; }
			set { _buyVolume = value; }
		}

		public string Symbol
		{
			get { return _symbol;}
			set { _symbol = value; }
		}
		public string Market
		{
			get { return _market;}
			set { _market = value; }
		}
	};
}
