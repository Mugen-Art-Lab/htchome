using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Home.Base
{
    public class ExtrasInfo
    {
        public string Cid { get; set; }
        public string Title { get; set; }
        public string Version { get; set; }
        public string Developer { get; set; }
        public bool Removable { get; set; }
        public List<string> Files { get; set; }
    }
}
