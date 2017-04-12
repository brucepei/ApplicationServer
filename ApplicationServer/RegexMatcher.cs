using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ExpectNet
{
    public class RegexMatcher : IMatcher
    {
        private System.Text.RegularExpressions.Regex regex;
        private string preMatchedString;
        private string matchedString;
        private string postMatchedString;
        public string PreMatchedString
        {
            get
            {
                return preMatchedString;
            }
        }
        public string MatchedString
        {
            get
            {
                return matchedString;
            }
        }
        public string PostMatchedString
        {
            get
            {
                return postMatchedString;
            }
        }

        public RegexMatcher(System.Text.RegularExpressions.Regex regex)
        {
            if (regex == null)
            {
                throw new ArgumentNullException("regex");
            }
            this.regex = regex;
        }

        public bool IsMatch(string text)
        {
            var result = false;
            var match = regex.Match(text);
            if (match.Success)
            {
                preMatchedString = text.Substring(0, match.Index);
                postMatchedString = text.Substring(match.Index + match.Groups[0].Value.Length);
                matchedString = match.Groups[0].Value;
                result = true;
            }
            return result;
        }
    }
}
