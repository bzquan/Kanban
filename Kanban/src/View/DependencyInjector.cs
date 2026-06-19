using Unity;
using Unity.Lifetime;
using Unity.Resolution;

namespace Kanban;

public class DependencyInjector
{
    private IUnityContainer DIContainer = new UnityContainer();
    private DI4Model m_DI4Model;
    private DI4ViewModel m_DI4ViewModel;
    private DI4Repository m_DI4Repository;
    private DI4View m_DI4View;
    private DI4Infrastructure m_DI4Infrastructure;

    public DependencyInjector()
    {
        m_DI4Repository = new DI4Repository(DIContainer);
        m_DI4Infrastructure = new DI4Infrastructure(DIContainer);
        m_DI4Model = new DI4Model(DIContainer);
        m_DI4ViewModel = new DI4ViewModel(DIContainer);
        m_DI4View = new DI4View(DIContainer);
    }

    public void RegisterDependencies()
    {
        m_DI4Repository.RegisterDependencies();
        m_DI4Infrastructure.RegisterDependencies();
        m_DI4Model.RegisterDependencies();
        m_DI4ViewModel.RegisterDependencies();
        m_DI4View.RegisterDependencis();
    }

    public T Resolve<T>()
    {
        return DIContainer.Resolve<T>();
    }
}

class DIBase
{
    internal IUnityContainer DIContainer { get; private set; }

    internal DIBase(IUnityContainer DIContainer)
    {
        this.DIContainer = DIContainer;
    }
}

class DI4View : DIBase
{
    internal DI4View(IUnityContainer DIContainer) : base(DIContainer) { }

    internal void RegisterDependencis()
    {
        DIContainer
            .RegisterType<MainWindow>(new ContainerControlledLifetimeManager())
            .RegisterInstance<ViewModel.IViewModelProperties>(new ViewModelProperties())
            .RegisterInstance(new BoardPageFactory(board => DIContainer.Resolve<BoardPage>(new ParameterOverride("board", board))));
    }
}

class DI4ViewModel : DIBase
{
    internal DI4ViewModel(IUnityContainer DIContainer) : base(DIContainer) { }

    internal void RegisterDependencies()
    {
        DIContainer
            .RegisterInstance(new ViewModel.CardFactory(async (board, cardWorkState, srcCard) => await CreateCard(board, cardWorkState, srcCard)))
            .RegisterInstance(new ViewModel.WIPDoneViewModelFactory((board, cardWorkState, cardFilter, loadCardsOnBackBoard) =>
                                    new ViewModel.WIPDoneViewModel(board,
                                                         cardWorkState,
                                                         cardFilter,
                                                         loadCardsOnBackBoard,
                                                         DIContainer.Resolve<ViewModel.CardFactory>(),
                                                         DIContainer.Resolve<ViewModel.IViewModelProperties>(),
                                                         DIContainer.Resolve<Model.ICardRepository>()
                                                         )))
            .RegisterInstance(new ViewModel.ProcessStepViewModelFactory((board, processStep) => ResolveProcessStepViewModel(board, processStep)));
    }

    private async Task<ViewModel.Card> CreateCard(ViewModel.Board board, Model.WorkState cardWorkState, ViewModel.Card srcCard)
    {
        Model.ICardFactory cardFactory = DIContainer.Resolve<Model.ICardFactory>();
        Model.Card card = await cardFactory.CreateCard(board._id, cardWorkState, srcCard?.CardModel);
        return new ViewModel.Card(card, board.ShowDetailedCards);
    }

    private ViewModel.ProcessStepViewModel ResolveProcessStepViewModel(ViewModel.Board board, Repository.ProcessStep processStep)
    {
        return DIContainer.Resolve<ViewModel.ProcessStepViewModel>(
                    new ParameterOverride("board", board),
                    new ParameterOverride("step", processStep),
                    new ParameterOverride("boardsViewModelFactory", DIContainer.Resolve<ViewModel.WIPDoneViewModelFactory>()));
    }
}

class DI4Model : DIBase
{
    internal DI4Model(IUnityContainer DIContainer) : base(DIContainer) { }

    internal void RegisterDependencies()
    {
        Model.Board.BoardRepository = DIContainer.Resolve<Model.IBoardRepository>();
        Model.Card.CardRepository = DIContainer.Resolve<Model.ICardRepository>();
        Model.Activity.ActivityRepository = DIContainer.Resolve<Model.IActivityRepository>();
        Model.DevProcess.Localization = DIContainer.Resolve<Util.ILocalization>();
    }
}

class DI4Repository : DIBase
{
    internal DI4Repository(IUnityContainer DIContainer) : base(DIContainer) { }

    internal void RegisterDependencies()
    {
        DIContainer
            .RegisterType<Repository.IDBClient, Repository.DBClient>(new ContainerControlledLifetimeManager())
            .RegisterType<Repository.IDBActivity, Repository.DBActivities>(new ContainerControlledLifetimeManager())
            .RegisterType<Repository.IDBCard, Repository.DBCards>(new ContainerControlledLifetimeManager())
            .RegisterType<Repository.IDBBoard, Repository.DBBoards>(new ContainerControlledLifetimeManager());
    }
}

class DI4Infrastructure : DIBase
{
    internal DI4Infrastructure(IUnityContainer DIContainer) : base(DIContainer) { }

    internal void RegisterDependencies()
    {
        DIContainer
            .RegisterInstance(new ViewModel.DBBackupFactory(client =>
                    new Infrastructure.DBBackup(DIContainer.Resolve<Infrastructure.ProcessExecutorFactory>(),
                                 client,
                                 DIContainer.Resolve<Repository.IDBClient>())));
        DIContainer
            .RegisterType<Util.IAppSettings, Infrastructure.AppSettings>(new ContainerControlledLifetimeManager())
            .RegisterType<Util.ILocalization, Kanban.Localization>(new ContainerControlledLifetimeManager())
            .RegisterType<Infrastructure.IProcessExecutor, Infrastructure.ProcessExecutor>()
            .RegisterInstance(new Infrastructure.ProcessExecutorFactory(() => DIContainer.Resolve<Infrastructure.IProcessExecutor>()))
            .RegisterType<Model.IBoardRepository, Infrastructure.BoardRepository>(new ContainerControlledLifetimeManager())
            .RegisterType<Model.IBoardFactory, Infrastructure.BoardFactory>(new ContainerControlledLifetimeManager())
            .RegisterType<Model.ICardFactory, Infrastructure.CardFactory>(new ContainerControlledLifetimeManager())
            .RegisterType<Model.ICardRepository, Infrastructure.CardRepository>(new ContainerControlledLifetimeManager())
            .RegisterType<Model.IActivityRepository, Infrastructure.ActivityRepository>(new ContainerControlledLifetimeManager());
    }
}