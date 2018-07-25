using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.Scripting;

namespace Scripting
{
    public enum ScriptingExecutionStatus { Ok, Error }

    public class ScriptingResult
    {
        public ScriptingExecutionStatus Status { get; set; }

        public string Result { get; set; }

        public string ResultType { get; set; }

        public List<ScriptingVariable> Variables { get; set; }
    }

    public class ScriptingVariable
    {
        public string Name { get; set; }

        public string Type { get; set; }

        public int Size { get; set; }

        public string Value { get; set; }

        internal ScriptingVariable(ScriptVariable v) : this(v.Type, v.Value)
        {
            Name = v.Name;
        }

        internal ScriptingVariable(object v) : this(v?.GetType(), v) { }

        private ScriptingVariable(Type type, object value)
        {
            if (type == null)
                return;

            Type = Utils.GetTypeName(type);

            if (value == null)
                return;

            // give arrays and collections a value representing their sizes, to create a useful state to display to a user
            if (type.IsArray)
            {
                Size = ((Array)value).Length;
                Value = Size + " objects";
            }
            else if (value is ICollection coll)
            {
                Size = coll.Count;
                Value = coll.Count + " objects";
            }
            else
            {
                Size = 1;
                Value = value.ToString();
            }
        }
    }
}