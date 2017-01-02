using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Cache;
using System.IO;

namespace Blinktrade
{
    public class Util
    {
        public static double ConvertToUnixTimestamp(DateTime date)
        {
            DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            TimeSpan diff = date.ToUniversalTime() - origin;
            return Math.Floor(diff.TotalSeconds);
        }

        public static string GetLocalIPAddress()
        {
            string result = Dns.GetHostEntry(Dns.GetHostName()).AddressList.Where(o => o.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork).First().ToString();
            return result;
        }

        public static string GetExternalIpAddress()
        {
            // this is just a workaround and might have a better solution (i.e query the network router instead of trust in a website)
            WebRequest request = WebRequest.Create("http://checkip.amazonaws.com/");
            request.CachePolicy = new RequestCachePolicy(RequestCacheLevel.CacheIfAvailable);
            HttpWebResponse httpWebResponse = (HttpWebResponse)request.GetResponse();
            if (httpWebResponse.StatusCode == HttpStatusCode.OK)
            {
                WebResponse response = httpWebResponse;
                Stream dataStream = response.GetResponseStream();
                StreamReader reader = new StreamReader(dataStream);
                string responseFromServer = reader.ReadLine();
                reader.Close();
                response.Close();
                return responseFromServer;
            }
            else
            {
                httpWebResponse.Close();
                return string.Empty;
            }
        }
    }
}
