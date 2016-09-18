﻿using System;
using System.Collections.Generic;

namespace ToTypeScriptD.Core
{
    public class TsNamespace
    {
        public string Name { get; set; }
        public IList<TsType> TypeDeclarations { get; set; } = new List<TsType>();

        public override string ToString()
        {
            return Name;
        }
    }
}
