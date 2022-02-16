using System;

namespace RabiRiichi.Util {
    public static class Logger {
        public static void Log(string message) {
            Console.WriteLine(message);
        }

        public static void Log(string message, params object[] args) {
            Console.WriteLine(message, args);
        }

        public static void Assert(bool condition, string message) {
            if (!condition) {
                Log(message);
            }
        }
    }
}