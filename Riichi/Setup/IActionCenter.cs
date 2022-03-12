using RabiRiichi.Action;
using System.Threading.Tasks;

namespace RabiRiichi.Riichi.Setup {
    public interface IActionCenter {
        void OnInquiry(MultiPlayerInquiry inquiry);
    }
}