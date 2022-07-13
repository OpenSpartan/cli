using Grunt.Authentication;
using Grunt.Models;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace OpenSpartan.CLI.Core
{
    internal class AuthHelper
    {
        internal static async Task<string> PerformAuthentication(XboxAuthenticationManager authManager, string code, string clientId, string clientSecret, string redirectUrl)
        {
            OAuthToken currentOAuthToken = await authManager.RequestOAuthToken(clientId, code, redirectUrl, clientSecret);

            if (currentOAuthToken != null)
            {
                var options = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
                return JsonSerializer.Serialize(currentOAuthToken, options);
            }

            return string.Empty;
        }

        internal static async Task<string> PerformTokenRefresh(XboxAuthenticationManager authManager,  string refreshToken, string clientId, string clientSecret, string redirectUrl)
        {
            OAuthToken currentOAuthToken = await authManager.RefreshOAuthToken(clientId, refreshToken, redirectUrl, clientSecret);

            if (currentOAuthToken != null)
            {
                var options = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
                return JsonSerializer.Serialize(currentOAuthToken, options);
            }

            return string.Empty;
        }
    }
}
