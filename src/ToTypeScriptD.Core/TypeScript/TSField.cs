using System;

namespace ToTypeScriptD.Core.TypeScript
{
    public class TSField
    {
        public string Name { get; set; }
        public TSType Type { get; set; }
        public bool IsStatic { get; set; } = false;
        public override string ToString()
        {
            throw new NotImplementedException();
        }
    }
}