using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace PLCLib
{
    class FoundPLC
    {
        public IPEndPoint ipEndPoint;
        public byte[] MacAddress = new byte[6];
        public string Name = "";
        public string Description = "";
    }
}
