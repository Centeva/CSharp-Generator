using System;
using System.Collections.Generic;
using System.Text;

namespace common
{
    public class Options
    {
        public string Source { get; set; }
        public string Destination { get; set; }
        public List<string> Dlls { get; set; }
        public List<string> CollectionTypeNames { get; set; }
        public bool Verbose { get; set; } = false;
    }
}
