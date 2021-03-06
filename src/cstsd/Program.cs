﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using cstsd.Lexical.Core;
using cstsd.TypeScript;
using Fclp;
using Fclp.Internals.Extensions;
using Newtonsoft.Json;

namespace cstsd
{
    class Program
    {
        //TODO: location of output file
        static void Main(string[] args)
        {
            var p = new FluentCommandLineParser();

            p.SetupHelp("?", "help")
                .Callback(helpText =>
                {
                    Console.WriteLine("Welcome to cstsd. Use the following command line args:");
                    Console.WriteLine(helpText);
                    Console.WriteLine("");
                    Console.WriteLine("");
                });
            
            var filePath = "cstsd.json";
            p.Setup<string>('c', "config")
                .Callback(s => filePath = s)
                .WithDescription("This is the path cstsd config json file or directory where cstsd.json is located.")
            ;

            var isDirFile = FileHelpers.IsDirFile(filePath);

            if (isDirFile == null)
            {
                Console.WriteLine($"Could not find '{Path.GetFullPath(filePath)}'...");
                return;
            }
            
            if (!isDirFile.Value)
                filePath = Path.Combine(filePath, "cstsd.json");

            WriterConfig cstsdConfig;
            if (File.Exists(filePath))
            {
                cstsdConfig = JsonConvert.DeserializeObject<WriterConfig>(File.ReadAllText(filePath));
            }
            else
            {
                Console.WriteLine($"Could not find '{filePath}'...");
                return;
            }

            var cstsdDir = new FileInfo(filePath).Directory?.FullName ?? "";


            //render cs controllers
            Console.WriteLine("Scanning controllers for CS export");
            if (cstsdConfig.ToCsControllerTasks != null)
            {
                foreach (var controllerTask in cstsdConfig.ToCsControllerTasks)
                {
                    Console.WriteLine($"Scanning controller: {controllerTask.SourceFile}");

                    var fileName = Path.GetFileNameWithoutExtension(controllerTask.SourceFile);

                    var outputFile = Path.IsPathRooted(controllerTask.OutputDirectory) == false
                        ? Path.Combine(cstsdDir, controllerTask.OutputDirectory, fileName + ".cs")
                        : Path.Combine(controllerTask.OutputDirectory, fileName + ".cs");

                    var nameSpace = string.IsNullOrWhiteSpace(controllerTask.Namespace)
                        ? cstsdConfig.DefaultCsControllerNamespace
                        : controllerTask.Namespace;

                    CheckCreateDir(outputFile);
                    using (TextWriter tw = new StreamWriter(outputFile, false))
                    {
                        RenderCs.FromControllerRoslyn(controllerTask.SourceFile, nameSpace, cstsdConfig, tw);
                        tw.Flush();
                    }
                }
            }

            //render ts controllers
            Console.WriteLine("Scanning controllers for TS export");
            if (cstsdConfig.ToTsControllerTasks != null)
            {
                foreach (var controllerTask in cstsdConfig.ToTsControllerTasks)
                {
                    Console.WriteLine($"Scanning controller: {controllerTask.SourceFile}");

                    var fileName = Path.GetFileNameWithoutExtension(controllerTask.SourceFile);

                    var outputFile = Path.IsPathRooted(controllerTask.OutputDirectory) == false
                        ? Path.Combine(cstsdDir, controllerTask.OutputDirectory, fileName + ".ts")
                        : Path.Combine(controllerTask.OutputDirectory, fileName + ".ts");

                    var nameSpace = string.IsNullOrWhiteSpace(controllerTask.Namespace)
                        ? cstsdConfig.DefaultTsControllerNamespace
                        : controllerTask.Namespace;

                    CheckCreateDir(outputFile);
                    using (TextWriter tw = new StreamWriter(outputFile, false))
                    {
                        RenderTypescript.FromControllerRoslyn(controllerTask.SourceFile, nameSpace, cstsdConfig,
                            tw);
                        tw.Flush();
                    }
                }
            }

            //render poco objects
            Console.WriteLine("Scanning poco objects for TS export");
            if (cstsdConfig.ToTsPocoObjectTasks != null)
            {
                //render poco's from one dll into one .d.ts file
                foreach (var pocoTask in cstsdConfig.ToTsPocoObjectTasks)
                {
                    var sourceFiles = new List<string>();
                    
                    pocoTask.SourceDirectories.ForEach(sd =>
                    {
                        Console.WriteLine($"Scanning poco dir: {sd}");
                        if(pocoTask.Recursive)
                            FileHelpers.ScanRecursive(sd, sourceFiles.Add);
                        else
                            FileHelpers.ScanStandard(sd, sourceFiles.Add);
                    });
                    
                    var outputFileName = pocoTask.OutputName; //make the outputfilename the na

                    var outputFile = Path.IsPathRooted(pocoTask.OutputDirectory) == false
                        ? Path.Combine(cstsdDir, pocoTask.OutputDirectory, outputFileName + ".d.ts")
                        : Path.Combine(pocoTask.OutputDirectory, outputFileName + ".d.ts");

                    var nameSpace = string.IsNullOrWhiteSpace(pocoTask.Namespace)
                        ? cstsdConfig.DefaultTsPocoNamespace
                        : pocoTask.Namespace;
                    
                    CheckCreateDir(outputFile);
                    using (TextWriter tw = new StreamWriter(outputFile, false))
                    {
                        RenderTypescript.FromPocoRoslyn(sourceFiles, nameSpace, cstsdConfig, tw);
                        tw.Flush();
                    }
                }
            }

            //render enum tasks
            Console.WriteLine("Scanning enum objects for TS export");
            if (cstsdConfig.ToTsEnumTasks != null)
            {
                //render enum's from one dll into one .d.ts file
                foreach (var enumTask in cstsdConfig.ToTsEnumTasks)
                {
                    var sourceFiles = new List<string>();

                    enumTask.SourceDirectories.ForEach(sd =>
                    {
                        Console.WriteLine($"Scanning enum dir: {sd}");
                        if (enumTask.Recursive)
                            FileHelpers.ScanRecursive(sd, sourceFiles.Add);
                        else
                            FileHelpers.ScanStandard(sd, sourceFiles.Add);
                    });

                    var outputFileName = enumTask.OutputName; //make the outputfilename the na

                    var outputFile = Path.IsPathRooted(enumTask.OutputDirectory) == false
                        ? Path.Combine(cstsdDir, enumTask.OutputDirectory, outputFileName + ".ts")
                        : Path.Combine(enumTask.OutputDirectory, outputFileName + ".ts");

                    var nameSpace = string.IsNullOrWhiteSpace(enumTask.Namespace)
                        ? cstsdConfig.DefaultTsEnumNamespace
                        : enumTask.Namespace;

                    CheckCreateDir(outputFile);
                    using (TextWriter tw = new StreamWriter(outputFile, false))
                    {
                        RenderTypescript.FromEnumRoslyn(sourceFiles, nameSpace, cstsdConfig, tw);
                        tw.Flush();
                    }
                }
            }

            // Console.WriteLine(@"Press any key to continue...");
            // Console.ReadLine();
        }


        private static void CheckCreateDir(string filePath)
        {
            var dirPath = Path.GetDirectoryName(filePath);

            if (Directory.Exists(dirPath))
                return;

            var di = Directory.CreateDirectory(dirPath);
        }


    }
}
