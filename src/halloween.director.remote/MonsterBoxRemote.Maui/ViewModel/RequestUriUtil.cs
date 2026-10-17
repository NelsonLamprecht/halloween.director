using System;
using System.Collections.Generic;
using System.Text;

namespace MonsterBoxRemote.Maui.ViewModel
{
    internal static class RequestUriUtil
    {
        public static string GetUriWithQueryString(string requestUri,
            Dictionary<string, string> queryStringParams)
        {
            bool startingQuestionMarkAdded = false;
            var sb = new StringBuilder();
            sb.Append(requestUri);
            foreach (var parameter in queryStringParams)
            {
                if (parameter.Value == null)
                {
                    continue;
                }

                sb.Append(startingQuestionMarkAdded ? '&' : '?');
                sb.Append(Uri.EscapeDataString(parameter.Key));
                sb.Append('=');
                sb.Append(Uri.EscapeDataString(parameter.Value));
                startingQuestionMarkAdded = true;
            }
            return sb.ToString();
        }
    }
}
