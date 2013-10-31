﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using ToTypeScriptD.Core;
using ToTypeScriptD.Core.TypeWriters;

namespace ToTypeScriptD
{
    public class Render
    {
        public static bool AllAssemblies(IEnumerable<string> assemblyPaths, bool includeSpecialTypes, TextWriter w, ITypeNotFoundErrorHandler typeNotFoundErrorHandler)
        {
            if (assemblyPaths == null)
                assemblyPaths = new string[0];

            var typeCollection = new TypeCollection();

            var wroteAnyTypes = WriteSpecialTypes(includeSpecialTypes, w);
            wroteAnyTypes |= WriteFiles(assemblyPaths, w, typeNotFoundErrorHandler, typeCollection);
            return  wroteAnyTypes;
        }

        private static bool WriteFiles(IEnumerable<string> assemblyPaths, TextWriter w, ITypeNotFoundErrorHandler typeNotFoundErrorHandler, TypeCollection typeCollection)
        {
            var filesAlreadyProcessed = new HashSet<string>(new IgnoreCaseStringEqualityComparer());
            if (!assemblyPaths.Any())
                return false;

            assemblyPaths.Each(file =>
            {
                if (filesAlreadyProcessed.Contains(file))
                    return;

                filesAlreadyProcessed.Add(file);
                var x = ToTypeScriptD.Render.FullAssembly(file, typeNotFoundErrorHandler, typeCollection);

                w.NewLine();
                w.WriteLine(x);
            });

            return true;
        }

        private static bool WriteSpecialTypes(bool includeSpecialTypes, TextWriter w)
        {
            if (!includeSpecialTypes)
                return false;

            w.NewLine();
            w.WriteLine(Resources.ToTypeScriptDSpecialTypes_d);
            w.NewLine();
            w.NewLine();
            return true;
        }

        public static string FullAssembly(string assemblyPath, ITypeNotFoundErrorHandler typeNotFoundErrorHandler, TypeCollection typeCollection)
        {
            var assembly = Mono.Cecil.AssemblyDefinition.ReadAssembly(assemblyPath);

            typeCollection.AddAssembly(assembly);

            var typeWriterGenerator = new TypeWriterCollector(typeNotFoundErrorHandler);
            foreach (var item in assembly.MainModule.Types)
            {
                typeWriterGenerator.Collect(item, typeCollection);
            }

            return typeCollection.Render();
        }

    }
}
