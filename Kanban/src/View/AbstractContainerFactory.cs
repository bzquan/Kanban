using Unity;

namespace Kanban
{
    public class AbstractContainerFactory
    {
        public AbstractContainerFactory(IUnityContainer container)
        {
            Container = container;
        }

        public IUnityContainer Container { get; private set; }
    }
}
