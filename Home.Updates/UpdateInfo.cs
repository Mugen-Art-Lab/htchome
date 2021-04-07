using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Home.Updates
{
    public class UpdateInfo
    {
        public string Cid; //component id
        public string Id { get; set; }
        public List<string> Dependencies = new List<string>();
        public List<string> CultureList = new List<string>();
        public string InfoUrl;
        public string Title { get; set; }
        public string Description { get; set; }
        public string Package;
        public string Size { get; set; }
    }
}
