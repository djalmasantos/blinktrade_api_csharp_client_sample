using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blinktrade
{
    public class TestRequestDispatcher
    {
        public static readonly TimeSpan _testRequestDelay = TimeSpan.FromMilliseconds(10000);
		private bool _enableTestRequest = true;

		public bool EnableTestRequest
		{
			get
			{
				return _enableTestRequest;
			}
			set
			{
				_enableTestRequest = value;
			}
		}

		protected static async Task TestRequest(IWebSocketClientConnection connection)
        {
            // Simple keep-alive mechanism using TestRequest/Heartbeat
            long nextExpectedCounter = 0;
            bool disconnect = false;

            do
            {
                await Task.Delay(_testRequestDelay);

                if (!connection.IsConnected)
                    break;

                if (!connection.EnableTestRequest)
                    continue;

                if (nextExpectedCounter > connection.receivedMessageCounter)
                {
                    if (!disconnect)
                    {
                        connection.SendTestRequest();
                        disconnect = true;
                    }
                    else
                    {
                        // second chance before disconnecting
                        await Task.Delay(_testRequestDelay);

                        if (nextExpectedCounter > connection.receivedMessageCounter)
                        {
                            connection.OnLogEvent(LogStatusType.ERROR, "Websocket connection not responding");
                            connection.Shutdown();
                            break;
                        }
                        disconnect = false;
                    }
                }
                else
                {
                    disconnect = false;
                }

                // update expectation for next iteration
                nextExpectedCounter = connection.receivedMessageCounter + 1;

            } while (connection.IsConnected);
        }
    }
}
