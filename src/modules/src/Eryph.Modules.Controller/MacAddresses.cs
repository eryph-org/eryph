using System;
using System.IO.Hashing;
using System.Text;
using System.Text.RegularExpressions;

namespace Eryph.Modules.Controller
{
    public class MacAddresses
    {
        public static string FormatMacAddress(string input)
        {
            const string regex = "(.{2})(.{2})(.{2})(.{2})(.{2})(.{2})";
            const string replace = "$1:$2:$3:$4:$5:$6";

            return Regex.Replace(input, regex, replace).ToLowerInvariant();

        }

        public static string GenerateMacAddress(string valueSource)
        {

            string? result = null;

            var arrayData = Encoding.ASCII.GetBytes(valueSource);
            var arrayResult = Crc32.Hash(arrayData);
            foreach (var t in arrayResult)
            {
                var temp = Convert.ToString(t, 16);
                if (temp.Length == 1)
                    temp = $"0{temp}";
                result += temp;
            }

            return "d2ab" + result;
        }
    }
}
