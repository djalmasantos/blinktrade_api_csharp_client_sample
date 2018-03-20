using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading;
using System.Threading.Tasks.Dataflow;

#if __MonoCS__
using Mono.Unix;
#else
using System.Runtime.InteropServices;
#endif

namespace Blinktrade
{
	class SimpleTradeClient : ITradeClientService
    {
        private int _brokerId;
        private string _tradingSymbol;
        private ulong _myUserID = 0;
        private Dictionary<string, ulong> _balances = new Dictionary<string,ulong>();
		private MiniOMS _miniOMS = new MiniOMS();
		// TODO: MarketDataHelper class
		protected Dictionary<string, OrderBook> _allOrderBooks = new Dictionary<string, OrderBook>();
		protected SortedDictionary<string, SecurityStatus> _securityStatusEntries = new SortedDictionary<string, SecurityStatus>();
		ShortPeriodTickBasedVWAP _vwapForTradingSym; // TODO: use a dictionary here too

        private TradingStrategy _tradingStrategy;
		private IWebSocketClientProtocolEngine _protocolEngine;
		private ulong _soldAmount = 0;
		private static volatile bool _userRequestExit = false;

		SimpleTradeClient(int broker_id, string symbol, TradingStrategy strategy, IWebSocketClientProtocolEngine protocolEngine)
        {
            _brokerId = broker_id;
            _tradingSymbol = symbol;
            _tradingStrategy = strategy;
			_protocolEngine = protocolEngine;
            _tradingStrategy.tradeclient = this;
			_vwapForTradingSym = new ShortPeriodTickBasedVWAP(_tradingSymbol, 30);
        }

		public void ResetData()
		{
			_balances.Clear();
			_miniOMS = new MiniOMS();
			_allOrderBooks.Clear();
			_securityStatusEntries.Clear();
			_vwapForTradingSym = new ShortPeriodTickBasedVWAP(_tradingSymbol, 30);
			_tradingStrategy.Reset();
		}

		public ulong UserId
        {
            get
            {
                if (_myUserID > 0)
                    return _myUserID;
                else
                    throw new InvalidOperationException();
            }
        }

        public int BrokerId { get { return _brokerId; } }

        public MiniOMS miniOMS { get { return _miniOMS; } }

        public OrderBook GetOrderBook(string symbol)
        {
            OrderBook orderBook = null;
            if (_allOrderBooks.TryGetValue(symbol, out orderBook))
                return orderBook;
            else
                throw new ArgumentException("Symbol not found : " + symbol);
        }

        public ulong GetBalance(string currency)
        {
            ulong result;
            if (_balances.TryGetValue(currency, out result))
                return result;
            else
                return 0;
        }

		public string GetTradingSymbol()
		{
			return _tradingSymbol;
		}

		public ulong GetSoldAmount(/*string symbol*/)
		{
			return _soldAmount;
		}

		public ulong CalculateVWAP(/*string symbol*/)
		{
			return this._vwapForTradingSym.calculateVWAP();
		}

		public ulong GetLastPrice(/*string symbol*/)
		{
			return this._vwapForTradingSym.getLastPx();
		}

		public SecurityStatus GetSecurityStatus(string market, string symbol)
		{
			string key = market + ":" + symbol;
			SecurityStatus result = null;
			if (_securityStatusEntries.TryGetValue (key, out result))
				return result;
			else
				return null;
		}

        private void OnBrokerNotification(object sender, SystemEventArgs evt)
        {
            IWebSocketClientConnection webSocketConnection = (IWebSocketClientConnection)sender;
            try
            {
                switch (evt.evtType)
                {
                    case SystemEventType.LOGIN_OK:
                        LogStatus(LogStatusType.INFO, "Processing after succesful LOGON");
                        this._myUserID = evt.json["UserID"].Value<ulong>();
						// disable test request to avoid disconnection during the "slow" market data processing
						webSocketConnection.EnableTestRequest = false; 
                        StartInitialRequestsAfterLogon(webSocketConnection);
                        break;

                    case SystemEventType.MARKET_DATA_REQUEST_REJECT:
                        LogStatus(LogStatusType.ERROR, "Unexpected Marketdata Request Reject");
                        webSocketConnection.Shutdown();
                        break;

                    case SystemEventType.MARKET_DATA_FULL_REFRESH:
                        {
                            string symbol = evt.json["Symbol"].Value<string>();
                            // dump the order book
                            LogStatus(LogStatusType.WARN, _allOrderBooks[symbol].ToString());
                            // bring back the testrequest keep-alive mechanism after processing the book
                            webSocketConnection.EnableTestRequest = true;
                            // run the trading strategy to buy and sell orders based on the top of the book
                            _tradingStrategy.runStrategy(webSocketConnection, symbol);
							// TODO: remove the temp dump bellow
							this._vwapForTradingSym.PrintTradesAndTheVWAP(); 
							// example how to notify the application to start
							//this._tradingStrategy.OnStart(webSocketConnection);
							
                        }
                        break;

                    // --- Order Book Management Events ---
                    case SystemEventType.ORDER_BOOK_CLEAR:
                        {
                            string symbol = evt.json["Symbol"].Value<string>();
                            OrderBook orderBook = null;
                            if (_allOrderBooks.TryGetValue(symbol, out orderBook))
                            {
                                orderBook.Clear();
                            }
                            else
                            {
                                orderBook = new OrderBook(symbol);
                                _allOrderBooks.Add(symbol, orderBook);
                            }

                        }
                        break;

                    case SystemEventType.ORDER_BOOK_NEW_ORDER:
                        {
                            string symbol = evt.json["Symbol"].Value<string>();
                            OrderBook orderBook = null;
                            if (_allOrderBooks.TryGetValue(symbol, out orderBook))
                                orderBook.AddOrder(evt.json);
                            else
                                LogStatus(LogStatusType.ERROR, 
									"Order Book not found for Symbol " + symbol + " @ " + evt.evtType.ToString());
                        }
                        break;

                    case SystemEventType.ORDER_BOOK_DELETE_ORDERS_THRU:
                        {
                            string symbol = evt.json["Symbol"].Value<string>();
                            OrderBook orderBook = null;
                            if (_allOrderBooks.TryGetValue(symbol, out orderBook))
                                orderBook.DeleteOrdersThru(evt.json);
                            else
                                LogStatus(LogStatusType.ERROR, 
									"Order Book not found for Symbol " + symbol + " @ " + evt.evtType.ToString()
								);
                        }
                        break;

                    case SystemEventType.ORDER_BOOK_DELETE_ORDER:
                        {
                            string symbol = evt.json["Symbol"].Value<string>();
                            OrderBook orderBook = null;
                            if (_allOrderBooks.TryGetValue(symbol, out orderBook))
                                orderBook.DeleteOrder(evt.json);
                            else
                                LogStatus(LogStatusType.ERROR, 
									"Order Book not found for Symbol " + symbol + " @ " + evt.evtType.ToString()
								);
                        }
                        break;

                    case SystemEventType.ORDER_BOOK_UPDATE_ORDER:
                        {
                            string symbol = evt.json["Symbol"].Value<string>();
                            OrderBook orderBook = null;
                            if (_allOrderBooks.TryGetValue(symbol, out orderBook))
                            {
                                orderBook.UpdateOrder(evt.json);

                            }
                            else
                                LogStatus(LogStatusType.ERROR, 
									"Order Book not found for Symbol " + symbol + " @ " + evt.evtType.ToString()
								);
                        }
                        break;
                    // ------------------------------------

                    case SystemEventType.TRADE_CLEAR:
                        LogStatus(LogStatusType.WARN, "Receieved Market Data Event " + evt.evtType.ToString());
                        break;

                    case SystemEventType.SECURITY_STATUS:
                        {
                            LogStatus(LogStatusType.WARN, 
								"Receieved Market Data Event " + 
								evt.evtType.ToString() + " " + 
								(evt.json != null ? evt.json.ToString() : ".")
							);
							
							SecurityStatus securityStatus = new SecurityStatus();
							securityStatus.Market = evt.json["Market"].Value<string>();
							securityStatus.Symbol = evt.json["Symbol"].Value<string>();
							securityStatus.LastPx = evt.json["LastPx"].Value<ulong>();
							securityStatus.HighPx = evt.json["HighPx"].Value<ulong>();
							
							if ( evt.json["BestBid"].Type != JTokenType.Null )
								securityStatus.BestBid = evt.json["BestBid"].Value<ulong>();
							else 
								securityStatus.BestBid = 0;

							if ( evt.json["BestAsk"].Type != JTokenType.Null )
								securityStatus.BestAsk = evt.json["BestAsk"].Value<ulong>();
							else
								securityStatus.BestAsk = 0;
						
							if ( evt.json["LowPx"].Type != JTokenType.Null )
								securityStatus.LowPx = evt.json["LowPx"].Value<ulong>();
							else
								securityStatus.LowPx = 0;

							securityStatus.SellVolume = evt.json["SellVolume"].Value<ulong>();
							securityStatus.BuyVolume  = evt.json["BuyVolume"].Value<ulong>();
                            
							// update the security status information
							string securityKey = securityStatus.Market + ":" + securityStatus.Symbol;
							_securityStatusEntries[securityKey] = securityStatus;

							// update the strategy when a new market information arrives
							_tradingStrategy.runStrategy(webSocketConnection, _tradingSymbol);
                        }
                        break;

					case SystemEventType.TRADE:
						{
							JObject msg = evt.json;
							LogStatus(LogStatusType.WARN, "Receieved Market Data Event " + evt.evtType.ToString() + msg);

							_vwapForTradingSym.pushTrade( 
									new ShortPeriodTickBasedVWAP.Trade(
										msg["TradeID"].Value<ulong>(),
										msg["Symbol"].Value<string>(),
										msg["MDEntryPx"].Value<ulong>(),
										msg["MDEntrySize"].Value<ulong>(),
										String.Format("{0} {1}", msg["MDEntryDate"].Value<string>(),msg["MDEntryTime"].Value<string>())
									)
							);

						}
						break;
				
                    case SystemEventType.TRADING_SESSION_STATUS:
						break;

                    case SystemEventType.MARKET_DATA_INCREMENTAL_REFRESH:
                        LogStatus(LogStatusType.WARN, "Receieved Market Data Incremental Refresh : " + evt.evtType.ToString());
						// update the strategy when an incremental message is processed
						_tradingStrategy.runStrategy(webSocketConnection, _tradingSymbol);
                        break;

                    // --- Order Entry Replies ---
                    case SystemEventType.EXECUTION_REPORT:
						{
	                        LogStatus(LogStatusType.WARN, "Receieved " + evt.evtType.ToString() + "\n" + evt.json.ToString());
							MiniOMS.IOrder order = ProcessExecutionReport(evt.json);
							_tradingStrategy.OnExecutionReport(webSocketConnection, order);
						}
						break;

                    case SystemEventType.ORDER_LIST_RESPONSE:
                        {
                            // process the requested list of orders
                            JObject msg = evt.json;
                            LogStatus(LogStatusType.WARN, 
								"Received " + evt.evtType.ToString() + " : " + "Page=" + msg["Page"].Value<string>()
							);
                            JArray ordersLst = msg["OrdListGrp"].Value<JArray>();

                            if (ordersLst != null && ordersLst.Count > 0)
                            {
                                var columns = msg["Columns"].Value<JArray>();
                                Dictionary<string, int> indexOf = new Dictionary<string, int>();
                                int index = 0;
                                foreach (JToken col in columns)
                                {
                                    indexOf.Add(col.Value<string>(), index++);
                                }

                                foreach (JArray data in ordersLst)
                                {
                                    MiniOMS.Order order = new MiniOMS.Order();
                                    order.ClOrdID = data[indexOf["ClOrdID"]].Value<string>();
                                    order.OrderID = data[indexOf["OrderID"]].Value<ulong>();
                                    order.Symbol = data[indexOf["Symbol"]].Value<string>();
                                    order.Side = data[indexOf["Side"]].Value<char>();
                                    order.OrdType = data[indexOf["OrdType"]].Value<char>();
                                    order.OrdStatus = data[indexOf["OrdStatus"]].Value<char>();
                                    order.AvgPx = data[indexOf["AvgPx"]].Value<ulong>();
                                    order.Price = data[indexOf["Price"]].Value<ulong>();
                                    order.OrderQty = data[indexOf["OrderQty"]].Value<ulong>();
                                    order.OrderQty = data[indexOf["LeavesQty"]].Value<ulong>();
                                    order.CumQty = data[indexOf["CumQty"]].Value<ulong>();
                                    order.CxlQty = data[indexOf["CxlQty"]].Value<ulong>();
                                    order.Volume = data[indexOf["Volume"]].Value<ulong>();
									order.OrderDate = DateTime.ParseExact(data[indexOf["OrderDate"]].Value<string>(), "yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
                                    order.TimeInForce = data[indexOf["TimeInForce"]].Value<char>();
                                    LogStatus(LogStatusType.WARN, 
										"Adding Order to MiniOMS -> ClOrdID = " + order.ClOrdID.ToString() + 
										" OrdStatus = " + order.OrdStatus + "["+order.OrderDate+"]"
									);
                                    try
                                    {
                                        _miniOMS.AddOrder(order);
                                    }
                                    catch (System.ArgumentException)
                                    {
                                    }
                                }

                                // check and request the next page
                                if (ordersLst.Count >= msg["PageSize"].Value<int>())
                                {
                                    LogStatus(LogStatusType.INFO, "Requesting Page " + msg["Page"].Value<int>() + 1);
                                    SendRequestForOpenOrders(webSocketConnection, msg["Page"].Value<int>() + 1);
                                }
                                else
                                {
                                    LogStatus(LogStatusType.INFO, "EOT - no more Order List pages to process.");
									LogStatus(LogStatusType.INFO, "MAX BUY PRICE  = "+_miniOMS.MaxBuyPrice);	
									LogStatus(LogStatusType.INFO, "MIN SELL PRICE = "+_miniOMS.MinSellPrice);
									// notify application that all requestes where replied, 
									// assuming the ORDER_LIST_REQUEST was the last in the StartInitialRequestsAfterLogon
									//_tradingStrategy.OnStart(webSocketConnection);
                                }
                            }
                        }
                        break;

                    case SystemEventType.BALANCE_RESPONSE:
                        if (evt.json != null)
                        {
                            //JObject receivedBalances = evt.json[_brokerId.ToString()].Value<JObject>();
                            foreach (var rb in evt.json[_brokerId.ToString()].Value<JObject>())
                            {
                                try
                                {
                                    this._balances[rb.Key] = rb.Value.Value<ulong>();
                                }
                                catch (System.OverflowException)
                                {
                                    // TODO: find a better solution for this kind of conversion problem 
                                    // {"4": {"BRL_locked": -1, "BTC_locked": 0, "BRL": 48460657965, "BTC": 50544897}, "MsgType": "U3", "ClientID": 90826379, "BalanceReqID": 3}
                                    this._balances[rb.Key] = 0; 
                                }
                            }
							// update the strategy when the balance is updated
							_tradingStrategy.runStrategy(webSocketConnection, _tradingSymbol);
                        }
                        break;
					case SystemEventType.TRADE_HISTORY_RESPONSE:
						{
							JObject msg = evt.json;
							LogStatus(LogStatusType.WARN, 
								"Received " + evt.evtType.ToString() + " : " + "Page=" + msg["Page"].Value<string>()
							);
							/*
							JArray all_trades = msg["TradeHistoryGrp"].Value<JArray>();

							if (all_trades != null && all_trades.Count > 0)
							{
								var columns = msg["Columns"].Value<JArray>();
								Dictionary<string, int> indexOf = new Dictionary<string, int>();
								int index = 0;
								foreach (JToken col in columns)
								{
									indexOf.Add(col.Value<string>(), index++);
								}

								foreach (JArray trade in all_trades)
								{
									_vwapForTradingSym.pushTrade( 
										new ShortPeriodTickBasedVWAP.Trade(
											trade[indexOf["TradeID"]].Value<ulong>(),
											trade[indexOf["Market"]].Value<string>(),
											trade[indexOf["Price"]].Value<ulong>(),
											trade[indexOf["Size"]].Value<ulong>(),
											trade[indexOf["Created"]].Value<string>()
										)
									);
								}

								// check and request the next page
								if (all_trades.Count >= msg["PageSize"].Value<int>())
								{
									LogStatus(LogStatusType.INFO, "TODO: Requesting Page " + msg["Page"].Value<int>() + 1);
									//TODO: create a function to call here and request a new page if requested period in minutes is not satified
								}
								else
								{
									LogStatus(LogStatusType.INFO, "EOT - no more Trade History pages to process.");
								}

								LogStatus(LogStatusType.INFO, String.Format("VWAP = {0}", _vwapForTradingSym.calculateVWAP()));
							}
							*/
						}
						//
						break;

					case SystemEventType.DEPOSIT_REFRESH:
						LogStatus(LogStatusType.WARN, "Receieved " + evt.evtType.ToString() + "\n" + evt.json.ToString());
						_tradingStrategy.OnDepositRefresh(
							evt.json["DepositID"].Value<string>(),
							evt.json["Currency"].Value<string>(), 
							evt.json["Value"].Value<ulong>(),
							evt.json["Status"].Value<int>(),
							evt.json["State"].Value<string>()
						);
						break;
					case SystemEventType.CLOSED:
						// notify the application the connection was broken
						//_tradingStrategy.OnClose(webSocketConnection);
						break;
					// Following events are ignored because inheritted behaviour is sufficient for this prototype
                    case SystemEventType.OPENED:
                    case SystemEventType.ERROR:
                    case SystemEventType.LOGIN_ERROR:
                    case SystemEventType.HEARTBEAT:
                        break;
                    default:
                        LogStatus(LogStatusType.WARN, "Unhandled Broker Notification Event : " + evt.evtType.ToString());
                        break;
                }
            }
            catch (Exception ex)
            {
                LogStatus(LogStatusType.ERROR, 
					" OnBrokerNotification Event Handler Error : " + ex.Message.ToString() + "\n" + ex.StackTrace
				);
            }
        }

        private void StartInitialRequestsAfterLogon(IWebSocketClientConnection connection)
        {
			// 1. cancel all user orders 
            SendRequestToCancelAllOrders(connection); // not necessary if cancel on disconnect is active

            // 2. send the balance request
            JObject balance_request = new JObject();
            balance_request["MsgType"] = "U2";
            balance_request["BalanceReqID"] = connection.NextOutgoingSeqNum();
            balance_request["FingerPrint"] = connection.Device.FingerPrint;
            balance_request["STUNTIP"] = connection.Device.Stuntip;
            connection.SendMessage(balance_request.ToString());

            // 3. send market data request
            JObject marketdata_request = new JObject();
            marketdata_request["MsgType"] = "V";
            marketdata_request["MDReqID"] = connection.NextOutgoingSeqNum();
            marketdata_request["SubscriptionRequestType"] = "1";
            marketdata_request["MarketDepth"] = 0;
            marketdata_request["MDUpdateType"] = "1";
            marketdata_request["MDEntryTypes"] = new JArray("0", "1", "2"); // bid, offer, trade
            marketdata_request["Instruments"] = new JArray(_tradingSymbol);
            marketdata_request["FingerPrint"] = connection.Device.FingerPrint;
            marketdata_request["STUNTIP"] = connection.Device.Stuntip;
            connection.SendMessage(marketdata_request.ToString());

            // 4. send security status request
            JObject securitystatus_request = new JObject();
            securitystatus_request["MsgType"] = "e";
            securitystatus_request["SecurityStatusReqID"] = connection.NextOutgoingSeqNum();
            securitystatus_request["SubscriptionRequestType"] = "1";
            JArray instruments = new JArray();
			instruments.Add("BLINK:BTCBRL");
			instruments.Add("BLINK:BTCUSD");
			instruments.Add("BLINK:BTCVND");
			instruments.Add("BLINK:BTCVEF");
            instruments.Add("BLINK:BTCPKR");
            instruments.Add("BLINK:BTCCLP");
            instruments.Add("BITSTAMP:BTCUSD");
            instruments.Add("ITBIT:BTCUSD");
            instruments.Add("BITFINEX:BTCUSD");
            instruments.Add("BTRADE:BTCUSD");
            instruments.Add("MBT:BTCBRL");
            instruments.Add("KRAKEN:BTCEUR");
            instruments.Add("COINFLOOR:BTCGBP");
            instruments.Add("UOL:USDBRL");
            instruments.Add("UOL:USDBRT");
            instruments.Add("OKCOIN:BTCCNY");
            securitystatus_request["Instruments"] = instruments;
            securitystatus_request["FingerPrint"] = connection.Device.FingerPrint;
            securitystatus_request["STUNTIP"] = connection.Device.Stuntip;
            connection.SendMessage(securitystatus_request.ToString());

			// 5. send the trade history request
			JObject trades_request = new JObject();
			trades_request["MsgType"] = "U32";
			trades_request["TradeHistoryReqID"] = connection.NextOutgoingSeqNum();
			//trades_request["Filter"] = new JArray("Symbol eq 'BTCBRL'"); // not working
			//trades_request["SymbolList"] = new JArray("BTCBRL"); // not working
			trades_request["FingerPrint"] = connection.Device.FingerPrint;
			trades_request["STUNTIP"] = connection.Device.Stuntip;
			connection.SendMessage(trades_request.ToString());

			// 6. send request for all "open" orders
			SendRequestForOpenOrders(connection);
        }

        private void SendRequestForOpenOrders(IWebSocketClientConnection connection, int page = 0)
        {
            JObject orders_list_request = new JObject();
            orders_list_request["MsgType"] = "U4";
            orders_list_request["OrdersReqID"] = connection.NextOutgoingSeqNum();
            orders_list_request["Page"] = page;
            orders_list_request["PageSize"] = 20;
			orders_list_request["Filter"] = new JArray(/*"has_leaves_qty eq 1"*/ "has_cum_qty eq 1");
            connection.SendMessage(orders_list_request.ToString());
        }

        // 

        public string SendOrder(IWebSocketClientConnection connection, 
								string symbol, 
								ulong qty, 
								ulong price, 
								char side, 
								int broker_id, 
								string client_order_id, 
								char order_type = '2', 
								ulong stop_price = 0,
								char execInst = default(char))
        {
            // add pending new order to the OMS
            MiniOMS.Order orderToSend = new MiniOMS.Order();
            orderToSend.Symbol = symbol;
            orderToSend.OrderQty = qty;
            orderToSend.Price = price;
			orderToSend.StopPx = stop_price;
			orderToSend.Side = side;
            orderToSend.ClOrdID = client_order_id;
            orderToSend.OrdType = order_type;
            orderToSend.OrdStatus = 'A'; // PENDING_NEW according to FIX std
            try
            {
                _miniOMS.AddOrder(orderToSend);
            }
            catch (Exception ex)
            {
                LogStatus(LogStatusType.ERROR, 
					"The MiniOMS Rejected the Order : " + 
					orderToSend.ClOrdID + ";" + 
					orderToSend.OrderQty + ";" + 
					orderToSend.Price.ToString() + ";\n" 
					+ ex.Message.ToString() + "\n" 
					+ ex.StackTrace
				);
                return null;
            }

            // send the order to the broker
            JObject new_order_single = new JObject();
            new_order_single["MsgType"] = "D";
            new_order_single["ClOrdID"] = orderToSend.ClOrdID;
            new_order_single["Symbol"] = orderToSend.Symbol;
            new_order_single["Side"] = orderToSend.Side.ToString();
            new_order_single["OrdType"] = orderToSend.OrdType.ToString();
            new_order_single["Price"] = orderToSend.Price;
			if (order_type == '3' || order_type == '4') {
				new_order_single["StopPx"] = stop_price;
			}
            new_order_single["OrderQty"] = orderToSend.OrderQty;
            new_order_single["BrokerID"] = broker_id;
            if (execInst != default(char)) {
                new_order_single["ExecInst"] = execInst.ToString();
            }
            new_order_single["FingerPrint"] = connection.Device.FingerPrint;
            new_order_single["STUNTIP"] = connection.Device.Stuntip;
            connection.SendMessage(new_order_single.ToString());
            return orderToSend.ClOrdID;
        }

        public void SendRequestToCancelAllOrders(IWebSocketClientConnection connection)
        {
            JObject order_cancel_request = new JObject();
            order_cancel_request["MsgType"] = "F";
            order_cancel_request["FingerPrint"] = connection.Device.FingerPrint;
			order_cancel_request["Side"] = "1";
            order_cancel_request["STUNTIP"] = connection.Device.Stuntip;
            connection.SendMessage(order_cancel_request.ToString());
			order_cancel_request["Side"] = "2";
			connection.SendMessage(order_cancel_request.ToString());
        }

        public bool CancelOrderByClOrdID(IWebSocketClientConnection connection, string clOrdID)
        {
            MiniOMS.Order orderToCancel = _miniOMS.GetOrderByClOrdID(clOrdID);
            if (orderToCancel != null)
            {
				if (orderToCancel.OrdStatus == OrdStatus.NEW || orderToCancel.OrdStatus == OrdStatus.PARTIALLY_FILLED)
                {
                    orderToCancel.OrdStatus = OrdStatus.PENDING_CANCEL;
                    JObject order_cancel_request = new JObject();
                    order_cancel_request["MsgType"] = "F";
                    order_cancel_request["ClOrdID"] = clOrdID;
                    order_cancel_request["FingerPrint"] = connection.Device.FingerPrint;
                    order_cancel_request["STUNTIP"] = connection.Device.Stuntip;
                    connection.SendMessage(order_cancel_request.ToString());
                    return true;
                }
            }
            return false;
        }

		private MiniOMS.Order ProcessExecutionReport(JObject msg)
        {
            Debug.Assert(msg["MsgType"].Value<string>() == "8");
			// find the order in the OMS
			MiniOMS.Order order = _miniOMS.GetOrderByClOrdID(msg["ClOrdID"].Value<string>());
            if (order != null)
            {
				// update the order in the OMS
				order.OrderID = msg.GetValue("OrderID").Type != JTokenType.Null ? msg["OrderID"].Value<ulong>() : 0;
				order.OrdStatus = msg["OrdStatus"].Value<char>();
				order.CumQty = msg["CumQty"].Value<ulong>();
				order.CxlQty = msg["CxlQty"].Value<ulong>();
				order.LastPx = msg["LastPx"].Value<ulong>();
				order.LastShares = msg["LastShares"].Value<ulong>();
				order.LeavesQty = msg["LeavesQty"].Value<ulong>();
				order.Price = msg["Price"].Value<ulong>();
				order.AvgPx = msg["AvgPx"].Value<ulong>();

				// update the traded amount of the trading symbol (this is not considering amounts traded by other sessions)
				if ( order.OrdStatus == OrdStatus.FILLED || order.OrdStatus == OrdStatus.PARTIALLY_FILLED )
				{
					if (order.Side == OrderSide.SELL) 
					{
						this._soldAmount += order.LastShares;
						LogStatus(LogStatusType.INFO, "Updating the Sold Amount = " + this._soldAmount);
					}
				}

				switch (order.OrdStatus)
                {
                    // -- If the order is still "alive", keep the order in the MiniOMS
                    case OrdStatus.NEW:
                    case OrdStatus.PARTIALLY_FILLED:
                    case OrdStatus.STOPPED:  // comming soon / work in progress
                        break;
                    // -- If the order is "dead", remove the order from the MiniOMS
                    case OrdStatus.CANCELED:
                    case OrdStatus.REJECTED:
                    case OrdStatus.FILLED:
                        //bool retVal = _miniOMS.RemoveOrderByClOrdID(msg["ClOrdID"].Value<string>());
                        //Debug.Assert(retVal);
                        break;

                    default:
                        LogStatus(LogStatusType.ERROR, 
							"Unexpected ExecutionReport.OrdStatus : " + 
							msg["OrdStatus"].Value<char>()
						);
                        break;
                }
            }
            else
            {
                LogStatus(LogStatusType.ERROR, "Order not found by ClOrdID = " + msg["ClOrdID"].Value<string>());
            }
			return order;
        }

        private static void show_usage(string program_name)
        {
            Console.WriteLine("Blinktrade client websocket C# sample");
            Console.WriteLine("\nusage:\n\t" + 
				program_name + 
				" <URL> <BROKER-ID> <SYMBOL> <BUY|SELL|BOTH> <DEFAULT|FIXED|FLOAT|STOP> <MAX-BTC-TRADE-SIZE> " +
				" <BUY-TARGET-PRICE-OR-STOP> <SELL-TARGET-PRICE-OR-PEGGED_PRICE_OFFSET-OR-STOPLIMIT> "+
				" <USERNAME> <PASSWORD> [<SECOND-FACTOR>]");
            Console.WriteLine("\nexample:\n\t" + 
				program_name + 
				" \"wss://api.testnet.blinktrade.com/trade/\" " +
				"5 BTCUSD BOTH DEFAULT 0.1 1900.01 2000.99 user abc12345");
        }

        static public void Main(string[] args)
        {
			if (args.Length < 10 || args.Length > 11)
            {
                show_usage(Process.GetCurrentProcess().ProcessName);
                return;
            }

            string url = args[0];
            int broker_id;
            try
            {
                broker_id = Int32.Parse(args[1]);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                show_usage(Process.GetCurrentProcess().ProcessName);
                return;
            }

            string symbol = args[2];

            char side;
            switch (args[3].ToUpper())
            {
                case "BUY":
                    side = OrderSide.BUY;
                    break;
                case "SELL":
                    side = OrderSide.SELL;
                    break;
                case "BOTH":
                    side = default(char);
                    break;
                default:
                    show_usage(Process.GetCurrentProcess().ProcessName);
                    return;
            }

			// ** temporary workaround to support market pegged sell order strategy without plugins **
			TradingStrategy.PriceType priceType;
			switch (args[4].ToUpper())
			{
				case "DEFAULT":
				case "FIXED":
					priceType = TradingStrategy.PriceType.FIXED;
					break;
				case "PEGGED":
				case "FLOAT":
					if ( side == OrderSide.SELL )
						priceType = TradingStrategy.PriceType.PEGGED;
					else
						throw new ArgumentException("PEGGED is currently supported only for SELL");
					break;
				case "STOP":
					priceType = TradingStrategy.PriceType.STOP;	
					if (side == default(char)) {
						throw new ArgumentException("STOP must define BUY or SELL");
					}
					break;
					
				default:
					show_usage(Process.GetCurrentProcess().ProcessName);
					return;
			}

			ulong maxTradeSize;
            try
            {
                maxTradeSize = (ulong)(Double.Parse(args[5]) * 1e8);
				if ( maxTradeSize < 10000)
					throw new ArgumentException("Invalid Trade Size, must be at least 10,000 satoshis");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                show_usage(Process.GetCurrentProcess().ProcessName);
                return;
            }

			// ***
            ulong buyTargetPrice;
            try
            {
                buyTargetPrice = (ulong)(Double.Parse(args[6]) * 1e8);
                if (buyTargetPrice < 0)
                    throw new ArgumentException("Invalid Buy Target Price");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                show_usage(Process.GetCurrentProcess().ProcessName);
                return;
            }

            ulong sellTargetPrice;
            try
            {
                sellTargetPrice = (ulong)(Double.Parse(args[7]) * 1e8);
                
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                show_usage(Process.GetCurrentProcess().ProcessName);
                return;
            }

            try
            {
                if ((side == OrderSide.BUY || side == default(char)) && buyTargetPrice == 0)
                    throw new ArgumentException("Invalid BUY Target Price");

				if (priceType != TradingStrategy.PriceType.STOP) 
				{
					if ((side == OrderSide.SELL || side == default(char)) && sellTargetPrice == 0)
	                    throw new ArgumentException("Invalid SELL Target Price");

					if (side == default(char) && buyTargetPrice >= sellTargetPrice)
	                    throw new ArgumentException("Invalid SELL and BUY Price RANGE");
				}
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                show_usage(Process.GetCurrentProcess().ProcessName);
                return;
            }

            string user = args[8];
            string password = args[9];
            string second_factor = args.Length == 11 ? args[10] : null;

			try 
			{
				// instantiate the tradeclient object to handle the trading stuff
				TradingStrategy strategy = null;
				if (priceType == TradingStrategy.PriceType.STOP) {
					// ** this is a workaround
					ulong stoppx = buyTargetPrice;
					ulong limit = sellTargetPrice;
					// validation of stoppx and limit should happen at server side
					strategy = new TradingStrategy ( side, maxTradeSize, stoppx, limit);
				}
				else
					strategy = new TradingStrategy (maxTradeSize, buyTargetPrice, sellTargetPrice, side, priceType);

				// instantiate the protocol engine object to handle the blinktrade messaging stuff
				WebSocketClientProtocolEngine protocolEngine = new WebSocketClientProtocolEngine();

				SimpleTradeClient tradeclient = new SimpleTradeClient (broker_id, symbol, strategy, protocolEngine);

				// tradeclient must subscribe to receive the callback events from the protocol engine
				protocolEngine.SystemEvent += tradeclient.OnBrokerNotification;
				protocolEngine.LogStatusEvent += LogStatus;
				strategy.LogStatusEvent += LogStatus;


				// workaround (on windows working only in DEBUG) to trap the console application exit 
				// and dump the last state of the Market Data Order Book(s), MiniOMS and Security Status
				#if  ( __MonoCS__ || DEBUG )
					System.AppDomain appDom = System.AppDomain.CurrentDomain;
					appDom.ProcessExit += new EventHandler(tradeclient.OnApplicationExit);
					#if __MonoCS__
						Thread signal_thread = new Thread(UnixSignalTrap);
						signal_thread.Start();
					#else
						var consoleHandler = new HandlerRoutine(OnConsoleCtrlCheck); // hold handler to not get GC'd
						SetConsoleCtrlHandler(consoleHandler, true);
					#endif
				#endif

				// objects to encapsulate and provide user account credentials
				UserAccountCredentials userAccount = new UserAccountCredentials (broker_id, user, password, second_factor);

				while (!_userRequestExit) 
				{
					try 
					{
						LogStatus (LogStatusType.WARN, "Please Wait...");

						// gather and provide the user device data (local ip, external ip etc)
						UserDevice userDevice = new UserDevice();

						// start the connection task to handle the Websocket connectivity and initiate the whole process
						Task task = WebSocketClientConnection.Start(url, userAccount, userDevice, protocolEngine);
						task.Wait(); // aguardar até a Task finalizar

						if (!_userRequestExit) 
						{
							tradeclient.ResetData(); // must reset tradeclient to refresh whole data after new connection
							LogStatus (LogStatusType.WARN, "Trying to reconnect in 5 seconds...");
							Task.Delay(TimeSpan.FromSeconds(5)).Wait();
						}
					}
					catch(System.Net.WebException ex) 
					{
						LogStatus (LogStatusType.ERROR, ex.Message + '\n' + ex.StackTrace);
						Task.Delay(TimeSpan.FromSeconds(5)).Wait();
						continue;
					}
				}
			}
			catch (Exception ex) 
			{
				LogStatus (LogStatusType.ERROR, ex.Message + '\n' + ex.StackTrace);
				return;
			}

            /*
			#if DEBUG
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
            #endif
            */
        }

        public static void LogStatus(LogStatusType logtype, string message)
        {
            lock (_consoleLock)
            {
                switch (logtype)
                {
                    case LogStatusType.MSG_IN:
                        Console.ForegroundColor = ConsoleColor.Green;
						Console.Write("[<---][{0}]", Util.ConvertToUnixTimestamp(DateTime.Now));
                        break;
                    case LogStatusType.MSG_OUT:
                        Console.ForegroundColor = ConsoleColor.Gray;
						Console.Write("[--->][{0}]", Util.ConvertToUnixTimestamp(DateTime.Now));
                        break;
                    case LogStatusType.WARN:
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.Write("[WARN] ");
                        break;
                    case LogStatusType.ERROR:
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Write("[ERROR] ");
                        break;
                    default:
                        Console.Write("[INFO] ");
                        Console.ForegroundColor = ConsoleColor.White;
                        break;
                }
                Console.WriteLine(message);
                Console.ResetColor();
            }
        }

        private void OnApplicationExit(object sender, EventArgs e)
        {
			// workaround to cancel all orders when application is dying (TODO: extend for more connections in the future)
			this._tradingStrategy.Enabled = false; // disable strategy
			Thread.Sleep(100);
			if (this._protocolEngine.GetConnections().Count > 0) {
				this.SendRequestToCancelAllOrders (this._protocolEngine.GetConnections()[0]);
				Thread.Sleep(100);
				this.SendRequestToCancelAllOrders (this._protocolEngine.GetConnections()[0]);
			}

            Console.WriteLine("Dumping In Memory Order Books");
            foreach (KeyValuePair<string, OrderBook> kvp in _allOrderBooks)
            {
                Console.WriteLine(kvp.Value.ToString());
            }

            Console.WriteLine("Dumping In Memory MiniOMS State");
            Console.WriteLine(_miniOMS.ToString());

            Console.WriteLine("Dumping In Memory Last Security Status received Info");
            foreach (KeyValuePair<string, SecurityStatus> kvp in _securityStatusEntries)
            {
                Console.WriteLine(kvp.Key.ToString());
                Console.WriteLine(kvp.Value.ToString());
            }


			Console.WriteLine("Program Terminated.");
			Console.Out.Flush();
			Console.Error.Flush();
        }

		#if !__MonoCS__
		[DllImport("Kernel32")]
        public static extern bool SetConsoleCtrlHandler(HandlerRoutine Handler, bool Add);
		#endif

        // A delegate type to be used as the handler routine 
        // for SetConsoleCtrlHandler.
        public delegate bool HandlerRoutine(CtrlTypes CtrlType);

        // An enumerated type for the control messages
        // sent to the handler routine.
        public enum CtrlTypes
        {
            CTRL_C_EVENT = 0,
            CTRL_BREAK_EVENT,
            CTRL_CLOSE_EVENT,
            CTRL_LOGOFF_EVENT = 5,
            CTRL_SHUTDOWN_EVENT
        }

        private static bool OnConsoleCtrlCheck(CtrlTypes ctrlType)
        {
			lock (_consoleLock)
            {
				Debug.Assert(!_userRequestExit);
				switch (ctrlType)
                {
                    case CtrlTypes.CTRL_C_EVENT:
                        _userRequestExit = true;
                        Console.WriteLine("CTRL+C received, shutting down");
                        break;

                    case CtrlTypes.CTRL_BREAK_EVENT:
						_userRequestExit = true;
                        Console.WriteLine("CTRL+BREAK received, shutting down");
                        break;

                    case CtrlTypes.CTRL_CLOSE_EVENT:
                        _userRequestExit = true;
                        Console.WriteLine("Program being closed, shutting down");
                        break;

                    case CtrlTypes.CTRL_LOGOFF_EVENT:
                    case CtrlTypes.CTRL_SHUTDOWN_EVENT:
						_userRequestExit = true;
                        Console.WriteLine("User is logging off!, shutting down");
                        break;
                }

				if (_userRequestExit) {
                    Environment.Exit (0);
				}
            }

            return true;
        }

		#if __MonoCS__
		public static void UnixSignalTrap()
		{
			// Catch SIGINT and SIGTERM
			UnixSignal[] signals = new UnixSignal [] {
				new UnixSignal (Mono.Unix.Native.Signum.SIGINT),
				new UnixSignal (Mono.Unix.Native.Signum.SIGTERM),
			};
			while (true) {
				// Wait for a signal to be delivered
				int index = UnixSignal.WaitAny (signals, -1);
		        // invoke the clean-up routine before exiting
				Mono.Unix.Native.Signum signal = signals[index].Signum;
				OnConsoleCtrlCheck(signal == Mono.Unix.Native.Signum.SIGINT ? CtrlTypes.CTRL_C_EVENT : CtrlTypes.CTRL_CLOSE_EVENT);
				break;
			}
		}
		#endif

        // object to synchronize access to console output
        private static object _consoleLock = new object();
    }
}
