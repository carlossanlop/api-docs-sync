// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using ApiDocsSync.Libraries.Docs;
using System;
using System.Collections.Generic;
using System.IO;

namespace ApiDocsSync.Libraries.IntelliSenseXml
{
    public class Configuration
    {
        private const char Separator = ',';

        private enum Mode
        {
            DisablePrompts,
            Docs,
            ExceptionCollisionThreshold,
            ExcludedAssemblies,
            ExcludedNamespaces,
            ExcludedTypes,
            IncludedAssemblies,
            IncludedNamespaces,
            IncludedTypes,
            Initial,
            IntelliSense,
            PortExceptionsExisting,
            PortExceptionsNew,
            PortMemberParams,
            PortMemberProperties,
            PortMemberReturns,
            PortMemberRemarks,
            PortMemberSummaries,
            PortMemberTypeParams,
            PortTypeParams, // Params of a Type
            PortTypeRemarks,
            PortTypeSummaries,
            PortTypeTypeParams, // TypeParams of a Type
            PrintSummaryDetails,
            PrintUndoc,
            Save,
            SkipInterfaceImplementations,
            SkipInterfaceRemarks
        }

        public static readonly string[] ForbiddenBinSubdirectories = new[] {
            "binplacePackages",
            "docs",
            "externals",
            "mscorlib",
            "native",
            "netfx",
            "netstandard",
            "pkg",
            "Product",
            "ref",
            "runtime",
            "shimsTargetRuntime",
            "testhost",
            "tests",
            "winrt"
        };

        public bool DisablePrompts { get; set; } = true;
        public List<DirectoryInfo> DirsIntelliSense { get; }
        public int ExceptionCollisionThreshold { get; set; } = 70;
        public bool PrintSummaryDetails { get; set; } = false;
        public bool PrintUndoc { get; set; } = false;
        public bool PortExceptionsExisting { get; set; } = false;
        public bool PortExceptionsNew { get; set; } = true;
        public bool PortMemberParams { get; set; } = true;
        public bool PortMemberProperties { get; set; } = true;
        public bool PortMemberReturns { get; set; } = true;
        public bool PortMemberRemarks { get; set; } = true;
        public bool PortMemberSummaries { get; set; } = true;
        public bool PortMemberTypeParams { get; set; } = true;
        /// <summary>
        /// Params of a Type.
        /// </summary>
        public bool PortTypeParams { get; set; } = true;
        public bool PortTypeRemarks { get; set; } = true;
        public bool PortTypeSummaries { get; set; } = true;
        /// <summary>
        /// TypeParams of a Type.
        /// </summary>
        public bool PortTypeTypeParams { get; set; } = true;
        public bool SkipInterfaceImplementations { get; set; } = false;
        public bool SkipInterfaceRemarks { get; set; } = true;

        public DocsConfiguration Docs { get; }

        public Configuration()
        {
            DirsIntelliSense = new List<DirectoryInfo>();
            Docs = new DocsConfiguration();
        }

        public static Configuration GetCLIArguments(string[] args)
        {
            Mode mode = Mode.Initial;

            Log.Info("Verifying CLI arguments...");

            if (args == null || args.Length == 0)
            {
                Log.ErrorAndExit("No arguments passed to the executable.");
            }

            Configuration config = new();

            foreach (string arg in args!)
            {
                switch (mode)
                {
                    case Mode.DisablePrompts:
                        {
                            config.DisablePrompts = ParseOrExit(arg, "Disable prompts");
                            mode = Mode.Initial;
                            break;
                        }

                    case Mode.Docs:
                        {
                            string[] splittedDirPaths = arg.Split(',', StringSplitOptions.RemoveEmptyEntries);

                            Log.Cyan($"Specified Docs xml locations:");
                            foreach (string dirPath in splittedDirPaths)
                            {
                                DirectoryInfo dirInfo = new(dirPath);
                                if (!dirInfo.Exists)
                                {
                                    throw new Exception($"This Docs xml directory does not exist: {dirPath}");
                                }

                                config.Docs.DirsDocsXml.Add(dirInfo);
                                Log.Info($"  -  {dirPath}");
                            }

                            mode = Mode.Initial;
                            break;
                        }

                    case Mode.ExceptionCollisionThreshold:
                        {
                            if (!int.TryParse(arg, out int value))
                            {
                                throw new Exception($"Invalid int value for 'Exception collision threshold' argument: {arg}");
                            }
                            else if (value < 1 || value > 100)
                            {
                                throw new Exception($"Value needs to be between 0 and 100: {value}");
                            }

                            config.ExceptionCollisionThreshold = value;

                            Log.Cyan($"Exception collision threshold:");
                            Log.Info($" - {value}");
                            break;
                        }

                    case Mode.ExcludedAssemblies:
                        {
                            string[] splittedArg = arg.Split(Separator, StringSplitOptions.RemoveEmptyEntries);

                            if (splittedArg.Length > 0)
                            {
                                Log.Cyan("Excluded assemblies:");
                                foreach (string assembly in splittedArg)
                                {
                                    Log.Cyan($" - {assembly}");
                                    config.Docs.ExcludedAssemblies.Add(assembly);
                                }
                            }
                            else
                            {
                                Log.ErrorAndExit("You must specify at least one assembly.");
                            }

                            mode = Mode.Initial;
                            break;
                        }

                    case Mode.ExcludedNamespaces:
                        {
                            string[] splittedArg = arg.Split(Separator, StringSplitOptions.RemoveEmptyEntries);

                            if (splittedArg.Length > 0)
                            {
                                Log.Cyan("Excluded namespaces:");
                                foreach (string ns in splittedArg)
                                {
                                    Log.Cyan($" - {ns}");
                                    config.Docs.ExcludedNamespaces.Add(ns);
                                }
                            }
                            else
                            {
                                Log.ErrorAndExit("You must specify at least one namespace.");
                            }

                            mode = Mode.Initial;
                            break;
                        }

                    case Mode.ExcludedTypes:
                        {
                            string[] splittedArg = arg.Split(Separator, StringSplitOptions.RemoveEmptyEntries);

                            if (splittedArg.Length > 0)
                            {
                                Log.Cyan($"Excluded types:");
                                foreach (string typeName in splittedArg)
                                {
                                    Log.Cyan($" - {typeName}");
                                    config.Docs.ExcludedTypes.Add(typeName);
                                }
                            }
                            else
                            {
                                Log.ErrorAndExit("You must specify at least one type name.");
                            }

                            mode = Mode.Initial;
                            break;
                        }

                    case Mode.IncludedAssemblies:
                        {
                            string[] splittedArg = arg.Split(Separator, StringSplitOptions.RemoveEmptyEntries);

                            if (splittedArg.Length > 0)
                            {
                                Log.Cyan($"Included assemblies:");
                                foreach (string assembly in splittedArg)
                                {
                                    Log.Cyan($" - {assembly}");
                                    config.Docs.IncludedAssemblies.Add(assembly);
                                }
                            }
                            else
                            {
                                Log.ErrorAndExit("You must specify at least one assembly.");
                            }

                            mode = Mode.Initial;
                            break;
                        }

                    case Mode.IncludedNamespaces:
                        {
                            string[] splittedArg = arg.Split(Separator, StringSplitOptions.RemoveEmptyEntries);

                            if (splittedArg.Length > 0)
                            {
                                Log.Cyan($"Included namespaces:");
                                foreach (string ns in splittedArg)
                                {
                                    Log.Cyan($" - {ns}");
                                    config.Docs.IncludedNamespaces.Add(ns);
                                }
                            }
                            else
                            {
                                Log.ErrorAndExit("You must specify at least one namespace.");
                            }

                            mode = Mode.Initial;
                            break;
                        }

                    case Mode.IncludedTypes:
                        {
                            string[] splittedArg = arg.Split(Separator, StringSplitOptions.RemoveEmptyEntries);

                            if (splittedArg.Length > 0)
                            {
                                Log.Cyan($"Included types:");
                                foreach (string typeName in splittedArg)
                                {
                                    Log.Cyan($" - {typeName}");
                                    config.Docs.IncludedTypes.Add(typeName);
                                }
                            }
                            else
                            {
                                Log.ErrorAndExit("You must specify at least one type name.");
                            }

                            mode = Mode.Initial;
                            break;
                        }

                    case Mode.Initial:
                        {
                            switch (arg.ToUpperInvariant())
                            {
                                case "-DOCS":
                                    mode = Mode.Docs;
                                    break;

                                case "-DISABLEPROMPTS":
                                    mode = Mode.DisablePrompts;
                                    break;

                                case "EXCEPTIONCOLLISIONTHRESHOLD":
                                    mode = Mode.ExceptionCollisionThreshold;
                                    break;

                                case "-EXCLUDEDASSEMBLIES":
                                    mode = Mode.ExcludedAssemblies;
                                    break;

                                case "-EXCLUDEDNAMESPACES":
                                    mode = Mode.ExcludedNamespaces;
                                    break;

                                case "-EXCLUDEDTYPES":
                                    mode = Mode.ExcludedTypes;
                                    break;

                                case "-H":
                                case "-HELP":
                                    Log.PrintHelp();
                                    Environment.Exit(0);
                                    break;

                                case "-INCLUDEDASSEMBLIES":
                                    mode = Mode.IncludedAssemblies;
                                    break;

                                case "-INCLUDEDNAMESPACES":
                                    mode = Mode.IncludedNamespaces;
                                    break;

                                case "-INCLUDEDTYPES":
                                    mode = Mode.IncludedTypes;
                                    break;

                                case "-INTELLISENSE":
                                    mode = Mode.IntelliSense;
                                    break;

                                case "-PORTEXCEPTIONSEXISTING":
                                    mode = Mode.PortExceptionsExisting;
                                    break;

                                case "-PORTEXCEPTIONSNEW":
                                    mode = Mode.PortExceptionsNew;
                                    break;

                                case "-PORTMEMBERPARAMS":
                                    mode = Mode.PortMemberParams;
                                    break;

                                case "-PORTMEMBERPROPERTIES":
                                    mode = Mode.PortMemberProperties;
                                    break;

                                case "-PORTMEMBERRETURNS":
                                    mode = Mode.PortMemberReturns;
                                    break;

                                case "-PORTMEMBERREMARKS":
                                    mode = Mode.PortMemberRemarks;
                                    break;

                                case "-PORTMEMBERSUMMARIES":
                                    mode = Mode.PortMemberSummaries;
                                    break;

                                case "-PORTMEMBERTYPEPARAMS":
                                    mode = Mode.PortMemberTypeParams;
                                    break;

                                case "-PORTTYPEPARAMS": // Params of a Type
                                    mode = Mode.PortTypeParams;
                                    break;

                                case "-PORTTYPEREMARKS":
                                    mode = Mode.PortTypeRemarks;
                                    break;

                                case "-PORTTYPESUMMARIES":
                                    mode = Mode.PortTypeSummaries;
                                    break;

                                case "-PORTTYPETYPEPARAMS": // TypeParams of a Type
                                    mode = Mode.PortTypeTypeParams;
                                    break;

                                case "-PRINTSUMMARYDETAILS":
                                    mode = Mode.PrintSummaryDetails;
                                    break;

                                case "-PRINTUNDOC":
                                    mode = Mode.PrintUndoc;
                                    break;

                                case "-SAVE":
                                    mode = Mode.Save;
                                    break;

                                case "-SKIPINTERFACEIMPLEMENTATIONS":
                                    mode = Mode.SkipInterfaceImplementations;
                                    break;

                                case "-SKIPINTERFACEREMARKS":
                                    mode = Mode.SkipInterfaceRemarks;
                                    break;

                                default:
                                    Log.ErrorAndExit($"Unrecognized argument: {arg}");
                                    break;
                            }
                            break;
                        }

                    case Mode.IntelliSense:
                        {
                            string[] splittedDirPaths = arg.Split(',', StringSplitOptions.RemoveEmptyEntries);

                            Log.Cyan($"Specified IntelliSense locations:");
                            foreach (string dirPath in splittedDirPaths)
                            {
                                DirectoryInfo dirInfo = new(dirPath);
                                if (!dirInfo.Exists)
                                {
                                    throw new Exception($"This IntelliSense directory does not exist: {dirPath}");
                                }

                                config.DirsIntelliSense.Add(dirInfo);
                                Log.Info($"  -  {dirPath}");
                            }

                            mode = Mode.Initial;
                            break;
                        }

                    case Mode.PortExceptionsExisting:
                        {
                            config.PortExceptionsExisting = ParseOrExit(arg, "Port existing exceptions");
                            mode = Mode.Initial;
                            break;
                        }

                    case Mode.PortExceptionsNew:
                        {
                            config.PortExceptionsNew = ParseOrExit(arg, "Port new exceptions");
                            mode = Mode.Initial;
                            break;
                        }

                    case Mode.PortMemberParams:
                        {
                            config.PortMemberParams = ParseOrExit(arg, "Port member Params");
                            mode = Mode.Initial;
                            break;
                        }

                    case Mode.PortMemberProperties:
                        {
                            config.PortMemberProperties = ParseOrExit(arg, "Port member Properties");
                            mode = Mode.Initial;
                            break;
                        }

                    case Mode.PortMemberRemarks:
                        {
                            config.PortMemberRemarks = ParseOrExit(arg, "Port member Remarks");
                            mode = Mode.Initial;
                            break;
                        }

                    case Mode.PortMemberReturns:
                        {
                            config.PortMemberReturns = ParseOrExit(arg, "Port member Returns");
                            mode = Mode.Initial;
                            break;
                        }

                    case Mode.PortMemberSummaries:
                        {
                            config.PortMemberSummaries = ParseOrExit(arg, "Port member Summaries");
                            mode = Mode.Initial;
                            break;
                        }

                    case Mode.PortMemberTypeParams:
                        {
                            config.PortMemberTypeParams = ParseOrExit(arg, "Port member TypeParams");
                            mode = Mode.Initial;
                            break;
                        }

                    case Mode.PortTypeParams: // Params of a Type
                        {
                            config.PortTypeParams = ParseOrExit(arg, "Port Type Params");
                            mode = Mode.Initial;
                            break;
                        }

                    case Mode.PortTypeRemarks:
                        {
                            config.PortTypeRemarks = ParseOrExit(arg, "Port Type Remarks");
                            mode = Mode.Initial;
                            break;
                        }

                    case Mode.PortTypeSummaries:
                        {
                            config.PortTypeSummaries = ParseOrExit(arg, "Port Type Summaries");
                            mode = Mode.Initial;
                            break;
                        }

                    case Mode.PortTypeTypeParams: // TypeParams of a Type
                        {
                            config.PortTypeTypeParams = ParseOrExit(arg, "Port Type TypeParams");
                            mode = Mode.Initial;
                            break;
                        }

                    case Mode.PrintSummaryDetails:
                        {
                            config.PrintSummaryDetails = ParseOrExit(arg, "Print summary details");
                            mode = Mode.Initial;
                            break;
                        }

                    case Mode.PrintUndoc:
                        {
                            config.PrintUndoc = ParseOrExit(arg, "Print undoc");
                            mode = Mode.Initial;
                            break;
                        }

                    case Mode.Save:
                        {
                            config.Docs.Save = ParseOrExit(arg, "Save");
                            mode = Mode.Initial;
                            break;
                        }

                    case Mode.SkipInterfaceImplementations:
                        {
                            config.SkipInterfaceImplementations = ParseOrExit(arg, "Skip interface implementations");
                            mode = Mode.Initial;
                            break;
                        }

                    case Mode.SkipInterfaceRemarks:
                        {
                            config.SkipInterfaceRemarks = ParseOrExit(arg, "Skip appending interface remarks");
                            mode = Mode.Initial;
                            break;
                        }

                    default:
                        {
                            Log.ErrorAndExit("Unexpected mode.");
                            break;
                        }
                }
            }

            if (mode != Mode.Initial)
            {
                Log.ErrorAndExit("You missed an argument value.");
            }

            if (config.Docs.IncludedAssemblies.Count == 0)
            {
                Log.ErrorAndExit($"You must specify at least one assembly with {nameof(Mode.IncludedAssemblies)}.");
            }

            return config;
        }

        // Tries to parse the user argument string as boolean, and if it fails, exits the program.
        private static bool ParseOrExit(string arg, string paramFriendlyName)
        {
            if (!bool.TryParse(arg, out bool value))
            {
                throw new Exception($"Invalid boolean value for '{paramFriendlyName}' argument: {arg}");
            }

            Log.Cyan($"{paramFriendlyName}:");
            Log.Info($" - {value}");

            return value;
        }
    }
}
