using System.Collections.Generic;

namespace cstsd.Core.Ts
{
    public class TsGenericParameter
    {
        public string Name { get; set; }

        public ICollection<TsType> ParameterConstraints { get; set; } = new List<TsType>();

        public override string ToString()
        {
            return Name;
        }
    }
}