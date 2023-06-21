using ConsoleWhisper.Model;
using System;

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

        internal static void Help(string msg) {
            string[] splited = msg.Split(Environment.NewLine);
            for (int i = 0; i < splited.Length; i++) {
                if (i <= 1 || i >= splited.Length - Argument.SupportedArgumentsCount - 1) Info(splited[i]);
                else Error(splited[i]);
			}
        }
    }
}
