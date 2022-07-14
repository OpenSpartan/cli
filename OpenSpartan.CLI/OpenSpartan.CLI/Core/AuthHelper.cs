using Grunt.Authentication;
using Grunt.Models;
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
        internal static async Task<bool> RefreshToken(string refreshToken, string redirectUrl, string clientId, string clientSecret, string location)
        {
            OAuthToken currentOAuthToken = await authManager.RefreshOAuthToken(clientId, refreshToken, redirectUrl, clientSecret);
            return ProcessOAuthToken(currentOAuthToken, redirectUrl, clientId, clientSecret, location);
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
