using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

/**

GC.GetTotalMemory(false)

new DirectoryInfo(".")
using System.IO;

new DirectoryInfo(".").GetF
string.Join("  ", new DirectoryInfo(".").GetFiles("*.cs").Select(f => $"{f.Name} ({f.Length} bytes)"))

FileInfo[] Files(string pattern) => new DirectoryInfo(".").GetFiles(pattern);
string.Join("  ", Files("*.cs").Select(f => $"{f.Name} ({f.Length} bytes)"))

"Total file size: " + Files("*.cs").Select(f => f.Length).Sum() + " bytes"

var p="Primes:";int m,n=2;for(;n<1e2;m=m<2?n:n%m>0?m-1:n++)p+=m<2?" "+n:"";p

 */

namespace Scripting
{
    class Repl
    {
        private static readonly string Prompt = "> ";

        private static Scripting scripting = new Scripting();

        private static readonly Dictionary<string, Tuple<string, Action>> Commands = new Dictionary<string, Tuple<string, Action>>()
        {
            { "help", Tuple.Create<string, Action>("Shows help", () => PrintHelp()) },
            { "exit", Tuple.Create<string, Action>("Exits REPL", () => Environment.Exit(0)) },
            { "clear", Tuple.Create<string, Action>("Clears scripting state", () => scripting.ClearState()) },
            { "vars", Tuple.Create<string, Action>("Shows declared variables", () => {
                foreach (var variable in scripting.GetVariables())
                    Console.WriteLine($"{variable.Name} = {variable.Value} ({variable.Type})");
            })},
        };

        static void Main(string[] args)
        {
            // Command window on Windows has ANSI disabled by default
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                ConsoleUtils.EnableANSI();

            PrintWelcomeMessage();

            ReadLine.AutoCompletionHandler = new AutoCompletionHandler();

            while (true)
            {
                string command = ReadLine.Read(Prompt);
                if (command.StartsWith("/") && Commands.TryGetValue(command.Substring(1), out var cmd)) {
                    cmd.Item2();
                } else {
                    ReadLine.AddHistory(command);
                    var result = scripting.Eval(command);
                    if (result.Result != null)
                    {
                        Console.WriteLine();
                        if (result.Status == ScriptingExecutionStatus.Error)
                            Console.WriteLine(AnsiUtils.Color(result.Result, AnsiColor.Red));
                        else
                            Console.WriteLine(AnsiUtils.Color(result.Result, AnsiColor.Green));
                        Console.WriteLine();
                    }
                }
            }
        }

        private static void PrintWelcomeMessage()
        {
            Console.WriteLine(AnsiUtils.Color(@"  _____  ____    ___  _______  __ ", 202));
            Console.WriteLine(AnsiUtils.Color(@" / ___/_/ / /_  / _ \/ __/ _ \/ / ", 167));
            Console.WriteLine(AnsiUtils.Color(@"/ /__/_  . __/ / , _/ _// ___/ /__", 132));
            Console.WriteLine(AnsiUtils.Color(@"\___/_    __/ /_/|_/___/_/  /____/", 97));
            Console.WriteLine(AnsiUtils.Color(@"     /_/_/", 62) + AnsiUtils.Color(@"    read-eval-print-loop", 240));
            Console.WriteLine("\nType /help for help\n");
        }

        private static void PrintCommands(IEnumerable<KeyValuePair<string, Tuple<string, Action>>> commands)
        {
            if (commands.Any())
            {
                Console.WriteLine();
                foreach (var cmd in commands.OrderBy(c => c.Key))
                    Console.WriteLine(AnsiUtils.Color($"/{cmd.Key} -- {cmd.Value.Item1}", AnsiColor.Gray));
                Console.WriteLine();
            }
        }

        private static void PrintHelp()
        {
            PrintCommands(Commands);
            Console.WriteLine(AnsiUtils.Color("Use Tab for code completion, and up-arrow to access previous commands", AnsiColor.Gray));
        }

        class AutoCompletionHandler : IAutoCompleteHandler
        {
            public char[] Separators { get; set; } = Enumerable.Range(32, 128-32).Select(n => (char)n).ToArray();

            public string[] GetSuggestions(string text, int index)
            {
                string[] autocomplete = null;

                if (!string.IsNullOrWhiteSpace(text))
                {
                    Console.WriteLine();

                    if (text.StartsWith("/"))
                    {
                        string partialCommand = text.Substring(1);
                        var matches = Commands.Where(c => c.Key.StartsWith(partialCommand));
                        if (matches.Any())
                        {
                            PrintCommands(matches);
                            string longestCommonPrefix = Utils.GetLongestCommonPrefix(matches.Select(kv => kv.Key));
                            autocomplete = new [] { longestCommonPrefix.Substring(partialCommand.Length)};
                        }
                    }

                    if (autocomplete == null)
                    {
                        var options = scripting.CodeComplete(text);
                        autocomplete = new [] { options.Item2 };
                        if (options.Item1.Any())
                        {
                            Console.WriteLine();
                            foreach (string option in options.Item1)
                                Console.WriteLine(AnsiUtils.Color(option, AnsiColor.Gray));
                            Console.WriteLine();
                        }
                    }

                    Console.Write(Prompt + text);
                }

                return autocomplete;
            }
        }
    }
}
