using System;

namespace RabiRiichi.Utils {
    public static class Logger {
        public static void Log(string message) {
            Console.WriteLine(message);
        }

        public static void Log(string message, params object[] args) {
            Console.WriteLine(message, args);
        }

        public static void Log(Exception e) {
            Console.WriteLine(e.ToString());
        }

        public static void Warn(string message) {
            Console.Error.WriteLine(message);
        }

        public static void Warn(string message, params object[] args) {
            Console.Error.WriteLine(message, args);
        }

        public static void Warn(Exception e) {
            Console.Error.WriteLine(e.ToString());
        }

        public static void Assert(bool condition, string message) {
            if (!condition) {
                throw new ArgumentException(message);
            }
        }
    }
}