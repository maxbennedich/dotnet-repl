using NUnit.Framework;

namespace Scripting
{
    [TestFixture]
    class ScriptingTest
    {
        [TestCase("1234", "", "")]
        [TestCase("1234.", "1234", "")]
        [TestCase("\"test\"", "", "")]
        [TestCase("\"test\".", "\"test\"", "")]
        [TestCase("\"test\".Leng", "\"test\"", "Leng")]
        [TestCase("if (\"test\".Leng", "\"test\"", "Leng")]
        [TestCase("k += array[3].", "array[3]", "")]
        public void FindCodeCompletionExpression(string command, string expression, string prefix)
        {
            var ex = CodeCompletion.GetExpressionToComplete(command);
            Assert.AreEqual(expression, ex.Expression);
            Assert.AreEqual(prefix, ex.Prefix);
        }
    }
}
