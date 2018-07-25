using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CSharp;

namespace Scripting
{
    class Utils
    {
        private static readonly CSharpCodeProvider CodeProvider = new CSharpCodeProvider();

        private static Dictionary<Type, string> TypeNameMapping = new Dictionary<Type, string>();

        internal static string GetTypeName(Type type)
        {
            TypeNameMapping.TryGetValue(type, out string name);

            if (name == null)
            {
                // TODO: accept generic types as input, that are bound when finding matching methods
                var reference = new CodeTypeReference(type);
                if (type.ContainsGenericParameters)
                    reference.TypeArguments.AddRange(type.GenericTypeArguments.Select(a => new CodeTypeReference(a)).ToArray());
                name = CodeProvider.GetTypeOutput(reference);
                TypeNameMapping[type] = name = Regex.Replace(name, "`\\d+", "");
            }

            return name;
        }

        internal static string GetTypeNameShort(Type type)
        {
            return GetTypeName(type).Replace(type.Namespace + ".", "");
        }

        internal static string GetLongestCommonPrefix(IEnumerable<string> strings)
        {
            return !strings.Any() ? "" : new string(
                strings.First().Substring(0, strings.Min(s => s.Length))
                .TakeWhile((c, i) => strings.All(s => s[i] == c)).ToArray());
        }
    }
}