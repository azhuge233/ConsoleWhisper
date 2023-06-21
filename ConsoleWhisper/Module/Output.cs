using ConsoleWhisper.Model;
using System;
using System.Linq;

namespace ConsoleWhisper.Module {
    internal static class Output {
        internal static void ResetColor() {
            Console.ResetColor();
        }
        internal static void Error(string msg) {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(msg);
            ResetColor();
        }
        internal static void Warn(string msg) { 
            Console.ForegroundColor= ConsoleColor.Yellow;
            Console.WriteLine(msg);
            ResetColor();
        }
        internal static void Success(string msg) {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(msg);
            ResetColor();
        }
        internal static void Info(string msg) {
            Console.WriteLine(msg);
			ResetColor();
		}
        internal static void InfoR(string msg) {
            Console.Write(msg);
			ResetColor();
		}

        internal static void Help(string msg, bool isHelp, bool isVersion) {
            var splited = msg.Split(Environment.NewLine);

			if (isHelp) Info(msg);
			else if (isVersion) splited.Take(3).ToList().ForEach(Info);
            else {
                for (int i = 0; i < splited.Length; i++) {
                    if (i <= 2) continue;
                    else if (i >= splited.Length - Argument.SupportedArgumentsCount - 1) Info(splited[i]);
                    else Error(splited[i]);
                }
            }
        }
    }
}
