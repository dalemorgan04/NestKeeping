namespace NestConsole.Settings
{
    public class GoogleOAuthClientSettings
    {
        public string ClientId { get; set; }
        public string ClientSecret { get; set; }
        public string AuthUri { get; set; }
        public string TokenUri { get; set; }
        public string AccessToken { get; set; }
    }
}