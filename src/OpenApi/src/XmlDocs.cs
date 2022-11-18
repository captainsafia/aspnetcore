// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

namespace Microsoft.AspNetCore.OpenApi;
internal static class XmlDocs
{
    internal static Dictionary<string, string> loadedXmlDocumentation =
 new Dictionary<string, string>();
    public static void LoadXmlDocumentation(string xmlDocumentation)
    {
        using (XmlReader xmlReader = XmlReader.Create(new StringReader(xmlDocumentation)))
        {
            while (xmlReader.Read())
            {
                if (xmlReader.NodeType == XmlNodeType.Element && xmlReader.Name == "member")
                {
                    string raw_name = xmlReader["name"];
                    loadedXmlDocumentation[raw_name] = xmlReader.ReadInnerXml();
                }
            }
        }
    }

    // Helper method to format the key strings
    private static string XmlDocumentationKeyHelper(
     string typeFullNameString,
     string memberNameString)
    {
        string key = Regex.Replace(
        typeFullNameString, @"\[.*\]",
        string.Empty).Replace('+', '.');
        if (memberNameString != null)
        {
            key += "." + memberNameString;
        }
        return key;
    }
    public static string GetDocumentation(this Type type)
    {
        LoadXmlDocumentation(type.Assembly);
        string key = "T:" + XmlDocumentationKeyHelper(type.FullName, null);
        loadedXmlDocumentation.TryGetValue(key, out string documentation);
        return documentation;
    }
    public static XmlDocument GetDocumentation(this PropertyInfo propertyInfo)
    {
        LoadXmlDocumentation(propertyInfo.DeclaringType.Assembly);
        string key = "P:" + XmlDocumentationKeyHelper(
        propertyInfo.DeclaringType.FullName, propertyInfo.Name);
        XmlDocument doc = new XmlDocument();
        if (loadedXmlDocumentation.TryGetValue(key, out string documentation))
        {
            doc.LoadXml(documentation);
            return doc;
        }
        return null;
    }

    public static string GetDirectoryPath(this Assembly assembly)
    {
        string codeBase = assembly.CodeBase;
        UriBuilder uri = new UriBuilder(codeBase);
        string path = Uri.UnescapeDataString(uri.Path);
        return Path.GetDirectoryName(path);
    }
    internal static HashSet<Assembly> loadedAssemblies = new HashSet<Assembly>();
    internal static void LoadXmlDocumentation(Assembly assembly)
    {
        if (loadedAssemblies.Contains(assembly))
        {
            return; // Already loaded
        }
        string directoryPath = assembly.GetDirectoryPath();
        string xmlFilePath = Path.Combine(directoryPath, assembly.GetName().Name + ".xml");
        if (File.Exists(xmlFilePath))
        {
            LoadXmlDocumentation(File.ReadAllText(xmlFilePath));
            loadedAssemblies.Add(assembly);
        }
    }
}
