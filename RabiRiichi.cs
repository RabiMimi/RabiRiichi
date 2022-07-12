using System.Reflection;

namespace RabiRiichi {
    public static class RabiRiichi {
        public static string VERSION => Assembly.GetExecutingAssembly().GetName().Version.ToString();
    }
}
