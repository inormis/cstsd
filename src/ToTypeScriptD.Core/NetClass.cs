using System;
using System.Collections.Generic;

namespace ToTypeScriptD.Core
{
    public class NetClass : NetInterface
    {
        public ICollection<NetType> NestedClasses { get; set; } = new List<NetType>(); 

        public override string ToString()
        {
            throw new NotImplementedException();
        }
    }
}