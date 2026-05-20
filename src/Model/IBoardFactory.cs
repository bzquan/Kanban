using System.Threading.Tasks;

namespace Kanban.Model
{
    public interface IBoardFactory
    {
        Task<Board> CreateBoard();
    }
}
