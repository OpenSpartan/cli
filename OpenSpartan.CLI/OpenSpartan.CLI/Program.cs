using Grunt.Authentication;
using System.CommandLine;

namespace OpenSpartan.CLI
{
    internal class Program
    {
        static XboxAuthenticationManager manager = new();
        static HaloAuthenticationClient haloAuthClient = new();
        static async Task<int> Main(string[] args)
        {
            var rootCommand = new RootCommand();

            var codeOption = new Option<string>(
                name: "--code",
                description: "Initial authorization code to perform the token exchange.",
                getDefaultValue: () => String.Empty);

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

            var outFolderOption = new Option<string>(
                name: "--out",
                description: "Output location to store the output of the command.",
                getDefaultValue: () => String.Empty);

            var buildIdOption = new Option<string>(
                name: "--build-id",
                description: "Build for which data is being referenced.",
                getDefaultValue: () => String.Empty);

            var projectIdOption = new Option<string>(
                name: "--project-id",
                description: "Unique identifier of the project for which stats need to be obtained.",
                getDefaultValue: () => String.Empty);

            var secretOption = new Option<string>(
                name: "--secret",
                description: "Value for the secret.",
                getDefaultValue: () => String.Empty);

            var publicKeyOption = new Option<string>(
                name: "--public-key",
                description: "Value for the public key used to encrypt the secret.",
                getDefaultValue: () => String.Empty);

            var startCommand = new Command("start", "Authenticate the user with the Xbox and Halo services.")
            {
                codeOption,
                clientIdOption,
                clientSecretOption,
                redirectUrlOption
            };

            var getUrlCommand = new Command("geturl", "Get the authentication URL which the user should go to for auth code production.")
            {
                clientIdOption,
                redirectUrlOption
            };

            var refreshCommand = new Command("refresh", "Refreshes the currently assigned token to a new one.")
            {
                clientIdOption,
                clientSecretOption,
                redirectUrlOption,
                refreshTokenOption
            };

            rootCommand.AddCommand(startCommand);
            rootCommand.AddCommand(getUrlCommand);
            rootCommand.AddCommand(refreshCommand);

            startCommand.SetHandler(async (code, clientId, clientSecret, redirectUrl) =>
            {
                var authResultString = await Core.AuthHelper.PerformAuthentication(manager, code, clientId, clientSecret, redirectUrl);
                if (!string.IsNullOrEmpty(authResultString))
                {
                    Console.WriteLine(authResultString);
                }
            }, codeOption, clientIdOption, clientSecretOption, redirectUrlOption);

            getUrlCommand.SetHandler((clientId, redirectUrl) =>
            {
                var url = manager.GenerateAuthUrl(clientId, redirectUrl);
                Console.WriteLine("You should be requesting the code from the following URL, if you don't have it yet:");
                Console.WriteLine(url);
            }, clientIdOption, redirectUrlOption);

            refreshCommand.SetHandler(async (clientId, clientSecret, redirectUrl, refreshToken) =>
            {
                var authResultString = await Core.AuthHelper.PerformTokenRefresh(manager, refreshToken, clientId, clientSecret, redirectUrl);
                if (!string.IsNullOrEmpty(authResultString))
                {
                    Console.WriteLine(authResultString);
                }
            }, clientIdOption, clientSecretOption, redirectUrlOption, refreshTokenOption);

            return await rootCommand.InvokeAsync(args);
        }
    }
}