using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NestConsole.Settings;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace NestConsole.GoogleServices
{
    public class OAuthService : IOAuthService
    {
        private const string AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
        //private const string AuthorizationEndpoint = "https://home.nest.com/login/oauth2";

        private readonly ILogger<OAuthService> _logger;
        private readonly GoogleOAuthClientSettings _oAuthSettings;
        private readonly NestDeviceAccessSettings _nestDeviceAccessSettings;

        public OAuthService(ILogger<OAuthService> logger, IOptions<GoogleOAuthClientSettings> oAuthSettings, IOptions<NestDeviceAccessSettings> nestDeviceAccessSettings)
        {
            _logger = logger;
            _oAuthSettings = oAuthSettings.Value;
            _nestDeviceAccessSettings = nestDeviceAccessSettings.Value;
        }

        public bool SignIn()
        {
            // Check for existing credentials or whether to overwrite existing
            if (string.IsNullOrWhiteSpace(_oAuthSettings.ClientId) || string.IsNullOrWhiteSpace(_oAuthSettings.ClientSecret))
            {
                Console.WriteLine("OAuth Client settings not found in config file. Unable to start");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(_oAuthSettings.AccessToken))
            {
                Console.WriteLine("Existing credentials found. Do you want to use these?");
                if (Utils.Console.GetUserBoolResponse())
                {
                    // Need to test they work - Refresh token
                    return true;
                }
                else
                {
                    Console.WriteLine("Updating Sign In Credentials");
                }
            }
            else
            {
                Console.WriteLine("No existing Credentials found. Sign in required");
            }

            Console.WriteLine("+-----------------------+");
            Console.WriteLine("|  Sign in with Google  |");
            Console.WriteLine("+-----------------------+");
            Console.WriteLine("");
            Console.WriteLine("Press any key to sign in...");
            Console.ReadKey();

            DoOAuthAsync().Wait();

            return true;
        }

        private async Task DoOAuthAsync()
        {
            // Generates state and PKCE values.
            string state = Utils.Encryption.GenerateRandomDataBase64url(32);
            string codeVerifier = Utils.Encryption.GenerateRandomDataBase64url(32);
            string codeChallenge = Utils.Encryption.Base64UrlEncodeNoPadding(Utils.Encryption.Sha256Ascii(codeVerifier));
            const string codeChallengeMethod = "S256";

            // Creates a redirect URI using an available port on the loopback address.
            string redirectUri = $"http://{IPAddress.Loopback}:{GetRandomUnusedPort()}/";
            _logger.LogInformation("redirect URI: " + redirectUri);

            // Creates an HttpListener to listen for requests on that redirect URI.
            var http = new HttpListener();
            http.Prefixes.Add(redirectUri);
            _logger.LogInformation("Listening..");
            http.Start();

            // Creates the OAuth 2.0 authorization request.
            var authorizationRequestSb = new StringBuilder();
            authorizationRequestSb.Append(_oAuthSettings.AuthUri); //AuthorizationEndpoint);
            authorizationRequestSb.Append($"?client_id={_oAuthSettings.ClientId}");
            authorizationRequestSb.Append($"&redirect_uri={Uri.EscapeDataString(redirectUri)}");
            authorizationRequestSb.Append($"&response_type=code");
            //authorizationRequestSb.Append($"&scope=https://www.googleapis.com/auth/sdm.service");
            authorizationRequestSb.Append($"&scope={string.Join("+", _nestDeviceAccessSettings.RequiredScopes)}");
            authorizationRequestSb.Append($"&state={state}");
            authorizationRequestSb.Append($"&code_challenge={codeChallenge}");
            authorizationRequestSb.Append($"&code_challenge_method={codeChallengeMethod}");

            // Opens request in the browser.
            Process.Start(new ProcessStartInfo(authorizationRequestSb.ToString()) { UseShellExecute = true });

            // Waits for the OAuth authorization response.
            var context = await http.GetContextAsync();

            // Sends an HTTP response to the browser.
            Utils.Console.BringConsoleToFront();
            var response = context.Response;
            string responseString = "<html><head><meta http-equiv='refresh' content='10;url=https://google.com'></head><body>Please return to the app.</body></html>";
            byte[] buffer = Encoding.UTF8.GetBytes(responseString);
            response.ContentLength64 = buffer.Length;
            var responseOutput = response.OutputStream;
            await responseOutput.WriteAsync(buffer, 0, buffer.Length);
            responseOutput.Close();
            http.Stop();
            _logger.LogInformation("HTTP server stopped.");

            // Checks for errors.
            string error = context.Request.QueryString.Get("error");
            if (error is object)
            {
                _logger.LogInformation($"OAuth authorization error: {error}.");
                return;
            }
            if (context.Request.QueryString.Get("code") is null
                || context.Request.QueryString.Get("state") is null)
            {
                _logger.LogInformation($"Malformed authorization response. {context.Request.QueryString}");
                return;
            }

            // extracts the code
            var code = context.Request.QueryString.Get("code");
            var incomingState = context.Request.QueryString.Get("state");

            // Compares the receieved state to the expected value, to ensure that
            // this app made the request which resulted in authorization.
            if (incomingState != state)
            {
                _logger.LogInformation($"Received request with invalid state ({incomingState})");
                return;
            }
            _logger.LogInformation("Authorization code: " + code);

            // Starts the code exchange at the Token Endpoint.
            await ExchangeCodeForTokensAsync(code, codeVerifier, redirectUri);
        }

        private async Task ExchangeCodeForTokensAsync(string code, string codeVerifier, string redirectUri)
        {
            _logger.LogInformation("Exchanging code for tokens...");

            // builds the  request
            //string tokenRequestUri = "https://www.googleapis.com/oauth2/v4/token";
            string tokenRequestUri = _oAuthSettings.TokenUri; //"https://oauth2.googleapis.com/token";
            StringBuilder tokenRequestBodySb = new StringBuilder();
            tokenRequestBodySb.Append($"code={code}");
            tokenRequestBodySb.Append($"&redirect_uri={Uri.EscapeDataString(redirectUri)}");
            tokenRequestBodySb.Append($"&client_id={_oAuthSettings.ClientId}");
            tokenRequestBodySb.Append($"&client_secret={ _oAuthSettings.ClientSecret}");
            tokenRequestBodySb.Append($"&code_verifier={codeVerifier}");
            tokenRequestBodySb.Append($"&scope=&grant_type=authorization_code");

            // sends the request
            HttpWebRequest tokenRequest = (HttpWebRequest)WebRequest.Create(tokenRequestUri);
            tokenRequest.Method = "POST";
            tokenRequest.ContentType = "application/x-www-form-urlencoded";
            tokenRequest.Accept = "Accept=text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
            byte[] tokenRequestBodyBytes = Encoding.ASCII.GetBytes(tokenRequestBodySb.ToString());
            tokenRequest.ContentLength = tokenRequestBodyBytes.Length;
            using (Stream requestStream = tokenRequest.GetRequestStream())
            {
                await requestStream.WriteAsync(tokenRequestBodyBytes, 0, tokenRequestBodyBytes.Length);
            }

            try
            {
                // gets the response
                WebResponse tokenResponse = await tokenRequest.GetResponseAsync();
                using (StreamReader reader = new StreamReader(tokenResponse.GetResponseStream()))
                {
                    // reads response body
                    string responseText = await reader.ReadToEndAsync();
                    Console.WriteLine(responseText);

                    // converts to dictionary
                    Dictionary<string, string> tokenEndpointDecoded = JsonConvert.DeserializeObject<Dictionary<string, string>>(responseText);

                    string accessToken = tokenEndpointDecoded["access_token"];

                    // Save token to app.config
                    Utils.Configuration.AddOrUpdateAppSettings("GoogleOAuthClient/AccessToken", accessToken);
                    //await RequestUserInfoAsync(accessToken);
                }
            }
            catch (WebException ex)
            {
                if (ex.Status == WebExceptionStatus.ProtocolError)
                {
                    var response = ex.Response as HttpWebResponse;
                    if (response != null)
                    {
                        _logger.LogInformation("HTTP: " + response.StatusCode);
                        using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                        {
                            // reads response body
                            string responseText = await reader.ReadToEndAsync();
                            _logger.LogInformation(responseText);
                        }
                    }
                }
            }
        }

        private async Task RequestUserInfoAsync(string accessToken)
        {
            _logger.LogInformation("Making API Call to Userinfo...");

            // builds the  request
            string userinfoRequestUri = "https://www.googleapis.com/oauth2/v3/userinfo";

            // sends the request
            HttpWebRequest userinfoRequest = (HttpWebRequest)WebRequest.Create(userinfoRequestUri);
            userinfoRequest.Method = "GET";
            userinfoRequest.Headers.Add(string.Format("Authorization: Bearer {0}", accessToken));
            userinfoRequest.ContentType = "application/x-www-form-urlencoded";
            userinfoRequest.Accept = "Accept=text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";

            // gets the response
            WebResponse userinfoResponse = await userinfoRequest.GetResponseAsync();
            using (StreamReader userinfoResponseReader = new StreamReader(userinfoResponse.GetResponseStream()))
            {
                // reads response body
                string userinfoResponseText = await userinfoResponseReader.ReadToEndAsync();
                _logger.LogInformation(userinfoResponseText);
            }
        }

        private void SaveAccessToken(string accessToken)
        {
            Utils.Configuration.AddOrUpdateAppSettings("GoogleOAuthClient/AccessToken", accessToken);
        }

        // ref http://stackoverflow.com/a/3978040
        private int GetRandomUnusedPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}