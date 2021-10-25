using System;
using System.Configuration;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace NestConsole
{
    public static class Utils
    {
        public static class Configuration
        {
            public static void AddOrUpdateAppSettings(string key, string value)
            {
                try
                {
                    var configFile = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None);
                    var settings = configFile.AppSettings.Settings;
                    if (settings[key] == null)
                    {
                        settings.Add(key, value);
                    }
                    else
                    {
                        settings[key].Value = value;
                    }
                    configFile.Save(ConfigurationSaveMode.Modified);
                    ConfigurationManager.RefreshSection(configFile.AppSettings.SectionInformation.Name);
                }
                catch (ConfigurationErrorsException)
                {
                    System.Console.WriteLine("Error writing app settings");
                }
            }
        }

        public static class Console
        {
            [DllImport("kernel32.dll", ExactSpelling = true)]
            private static extern IntPtr GetConsoleWindow();

            [DllImport("user32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool SetForegroundWindow(IntPtr hWnd);

            public static void BringConsoleToFront()
            {
                SetForegroundWindow(GetConsoleWindow());
            }
            public static bool GetUserBoolResponse()
            {
                var answer = GetUserResponse(new string[] { "Y", "N" });
                switch (answer)
                {
                    case "Y":
                        return true;
                    case "N":
                    default:
                        return false;
                }
            }

            public static string GetUserResponse(string[] possibleAnswers)
            {
                if (possibleAnswers == null || possibleAnswers.Length <= 1)
                {
                    return possibleAnswers[0] ?? "";
                }

                string answer = "";
                bool answered = false;
                do
                {
                    StringBuilder sb = new StringBuilder();
                    sb.Append("Please answer ");
                    for (int i = 0; i < possibleAnswers.Length + 1 ; i++)
                    {
                        if (i > 0)
                        {
                            sb.Append($"/");
                        }
                        sb.Append(possibleAnswers[i]);
                    }
                    System.Console.WriteLine(":");
                    answer = System.Console.ReadLine();

                    string[] upperPossibleAnswers = Array.ConvertAll(possibleAnswers, a => a.ToUpper());
                    if (upperPossibleAnswers.Contains(answer.ToUpper()))
                    {
                        answered = true;
                    }
                    else
                    {
                        System.Console.WriteLine("Invalid answer, please try again");
                    }

                } while (!answered);
                return answer;
            }
        }

        public static class Encryption
        {
            /// <summary>
            /// Returns URI-safe data with a given input length.
            /// </summary>
            /// <param name="length">Input length (nb. output will be longer)</param>
            /// <returns></returns>
            public static string GenerateRandomDataBase64url(uint length)
            {
                RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();
                byte[] bytes = new byte[length];
                rng.GetBytes(bytes);
                return Base64UrlEncodeNoPadding(bytes);
            }

            /// <summary>
            /// Returns the SHA256 hash of the input string, which is assumed to be ASCII.
            /// </summary>
            public static byte[] Sha256Ascii(string text)
            {
                byte[] bytes = Encoding.ASCII.GetBytes(text);
                using (SHA256Managed sha256 = new SHA256Managed())
                {
                    return sha256.ComputeHash(bytes);
                }
            }

            /// <summary>
            /// Base64url no-padding encodes the given input buffer.
            /// </summary>
            /// <param name="buffer"></param>
            /// <returns></returns>
            public static string Base64UrlEncodeNoPadding(byte[] buffer)
            {
                string base64 = Convert.ToBase64String(buffer);

                // Converts base64 to base64url.
                base64 = base64.Replace("+", "-");
                base64 = base64.Replace("/", "_");
                // Strips padding.
                base64 = base64.Replace("=", "");

                return base64;
            }
        }
    }
}