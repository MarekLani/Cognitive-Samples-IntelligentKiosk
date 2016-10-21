using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Security.Authentication.Web.Core;
using Windows.Security.Credentials;

namespace IntelligentKioskSample
{
    public class User
    {
        static string  tenant = "malams.onmicrosoft.com";
        static string  authority = "https://login.microsoftonline.com/" + tenant;
        static string resource = "https://graph.windows.net";
        static string clientId = "86002696-b38a-4aad-9cc3-73b9e76e1ea9";

        public static async Task<string> Login()
        {

            WebAccountProvider wap = await WebAuthenticationCoreManager.FindAccountProviderAsync("https://login.microsoft.com", authority);
            string clientId = "86002696-b38a-4aad-9cc3-73b9e76e1ea9";

         

            //Getting redirect url
            //string URI = string.Format("ms-appx-web://Microsoft.AAD.BrokerPlugIn/{0}", WebAuthenticationBroker.GetCurrentApplicationCallbackUri().Host.ToUpper());
            //resource = URI;

            WebTokenRequest wtr = new
            WebTokenRequest(wap, string.Empty, clientId);

            wtr.Properties.Add("resource", resource);
            WebTokenRequestResult wtrr = await WebAuthenticationCoreManager.RequestTokenAsync(wtr);

            if (wtrr.ResponseStatus == WebTokenRequestStatus.Success)
            {

                var accessToken = wtrr.ResponseData[0].Token;
                var account = wtrr.ResponseData[0].WebAccount;
                return account.UserName;
            }
            else
            {
                return "";
            }
        }

        public static async Task<bool> LogOut()
        {

            WebAccountProvider wap = await WebAuthenticationCoreManager.FindAccountProviderAsync("https://login.microsoft.com", authority);

            //Getting redirect url
            //string URI = string.Format("ms-appx-web://Microsoft.AAD.BrokerPlugIn/{0}", WebAuthenticationBroker.GetCurrentApplicationCallbackUri().Host.ToUpper());
            //resource = URI;

            WebTokenRequest wtr = new
            WebTokenRequest(wap, string.Empty, clientId);

            wtr.Properties.Add("resource", resource);
            WebTokenRequestResult wtrr = await WebAuthenticationCoreManager.RequestTokenAsync(wtr);

            if (wtrr.ResponseStatus == WebTokenRequestStatus.Success)

            {
                var account = wtrr.ResponseData[0].WebAccount;
                await account.SignOutAsync();
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
