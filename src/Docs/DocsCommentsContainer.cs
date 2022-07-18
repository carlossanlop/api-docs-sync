// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace ApiDocsSync.Libraries.Docs
{
    public class DocsCommentsContainer
    {
        private readonly DocsConfiguration _config;

        public readonly Dictionary<string, DocsType> Types = new();
        public readonly Dictionary<string, DocsMember> AllMembers = new();

        public DocsCommentsContainer(DocsConfiguration config) => _config = config;

        public void SaveToDisk()
        {
            if (!_config.Save)
            {
                Log.Line();
                Log.Error("[No files were saved]");
                Log.Warning($"Did you forget to specify '-{nameof(_config.Save)} true'?");
                Log.Line();

                return;
            }

            List<string> savedFiles = new();
            foreach (DocsType type in Types.Values.Where(x => x.Changed))
            {
                Log.Info(false, $"Saving changes for {type.FilePath} ... ");

                try
                {
                    // These settings prevent the addition of the <xml> element on the first line and will preserve indentation+endlines
                    XmlWriterSettings xws = new()
                    {
                        Encoding = type.FileEncoding,
                        OmitXmlDeclaration = true,
                        Indent = true,
                        CheckCharacters = false
                    };

                    using (XmlWriter xw = XmlWriter.Create(type.FilePath, xws))
                    {
                        type.XDoc.Save(xw);
                    }

                    // Workaround to delete the annoying endline added by XmlWriter.Save
                    string fileData = File.ReadAllText(type.FilePath);
                    if (!fileData.EndsWith(Environment.NewLine))
                    {
                        File.WriteAllText(type.FilePath, fileData + Environment.NewLine, type.FileEncoding);
                    }

                    Log.Success(" [Saved]");
                }
                catch (Exception e)
                {
                    Log.Error("Failed to write to {0}. {1}", type.FilePath, e.Message);
                    Log.Error(e.StackTrace ?? string.Empty);
                    if (e.InnerException != null)
                    {
                        Log.Line();
                        Log.Error(e.InnerException.Message);
                        Log.Line();
                        Log.Error(e.InnerException.StackTrace ?? string.Empty);
                    }
                }
            }
        }

        public IEnumerable<FileInfo> EnumerateFiles()
        {
            // Union avoids duplication
            IEnumerable<string> includedAssembliesAndNamespaces = _config.IncludedAssemblies.Union(_config.IncludedNamespaces);
            IEnumerable<string> excludedAssembliesAndNamespaces = _config.ExcludedAssemblies.Union(_config.ExcludedNamespaces);

            foreach (DirectoryInfo rootDir in _config.DirsDocsXml)
            {
                // Try to find folders with the names of assemblies AND namespaces (if the user specified any)
                foreach (string included in includedAssembliesAndNamespaces)
                {
                    // If the user specified a sub-assembly or sub-namespace to exclude, we need to skip it
                    if (excludedAssembliesAndNamespaces.Any(excluded => included.StartsWith(excluded)))
                    {
                        continue;
                    }

                    foreach (DirectoryInfo subDir in rootDir.EnumerateDirectories($"{included}*", SearchOption.TopDirectoryOnly))
                    {
                        foreach (FileInfo fileInfo in subDir.EnumerateFiles("*.xml", SearchOption.AllDirectories))
                        {
                            if (HasAllowedFileName(fileInfo))
                            {
                                // LoadFile will determine if the Type is allowed or not
                                yield return fileInfo;
                            }
                        }
                    }

                    if (!_config.SkipInterfaceImplementations)
                    {
                        // Find interfaces only inside System.* folders.
                        // Including Microsoft.* folders reaches the max limit of files to include in a list, plus there are no essential interfaces there.
                        foreach (DirectoryInfo subDir in rootDir.EnumerateDirectories("System*", SearchOption.AllDirectories))
                        {
                            // Exclude any folder that starts with the excluded assemblies OR excluded namespaces
                            if (!excludedAssembliesAndNamespaces.Any(excluded => subDir.Name.StartsWith(excluded)) && !subDir.Name.EndsWith(".Tests"))
                            {
                                // Ensure including interface files that start with I and then an uppercase letter, and prevent including files like 'Int'
                                foreach (FileInfo fileInfo in subDir.EnumerateFiles("I*.xml", SearchOption.AllDirectories))
                                {
                                    if (fileInfo.Name[1] >= 'A' || fileInfo.Name[1] <= 'Z')
                                    {
                                        yield return fileInfo;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        public void CollectDocsComments()
        {
            if (_config.DirsDocsXml.Count == 0)
            {
                Log.ErrorAndExit($"No dotnet-api-docs xml folders were specified.");
            }

            Log.Info("Looking for Docs xml files...");
            foreach (FileInfo fileInfo in EnumerateFiles())
            {
                LoadDocsTypeForFile(fileInfo);
            }
            Log.Success("Finished looking for Docs xml files.");
            Log.Line();
        }

        public DocsType? LoadDocsTypeForFile(FileInfo fileInfo)
        {
            XDocument? xDoc = null;
            Encoding? encoding = null;
            try
            {
                var utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
                var utf8Bom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
                using (StreamReader sr = new(fileInfo.FullName, utf8NoBom, detectEncodingFromByteOrderMarks: true))
                {
                    xDoc = XDocument.Load(sr);
                    encoding = sr.CurrentEncoding;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"Failed to load '{fileInfo.FullName}'. {ex}");
            }

            if (xDoc != null && encoding != null)
            {
                return LoadDocsTypeForXDoc(xDoc, fileInfo.FullName, encoding);
            }

            return null;
        }

        public DocsType? LoadDocsTypeForXDoc(XDocument xDoc, string filePath, Encoding encoding)
        {
            if (IsDocsXmlFileMalformed(xDoc, filePath))
            {
                return null;
            }

            DocsType docsType = new(filePath, xDoc, xDoc.Root!, encoding);

            bool add = false;
            bool addedAsInterface = false;

            bool containsForbiddenAssembly = docsType.AssemblyInfos.Any(assemblyInfo =>
                                                _config.ExcludedAssemblies.Any(excluded => assemblyInfo.AssemblyName.StartsWith(excluded)) ||
                                                _config.ExcludedNamespaces.Any(excluded => assemblyInfo.AssemblyName.StartsWith(excluded)));

            if (!containsForbiddenAssembly)
            {
                // If it's an interface, always add it if the user wants to detect EIIs,
                // even if it's in an assembly that was not included but was not explicitly excluded
                addedAsInterface = false;
                if (!_config.SkipInterfaceImplementations)
                {
                    // Interface files start with I, and have an 2nd alphabetic character
                    addedAsInterface = docsType.Name.Length >= 2 && docsType.Name[0] == 'I' && docsType.Name[1] >= 'A' && docsType.Name[1] <= 'Z';
                    add |= addedAsInterface;
                }

                bool containsAllowedAssembly = docsType.AssemblyInfos.Any(assemblyInfo =>
                                                    _config.IncludedAssemblies.Any(included => assemblyInfo.AssemblyName.StartsWith(included)) ||
                                                    _config.IncludedNamespaces.Any(included => assemblyInfo.AssemblyName.StartsWith(included)));

                if (containsAllowedAssembly)
                {
                    // If it was already added above as an interface, skip this part
                    // Otherwise, find out if the type belongs to the included assemblies, and if specified, to the included (and not excluded) types
                    // This includes interfaces even if user wants to skip EIIs - They will be added if they belong to this namespace or to the list of
                    // included (and not exluded) types, but will not be used for EII, but rather as normal types whose comments should be ported
                    if (!addedAsInterface)
                    {
                        // Either the user didn't specify namespace filtering (allow all namespaces) or specified particular ones to include/exclude
                        if (!_config.IncludedNamespaces.Any() ||
                                (_config.IncludedNamespaces.Any(included => docsType.Namespace.StartsWith(included)) &&
                                 !_config.ExcludedNamespaces.Any(excluded => docsType.Namespace.StartsWith(excluded))))
                        {
                            // Can add if the user didn't specify type filtering (allow all types), or specified particular ones to include/exclude
                            add = !_config.IncludedTypes.Any() ||
                                    (_config.IncludedTypes.Contains(docsType.Name) &&
                                     !_config.ExcludedTypes.Contains(docsType.Name));
                        }
                    }
                }
            }

            if (add)
            {
                int totalMembersAdded = 0;
                Types.TryAdd(docsType.DocId, docsType); // is it OK this encounters duplicates?

                if (XmlHelper.TryGetChildElement(xDoc.Root!, "Members", out XElement? xeMembers) && xeMembers != null)
                {
                    foreach (XElement xeMember in xeMembers.Elements("Member"))
                    {
                        DocsMember member = new(filePath, docsType, xeMember);
                        totalMembersAdded++;
                        docsType.Members.TryAdd(member.DocId, member); // is it OK this encounters duplicates?
                        AllMembers.TryAdd(member.DocId, member);
                    }
                }

                string message = $"Type '{docsType.DocId}' added with {totalMembersAdded} member(s) included: {filePath}";
                if (addedAsInterface)
                {
                    Log.Magenta("[Interface] - " + message);
                }
                else if (totalMembersAdded == 0)
                {
                    Log.Warning(message);
                }
                else
                {
                    Log.Success(message);
                }
            }

            return docsType;
        }

        private static bool HasAllowedFileName(FileInfo fileInfo) =>
            !fileInfo.Name.StartsWith("ns-") &&
                fileInfo.Name != "index.xml" &&
                fileInfo.Name != "_filter.xml";

        private static bool IsDocsXmlFileMalformed(XDocument? xDoc, string fileName)
        {
            if (xDoc == null)
            {
                Log.Error($"XDocument is null: {fileName}");
                return true;
            }
            if (xDoc.Root == null)
            {
                Log.Error($"Docs xml file does not have a root element: {fileName}");
                return true;
            }

            if (xDoc.Root.Name == "Namespace")
            {
                Log.Error($"Skipping namespace file (should have been filtered already): {fileName}");
                return true;
            }

            if (xDoc.Root.Name != "Type")
            {
                Log.Error($"Docs xml file does not have a 'Type' root element: {fileName}");
                return true;
            }

            if (!xDoc.Root.HasElements)
            {
                Log.Error($"Docs xml file Type element does not have any children: {fileName}");
                return true;
            }

            if (xDoc.Root.Elements("Docs").Count() != 1)
            {
                Log.Error($"Docs xml file Type element does not have a Docs child: {fileName}");
                return true;
            }

            return false;
        }
    }
}
