using System.Text;
using System.Text.Json;

namespace RabiRiichi.Communication.Json {
    public class EnumNamingPolicy : JsonNamingPolicy {
        public override string ConvertName(string name) {
            StringBuilder sb = new();
            foreach (char c in name) {
                if (char.IsUpper(c) && sb.Length > 0) {
                    sb.Append('_');
                }
                sb.Append(char.ToLower(c));
            }
            return sb.ToString();
        }
    }
}