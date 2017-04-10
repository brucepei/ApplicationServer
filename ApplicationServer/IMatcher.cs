using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ExpectNet
{
    public interface IMatcher
    {
        bool IsMatch(string text);
    }
}
