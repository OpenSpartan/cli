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
        static XboxAuthenticationManager manager = new();
        static HaloAuthenticationClient haloAuthClient = new();
        static ConfigurationReader clientConfigReader = new();
        static async Task<int> Main(string[] args)
        {
            var rootCommand = new RootCommand();

            var clientIdOption = new Option<string>(
                name: "--client-id",
                description: "Registered client ID.",
                getDefaultValue: () => String.Empty);

            var clientSecretOption = new Option<string>(
                name: "--client-secret",
                description: "Registered client secret.",
                getDefaultValue: () => String.Empty);

            var redirectUrlOption = new Option<string>(
                name: "--redirect-url",
                description: "Redirect URL for the registered client.",
                getDefaultValue: () => String.Empty);

            var authTokenOption = new Option<string>(
                name: "--auth-token",
                description: "Authentication token to get the data.",
                getDefaultValue: () => String.Empty);

            var refreshTokenOption = new Option<string>(
                name: "--refresh-token",
                description: "Refresh token used to obtain a new token.",
                getDefaultValue: () => String.Empty);

            var outputOption = new Option<string>(
                name: "--output",
                description: "Output location to store the output of the command.",
                getDefaultValue: () => String.Empty)
            {
                IsRequired = true
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

            var authCommand = new Command("auth", "Authenticate the user with Xbox Live and Halo services.")
            {
                clientIdOption,
                clientSecretOption,
                redirectUrlOption
            };

            var authUrlCommand = new Command("authurl", "Get the authentication URL that is required to complete the authentication process.")
            {
                clientIdOption,
                redirectUrlOption
            };

            var refreshCommand = new Command("authrefresh", "Refreshes the currently assigned token to a new one.");

            var getManifestCommand = new Command("get-manifest", "Gets the game manifest for the specified build.")
            {
                buildIdOption,
                spartanTokenOption,
                outputFormatOption,
                outputOption
            };

            rootCommand.AddCommand(authCommand);
            rootCommand.AddCommand(authUrlCommand);
            rootCommand.AddCommand(refreshCommand);
            rootCommand.AddCommand(getManifestCommand);

            authCommand.SetHandler((clientId, clientSecret, redirectUrl) =>
            {
                var success = AuthHelper.RequestNewToken(manager, redirectUrl, clientId, clientSecret, AuthHelper.GetConfigurationFilePath(ConfigurationFileType.AuthTokens));
                if (success)
                {
                    Console.WriteLine("Authentication process got a token.");
                }
                else
                {
                    Console.WriteLine("Authentication process could not get a token.");
                }
            }, clientIdOption, clientSecretOption, redirectUrlOption);

            authUrlCommand.SetHandler((clientId, redirectUrl) =>
            {
                var url = manager.GenerateAuthUrl(clientId, redirectUrl);
                Console.WriteLine("You should be requesting the code from the following URL, if you don't have it yet:");
                Console.WriteLine(url);
            }, clientIdOption, redirectUrlOption);

            refreshCommand.SetHandler(async () =>
            {
                if (AuthHelper.AuthTokensExist())
                {
                    var currentOAuthToken = clientConfigReader.ReadConfiguration<OAuthToken>(AuthHelper.GetConfigurationFilePath(ConfigurationFileType.AuthTokens));
                    var currentClientConfig = clientConfigReader.ReadConfiguration<ClientConfiguration>(AuthHelper.GetConfigurationFilePath(ConfigurationFileType.Client));

                    var success = await AuthHelper.RefreshToken(manager, currentOAuthToken.RefreshToken, currentClientConfig.RedirectUrl, currentClientConfig.ClientId, currentClientConfig.ClientSecret, AuthHelper.GetConfigurationFilePath(ConfigurationFileType.AuthTokens));
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