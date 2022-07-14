using System.Text.Json;
using System.Text.RegularExpressions;

namespace OpenSpartan.CLI.Models
{
    internal class GruntJsonNamingPolicy : JsonNamingPolicy
    {
        public override string ConvertName(string name) =>
            string.Join("_", Regex.Split(name, @"(?<!^)(?=[A-Z](?![A-Z]|$))")).ToLower();
    }
}
