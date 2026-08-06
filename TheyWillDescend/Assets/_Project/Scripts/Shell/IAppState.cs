namespace _Project.Scripts.Shell
{
    public interface IAppState
    {
        AppStateId Id { get; }
        void Enter();
        void Exit();
        void Tick();
    }
}
