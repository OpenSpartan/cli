using Grunt.Authentication;
using Grunt.Models;
using Grunt.Util;
using OpenSpartan.CLI.Models;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace OpenSpartan.CLI.Core
{
    internal class AuthHelper
    {
        static XboxAuthenticationManager authManager = new();
        static HaloAuthenticationClient haloAuthClient = new();
        static ConfigurationReader clientConfigReader = new();

        internal static async Task<bool> RefreshToken()
        {
            var currentOAuthToken = clientConfigReader.ReadConfiguration<OAuthToken>(GetConfigurationFilePath(ConfigurationFileType.AuthTokens));
            var currentClientConfig = clientConfigReader.ReadConfiguration<ClientConfiguration>(GetConfigurationFilePath(ConfigurationFileType.Client));

            if (currentOAuthToken != null && currentClientConfig != null)
            {
                currentOAuthToken = await authManager.RefreshOAuthToken(currentClientConfig.ClientId, currentOAuthToken.RefreshToken, currentClientConfig.RedirectUrl, currentClientConfig.ClientSecret);
                return ProcessOAuthToken(currentOAuthToken, currentClientConfig.RedirectUrl, currentClientConfig.ClientId, currentClientConfig.ClientSecret, GetConfigurationFilePath(ConfigurationFileType.AuthTokens));
            }
            else
            {
                Console.WriteLine("Could not read the auth tokens or the client configuration when trying to refresh tokens.");
            }

            return false;
        }

        internal static HaloTokens? GetHaloTokens()
        {
            XboxTicket? ticket = new XboxTicket();
            XboxTicket? haloTicket = new XboxTicket();
            XboxTicket? extendedTicket = new XboxTicket();
            HaloTokens? haloTokens = null;
            var spartanToken = new SpartanToken();

            var currentOAuthToken = clientConfigReader.ReadConfiguration<OAuthToken>(GetConfigurationFilePath(ConfigurationFileType.AuthTokens));
            var currentClientConfig = clientConfigReader.ReadConfiguration<ClientConfiguration>(GetConfigurationFilePath(ConfigurationFileType.Client));

            var isOAuthSuccessful = false;

            Task.Run(async () =>
            {
                ticket = await authManager.RequestUserToken(currentOAuthToken.AccessToken);
                if (ticket == null)
                {
                    // There was a failure to obtain the user token, so likely we need to refresh.
                    currentOAuthToken = await authManager.RefreshOAuthToken(currentClientConfig.ClientId, currentOAuthToken.RefreshToken, currentClientConfig.RedirectUrl, currentClientConfig.ClientSecret);
                    if (currentOAuthToken == null)
                    {
                        Console.WriteLine("Could not get the token even with the refresh token. Let's try getting a new one.");
                        var success = RequestNewToken(currentClientConfig.RedirectUrl, currentClientConfig.ClientId, currentClientConfig.ClientSecret, GetConfigurationFilePath(ConfigurationFileType.AuthTokens));
                        if (success)
                        {
                            currentOAuthToken = clientConfigReader.ReadConfiguration<OAuthToken>(GetConfigurationFilePath(ConfigurationFileType.AuthTokens));
                            if (currentOAuthToken != null && !string.IsNullOrEmpty(currentOAuthToken.AccessToken))
                            {
                                isOAuthSuccessful = true;
                            }
                        }
                        else
                        {
                            Console.WriteLine("Could not request a new token. You might need to try running `openspartan set-auth` to authenticate.");
                        }
                    }

                    if (currentOAuthToken != null && isOAuthSuccessful)
                    {
                        ticket = await authManager.RequestUserToken(currentOAuthToken.AccessToken);
                    }
                }
            }).GetAwaiter().GetResult();

            if (ticket != null && !string.IsNullOrEmpty(ticket.Token))
            {
                Task.Run(async () =>
                {
                    haloTicket = await authManager.RequestXstsToken(ticket.Token);
                }).GetAwaiter().GetResult();

                if (haloTicket != null && haloTicket.Token != null)
                {
                    Task.Run(async () =>
                    {
                        extendedTicket = await authManager.RequestXstsToken(ticket.Token, false);
                    }).GetAwaiter().GetResult();

                    Task.Run(async () =>
                    {
                        spartanToken = await haloAuthClient.GetSpartanToken(haloTicket.Token);
                        if (spartanToken != null && !string.IsNullOrEmpty(spartanToken.Token))
                        {
                            haloTokens = new HaloTokens();
                            haloTokens.SpartanToken = spartanToken.Token;
                            haloTokens.Xuid = extendedTicket.DisplayClaims.Xui[0].Xid;
                        }
                        else
                        {
                            Console.WriteLine("Could not obtain a Halo API token.");
                        }
                    }).GetAwaiter().GetResult();
                }
                else
                {
                    Console.WriteLine("Could not obtain a Halo API ticket.");
                }
            }
            else
            {
                Console.WriteLine("Could not obtain an Xbox Live ticket.");
            }

            return haloTokens;
        }

        internal static bool RequestNewToken(string redirectUrl, string clientId, string clientSecret, string location)
        {
            Console.WriteLine("Enter your authorization code:");
            var code = Console.ReadLine();
            var success = false;

            Task.Run(async () =>
            {
                var currentOAuthToken = await authManager.RequestOAuthToken(clientId, code, redirectUrl, clientSecret);
                success = ProcessOAuthToken(currentOAuthToken, redirectUrl, clientId, clientSecret, location);
            }).GetAwaiter().GetResult();

            return success;
        }

        internal static string GetAuthUrl(string clientId, string redirectUrl)
        {
            return authManager.GenerateAuthUrl(clientId, redirectUrl);
        }

        private static bool ProcessOAuthToken(OAuthToken currentOAuthToken, string redirectUrl, string clientId, string clientSecret, string location)
        {
            if (currentOAuthToken != null)
            {
                var storeTokenResult = StoreData(currentOAuthToken, location, OutputFormat.JSON, true);

                var clientConfiguration = new ClientConfiguration { ClientId = clientId, ClientSecret = clientSecret, RedirectUrl = redirectUrl };

                if (storeTokenResult)
                {
                    Console.WriteLine("Stored the tokens locally.");
                    var storeClientConfigurationResult = StoreData(clientConfiguration, GetConfigurationFilePath(ConfigurationFileType.Client), OutputFormat.JSON, true);
                    if (storeClientConfigurationResult)
                    {
                        Console.WriteLine("Stored client configuration locally.");
                        return true;
                    }
                    else
                    {
                        Console.WriteLine("Client configuration could not be stored locally. You will run into issues when it comes time to refresh tokens. Verify that you can write data to disk.");
                    }
                }
                else
                {
                    Console.WriteLine("There was an issue storing tokens locally. You should attempt to re-run this command again. Verify that you can write files to disk.");
                }
            }
            else
            {
                Console.WriteLine("No token was obtained. There is no valid token to be used right now. Verify that you're using correct information.");
            }

            return false;
        }

        internal static string GetConfigurationFilePath(ConfigurationFileType fileType)
        {
            string currentCliPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);

            switch (fileType)
            {
                case ConfigurationFileType.AuthTokens:
                    {
                        return Path.Combine(currentCliPath, GlobalConstants.AUTH_TOKEN_FILE_NAME);
                    }
                case ConfigurationFileType.Client:
                    {
                        return Path.Combine(currentCliPath, GlobalConstants.CLIENT_CONFIG_FILE_NAME);
                    }
                default:
                    {
                        return currentCliPath;
                    }
            }
            
        }

        internal static bool AuthTokensExist()
        {
            if (File.Exists(GetConfigurationFilePath(ConfigurationFileType.AuthTokens)))
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        private static bool StoreData<T>(T data, string path, OutputFormat format = OutputFormat.JSON, bool useGruntNamingPolicy = false)
        {
            string output = string.Empty;

            if (format == OutputFormat.JSON)
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    // TODO: This is stop gap until Grunt moves to using the native JSON serializer.
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };

                if (useGruntNamingPolicy)
                {
                    options.PropertyNamingPolicy = new GruntJsonNamingPolicy();
                }

                output = JsonSerializer.Serialize(data, options);
            }

            try
            {
                File.WriteAllText(path, output);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error has occured while storing the data. Error details: {ex.Message}");
                return false;
            }
        }
    }
}
