namespace TheyWillDescend.Shell
{
    public interface IAppState
    {
        AppStateId Id { get; }
        void Enter();
        void Exit();
    }
}
