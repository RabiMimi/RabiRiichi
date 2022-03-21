using System;
using System.Reflection;

namespace RabiRiichi {
    public static class RabiRiichi {
        public static readonly Lazy<string> VERSION = new(() => {
            return Assembly.GetExecutingAssembly().GetName().Version.ToString();
        });
    }
}
