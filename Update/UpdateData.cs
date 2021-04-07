using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Update
{
    public class UpdateData
    {
        public string Id;
        public List<string> Dependencies = new List<string>();
        public List<string> CultureList = new List<string>();
        public string InfoUrl;
    }
}
