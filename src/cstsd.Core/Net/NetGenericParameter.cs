using System.Collections.Generic;

namespace cstsd.Core.Net
{
    public class NetGenericParameter
    {
        public string Name { get; set; }

        public ICollection<NetType> ParameterConstraints { get; set; } = new List<NetType>();

        public override string ToString()
        {
            return Name;
        }
    }
}