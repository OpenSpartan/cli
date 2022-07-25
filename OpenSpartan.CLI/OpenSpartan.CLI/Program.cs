using OpenSpartan.CLI.Core;
using OpenSpartan.CLI.Models;
using OpenSpartan.Grunt.Core;
using OpenSpartan.Grunt.Models.HaloInfinite;
using System.CommandLine;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace OpenSpartan.CLI
{
    internal class Program
    {
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

            var clearanceOption = new Option<string>(
                name: "--clearance",
                description: "Clearance (flight) GUID.",
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
                clearanceOption,
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
                    var success = await AuthHelper.RefreshToken();
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

            getManifestCommand.SetHandler(async (buildId, spartanToken, outputFormat, output) =>
            {
                Manifest gameManifest;
                HaloInfiniteClient client = null;

                if (!string.IsNullOrEmpty(spartanToken))
                {
                    // User decided to use the Spartan token instead of the native auth.
                    client = new(spartanToken, string.Empty);
                }
                else
                {
                    // User is trying to use native auth.
                    if (AuthHelper.AuthTokensExist())
                    {
                        var haloTokens = AuthHelper.GetHaloTokens();
                        if (haloTokens != null && !string.IsNullOrEmpty(haloTokens.SpartanToken))
                        {
                            client = new HaloInfiniteClient(haloTokens.SpartanToken, haloTokens.Xuid);
                        }
                        else
                        {
                            Console.WriteLine("You don't have any Halo tokens ready. Make sure that your credentials are correct.");
                            Environment.Exit(0);
                        }
                    }
                    else
                    {
                        Console.WriteLine("No authentication tokens stored locally. Make sure that you run `openspartan set-auth` with your client credentials to set local tokens. Alternatively, you can re-run this command by passing your Spartan token directly.");
                        Environment.Exit(0);
                    }
                }

                var manifest = (await client.HIUGCDiscoveryGetManifestByBuild(buildId)).Result;
                if (manifest != null)
                {
                    var outputData = string.Empty;
                    if (outputFormat == OutputFormat.JSON)
                    {
                        var options = new JsonSerializerOptions
                        {
                            WriteIndented = true,
                            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                        };
                        outputData = JsonSerializer.Serialize(manifest, options);
                    }
                    else if (outputFormat == OutputFormat.Markdown)
                    {
                        StringBuilder compositeTables = new StringBuilder();
                        compositeTables.Append(manifest.EngineGameVariantLinks.ToMarkdownTable());
                        compositeTables.AppendLine();
                        compositeTables.Append(manifest.MapLinks.ToMarkdownTable());
                        compositeTables.AppendLine();
                        compositeTables.Append(manifest.UgcGameVariantLinks.ToMarkdownTable());

                        outputData = compositeTables.ToString();
                    }

                    if (!string.IsNullOrEmpty(output))
                    {
                        System.IO.File.WriteAllText(output, outputData);
                    }

                    Console.WriteLine(outputData);
                }
                else
                {
                    Console.WriteLine($"Could not successfully obtain a game manifest for build {buildId}");
                }
            }, buildIdOption, spartanTokenOption, outputFormatOption, outputOption);

            return await rootCommand.InvokeAsync(args);
        }
    }
}