using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Scripting;

namespace Scripting
{
    class CodeCompletionExpression
    {
        public string Expression { get; set; } = "";
        public string Prefix { get; set; } = "";
    }

    class CodeCompletion
    {
        private static readonly CodeCompletionExpression NoExpression = new CodeCompletionExpression();

        private static readonly String ExpressionPrefix = "void T(){";
        private static readonly String ExpressionPostfix = "}";

        internal static CodeCompletionExpression GetExpressionToComplete(string command)
        {
            // wrap expression in a method so that we can build a syntax tree
            var tree = CSharpSyntaxTree.ParseText(ExpressionPrefix + command + ExpressionPostfix);
            int endOfCodeIdx = ExpressionPrefix.Length + command.Length;
            return FindExpression(tree.GetRoot(), endOfCodeIdx);
        }

        private static CodeCompletionExpression FindExpression(SyntaxNodeOrToken node, int endOfCodeIdx)
        {
            if (node.FullSpan.End == endOfCodeIdx && node.Kind() == SyntaxKind.SimpleMemberAccessExpression)
            {
                var children = node.ChildNodesAndTokens();
                return new CodeCompletionExpression
                {
                    Expression = children.First().ToString(),
                    Prefix = children.ElementAt(2).ToString(),
                };
            }

            foreach (var c in node.ChildNodesAndTokens())
            {
                var expression = FindExpression(c, endOfCodeIdx);
                if (expression != NoExpression)
                    return expression;
            }

            return NoExpression;
        }

        private static readonly Tuple<List<string>, string> NoOptions = Tuple.Create(new List<string>(), "");

        public static Tuple<List<string>, string> GetCompletionOptions(ScriptState<object> scriptState, HashSet<string> imports, string command)
        {
            try
            {
                var expression = GetExpressionToComplete(command);
                var evaluatedScriptState = scriptState.ContinueWithAsync(expression.Expression).Result;
                if (evaluatedScriptState.ReturnValue == null)
                    return NoOptions;

                var type = evaluatedScriptState.ReturnValue.GetType();

                var options = new Dictionary<string, List<string>>();

                void AddOption(string name, string definition)
                {
                    if (name.StartsWith(expression.Prefix))
                    {
                         if (!options.TryGetValue(name, out var overloads))
                            options[name] = overloads = new List<string>();
                         overloads.Add(definition);
                    }
                }

                foreach (var mi in GetExtensionMethods(imports, type))
                {
                    string parameters = string.Join(", ", mi.GetParameters().Skip(1).Select(p => Utils.GetTypeNameShort(p.ParameterType) + " " + p.Name));
                    AddOption(mi.Name, $"{Utils.GetTypeNameShort(mi.ReturnType)} {mi.Name}({parameters})");
                }

                foreach (var mi in type.GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(m => !m.IsSpecialName))
                {
                    string parameters = string.Join(", ", mi.GetParameters().Select(p => Utils.GetTypeNameShort(p.ParameterType) + " " + p.Name));
                    AddOption(mi.Name, $"{Utils.GetTypeNameShort(mi.ReturnType)} {mi.Name}({parameters})");
                }

                foreach (var prop in type.GetProperties())
                {
                    var mi = prop.GetMethod;
                    string parameters = string.Join(", ", mi.GetParameters().Select(p => Utils.GetTypeName(p.ParameterType) + " " + p.Name));
                    AddOption(prop.Name, $"{Utils.GetTypeNameShort(prop.PropertyType)} {prop.Name}{(parameters.Any() ? $"[{parameters}]" : "")}");
                }

                List<string> optionList = new List<string>();
                
                if (options.Count == 1)
                {
                    optionList = options.First().Value;
                }
                else if (options.Count > 1)
                {
                    optionList = options
                        .OrderBy(kv => kv.Key)
                        .Select(kv => { kv.Value.Sort(); return kv; })
                        .Select(kv => kv.Key + " -- " + kv.Value[0] + (kv.Value.Count == 1 ? "" : $" (+{kv.Value.Count - 1} overload{(kv.Value.Count == 2 ? "" : "s")})")).ToList();
                }

                string longestCommonPrefix = Utils.GetLongestCommonPrefix(options.Keys);

                return Tuple.Create(optionList, longestCommonPrefix.Substring(expression.Prefix.Length));
            }
            catch (CompilationErrorException)
            {
                // code submitted for autocomplete is invalid; ignore, since this happens all the time
                return NoOptions;
            }
            catch (AggregateException e)
            {
                Exception ie = e.InnerException ?? e;
                Console.WriteLine($"Execution exception: [{ie.Message}] [{ie.StackTrace}]");
                return NoOptions;
            }
        }

        private static bool GenericIsAssignableFrom(Type genericType, Type typeToCheck, Type[] typeConstraints, GenericParameterAttributes[] attributes)
        {
            if (!genericType.IsGenericType || typeToCheck == null)
                return false;
            
            return CheckType(genericType, typeToCheck, typeConstraints, attributes) ||
                CheckTypeInterfaces(genericType, typeToCheck, typeConstraints, attributes);
        }

        private static bool CheckType(Type openGenericType, Type typeToCheck, Type[] typeConstraints, GenericParameterAttributes[] attributes)
        {
            if (!typeToCheck.IsGenericType)
                return false;

            var typeArgs = typeToCheck.GetGenericArguments();
            if (typeArgs.Length == openGenericType.GetGenericArguments().Length)
            {
                try
                {
                    var closed = openGenericType.MakeGenericType(typeArgs);
                    if (!closed.IsAssignableFrom(typeToCheck))
                        return false;

                    if (typeConstraints.Length != typeArgs.Length || attributes.Length != typeArgs.Length)
                        throw new InvalidOperationException($"{typeConstraints.Length}, {attributes.Length}, {typeArgs.Length}");

                    for (int i = 0; i < typeArgs.Length; ++i)
                    {
                        if (typeConstraints[i].IsGenericParameter && !typeConstraints[i].GetGenericParameterConstraints().All(c => c.IsAssignableFrom(typeArgs[i])))
                            return false;

                        // TODO: check for more attributes here, such as DefaultConstructorConstraint
                        bool referenceType = (attributes[i] & GenericParameterAttributes.ReferenceTypeConstraint) != 0;
                        if (referenceType && typeArgs[i].IsValueType)
                            return false;
                    }

                    return true;
                }
                catch
                {
                    // violated type contraints
                    return false;
                }
            }

            return false;
        }

        private static bool CheckTypeInterfaces(Type openGenericType, Type typeToCheck, Type[] typeConstraints, GenericParameterAttributes[] attributes)
        {
            return GenericIsAssignableFrom(openGenericType, typeToCheck.BaseType, typeConstraints, attributes) ||
                typeToCheck.GetInterfaces().Any(i => GenericIsAssignableFrom(openGenericType, i, typeConstraints, attributes));
        }

        private static List<MethodInfo> GetExtensionMethods(HashSet<string> namespaces, Type extendedType)
        {
            var extensionTypes = AppDomain.CurrentDomain
                .GetAssemblies()
                .SelectMany(t => t.GetTypes())
                .Where(t => t.IsClass && namespaces.Contains(t.Namespace))
                .Where(type => type.IsSealed && !type.IsGenericType && !type.IsNested);

            return extensionTypes
                .SelectMany(type => type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                .Where(method =>
                {
                    // is the method an extension method?
                    if (!method.IsDefined(typeof(ExtensionAttribute), false))
                        return false;

                    Type methodType = method.GetParameters()[0].ParameterType;

                    // extension method type directly assignable from the tested type
                    if (methodType.IsAssignableFrom(extendedType))
                        return true;

                    if (methodType.IsGenericType)
                    {
                        Type genericType = methodType.GetGenericTypeDefinition();

                        // for constraints such as generic type has to be a certain class
                        Type[] typeConstraints = methodType.GenericTypeArguments.Where(t => t.IsGenericParameter).ToArray();

                        // for constraints such as generic type has to be a reference type
                        var attributes = methodType.GetGenericArguments().Where(t => t.IsGenericParameter).Select(t => t.GenericParameterAttributes).ToArray();
                        
                        if (GenericIsAssignableFrom(genericType, extendedType, typeConstraints, attributes))
                            return true;
                    }

                    return false;
                }).ToList();
        }
   }
}