using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Scripting;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Scripting;

namespace Scripting
{
    /// <summary>Allows execution of arbitrary C# code, maintaining an execution state between submissions.</summary>
    public class Scripting
    {
        private ScriptState<object> _scriptState = NewScriptState();

        private static readonly ReadOnlyCollection<string> InitialImports = new List<string>()
        {
            "Scripting",
            "System",
            "System.Linq",
            "System.Collections",
            "System.Collections.Generic",
            "System.Text",
            "System.Threading.Tasks",
        }.AsReadOnly();

        // Imports for the current script state. Will be added to if any 'using' statements are submitted.
        private HashSet<string> _imports = new HashSet<string>(InitialImports);

        private static ScriptState<object> NewScriptState()
        {
            var options = ScriptOptions.Default;
            var assemblies = AppDomain.CurrentDomain
                        .GetAssemblies()
                        .Where(a => !a.IsDynamic)
                        .Select(a => a.Location).ToArray();
            options = options.AddReferences(assemblies);
            options = options.AddImports(InitialImports.ToArray());
            return CSharpScript.RunAsync("", options).Result;
        }

        /// <summary>Clears the state of previously run commands</summary>
        public ScriptingResult ClearState()
        {
            _scriptState = NewScriptState();
            return GetScriptState();
        }

        /// <summary>Runs the specified C# command and returns the state</summary>
        public ScriptingResult Eval(string command)
        {
            try
            {
                _scriptState = _scriptState.ContinueWithAsync(command).Result;
            }
            catch (CompilationErrorException e)
            {
                return GetErrorMessage("Compile exception:\n" + string.Join(Environment.NewLine, e.Diagnostics));
            }
            catch (AggregateException e)
            {
                Exception ie = e.InnerException ?? e;
                return GetErrorMessage($"Execution exception: [{ie.Message}] [{ie.StackTrace}]");
            }

            // If any 'using' statements were submitted, add them to the set of imports (used for code completion)
            _imports.UnionWith(((CompilationUnitSyntax)CSharpSyntaxTree.ParseText(command).GetRoot()).Usings.Select(u => u.Name.ToString()));

            return GetScriptState();
        }

        /// <summary>Returns completion options and completion string for the currently written command</summary>
        public Tuple<List<string>, string> CodeComplete(string command)
        {
            return CodeCompletion.GetCompletionOptions(_scriptState, _imports, command);
        }

        public List<ScriptingVariable> GetVariables()
        {
            // Note: state variables appear in declaration order; re-declared variables appear multiple times
            // The desired output is the current state, so we need to remove earlier duplicates

            var lastVariableByName = new Dictionary<string, ScriptVariable>();

            foreach (var v in _scriptState.Variables)
                lastVariableByName[v.Name] = v;

            // sort by variable name
            return lastVariableByName.OrderBy(v => v.Key).Select(v => new ScriptingVariable(v.Value)).ToList();
        }

        private ScriptingResult GetErrorMessage(string message)
        {
            return new ScriptingResult
            {
                Status = ScriptingExecutionStatus.Error,
                Result = message,
                Variables = GetVariables(),
            };
        }

        private ScriptingResult GetScriptState()
        {
            object returnValue = _scriptState.ReturnValue;
            ScriptingVariable result = new ScriptingVariable(returnValue);

            return new ScriptingResult
            {
                Status = ScriptingExecutionStatus.Ok,
                Result = result.Value,
                ResultType = result.Type,
                Variables = GetVariables(),
            };
        }
    }
}