using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ExpectNet
{
    public class StringContainsMatcher : IMatcher
    {
        private string query;
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
        public StringContainsMatcher(string query)
        {
            if (query == null)
            {
                throw new ArgumentNullException("query");
            }
            this.query = query;
        }
        public bool IsMatch(string text)
        {
            var result = false;
            var offset = text.IndexOf(query);
            if (offset > -1)
            {
                preMatchedString = text.Substring(0, offset);
                matchedString = query;
                postMatchedString = text.Substring(offset + query.Length);
                result = true;
            }
            return result;
        }
    }
}
