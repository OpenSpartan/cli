using Grunt.Authentication;
using Grunt.Models;
using Grunt.Util;
using OpenSpartan.CLI.Core;
using OpenSpartan.CLI.Models;
using System.CommandLine;
using System.Reflection;

namespace OpenSpartan.CLI
{
    internal class Program
    {
        static ConfigurationReader clientConfigReader = new();
        static async Task<int> Main(string[] args)
        {
            var rootCommand = new RootCommand();

            var clientIdOption = new Option<string>(
                name: "--client-id",
                description: "Registered client ID.")
            {
                IsRequired = true
            };

            var clientSecretOption = new Option<string>(
                name: "--client-secret",
                description: "Registered client secret.")
            {
                IsRequired = true
            };

            var redirectUrlOption = new Option<string>(
                name: "--redirect-url",
                description: "Redirect URL for the registered client.")
            {
                IsRequired = true
            };

            var outputOption = new Option<string>(
                name: "--output",
                description: "Output location to store the output of the command.",
                getDefaultValue: () => String.Empty)
            {
                IsRequired = false
            };

            var outputFormatOption = new Option<OutputFormat>(
                name: "--output-format",
                description: "Format for the output data.",
                getDefaultValue: () => OutputFormat.JSON)
            {
                IsRequired = true
            };

            var buildIdOption = new Option<string>(
                name: "--build-id",
                description: "Build for which data is being referenced.")
            {
                IsRequired = true
            };

            var spartanTokenOption = new Option<string>(
                name: "--spartan-token",
                description: "Existing Spartan token. Using this option means you're not using the built-in authentication.",
                getDefaultValue: () => String.Empty)
            {
                IsRequired = false
            };

            var setAuthCommand = new Command("set-auth", "Authenticate the user with Xbox Live and Halo services.")
            {
                clientIdOption,
                clientSecretOption,
                redirectUrlOption
            };

            var getAuthUrlCommand = new Command("get-auth-url", "Get the authentication URL that is required to complete the authentication process.")
            {
                clientIdOption,
                redirectUrlOption
            };

            var refreshAuthCommand = new Command("refresh-auth", "Refreshes the currently assigned token to a new one.");

            var getManifestCommand = new Command("get-manifest", "Gets the game manifest for the specified build.")
            {
                buildIdOption,
                spartanTokenOption,
                outputFormatOption,
                outputOption
            };

            rootCommand.AddCommand(setAuthCommand);
            rootCommand.AddCommand(getAuthUrlCommand);
            rootCommand.AddCommand(refreshAuthCommand);
            rootCommand.AddCommand(getManifestCommand);

            setAuthCommand.SetHandler((clientId, clientSecret, redirectUrl) =>
            {
                var success = AuthHelper.RequestNewToken(redirectUrl, clientId, clientSecret, AuthHelper.GetConfigurationFilePath(ConfigurationFileType.AuthTokens));
                if (success)
                {
                    Console.WriteLine("Authentication process got a token.");
                }
                else
                {
                    Console.WriteLine("Authentication process could not get a token.");
                }
            }, clientIdOption, clientSecretOption, redirectUrlOption);

            getAuthUrlCommand.SetHandler((clientId, redirectUrl) =>
            {
                Console.WriteLine("You should be requesting the code from the following URL, if you don't have it yet:");
                Console.WriteLine(AuthHelper.GetAuthUrl(clientId, redirectUrl));
            }, clientIdOption, redirectUrlOption);

            refreshAuthCommand.SetHandler(async () =>
            {
                if (AuthHelper.AuthTokensExist())
                {
                    var currentOAuthToken = clientConfigReader.ReadConfiguration<OAuthToken>(AuthHelper.GetConfigurationFilePath(ConfigurationFileType.AuthTokens));
                    var currentClientConfig = clientConfigReader.ReadConfiguration<ClientConfiguration>(AuthHelper.GetConfigurationFilePath(ConfigurationFileType.Client));

                    var success = await AuthHelper.RefreshToken(currentOAuthToken.RefreshToken, currentClientConfig.RedirectUrl, currentClientConfig.ClientId, currentClientConfig.ClientSecret, AuthHelper.GetConfigurationFilePath(ConfigurationFileType.AuthTokens));
                    if (success)
                    {
                        Console.WriteLine("Authentication process refreshed a token.");
                    }
                    else
                    {
                        Console.WriteLine("Authentication process could not refresh a token.");
                    }
                }
                else
                {
                    Console.WriteLine("Local tokens do not exist, therefore there is nothing to refresh.");
                }
            });

            //if (System.IO.File.Exists("tokens.json"))
            //{
            //    Console.WriteLine("Trying to use local tokens...");
            //    // If a local token file exists, load the file.
            //    currentOAuthToken = clientConfigReader.ReadConfiguration<OAuthToken>("tokens.json");
            //}
            //else
            //{
            //    currentOAuthToken = RequestNewToken(url, manager, clientConfig);
            //}

            return await rootCommand.InvokeAsync(args);
        }
    }
}