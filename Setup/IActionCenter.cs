using RabiRiichi.Action;
using System.Threading.Tasks;

namespace RabiRiichi.Setup {
    public interface IActionCenter {
        Task OnInquiry(MultiPlayerInquiry inquiry);
    }
}