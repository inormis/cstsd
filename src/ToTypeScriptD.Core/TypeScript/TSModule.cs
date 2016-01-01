﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace ToTypeScriptD.Core.TypeScript
{
    public class TSModule
    {
        public string NameSpace { get; set; }
        public ICollection<TSInterface> Interfaces { get; set; } = new List<TSInterface>();
        public ICollection<TSClass> Clases { get; set; } = new List<TSClass>(); 
        public override string ToString()
        {
            var interfaces = string.Join("\r\n\r\n", Interfaces.Select(i => i.ToString()));
            var classes = string.Join("\r\n", Clases.Select(c => c.ToString()));

            return $@"module {NameSpace}" +
                   @"{" +
                   $@"{interfaces.Indent(TSFormattingConfig.IndentSpaces)}" +
                   @"" +
                   $@"{classes.Indent(TSFormattingConfig.IndentSpaces)}" +
                   @"}";
        }
    }
}
