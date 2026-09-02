using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Simulation.Session
{
    [UpdateInGroup(typeof(CommandSystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(TheyWillDescend.Simulation.Agents.ConsumeDespawnAgentsSystem))]
    public partial struct ConsumeSimClockCommandsSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimSession>();
            state.RequireForUpdate<SimClockCommand>();
        }

        public void OnUpdate(ref SystemState state) => Run(state.EntityManager);

        public static void Run(EntityManager em)
        {
            if (!SimSessionAccess.TryGet(em, out var session) || !em.HasBuffer<SimClockCommand>(session))
                return;

            var commands = em.GetBuffer<SimClockCommand>(session);
            if (commands.Length == 0)
                return;

            var copy = commands.ToNativeArray(Allocator.Temp);
            commands.Clear();
            var control = em.GetComponentData<SimControl>(session);
            for (var i = 0; i < copy.Length; i++)
                Apply(ref control, copy[i]);
            copy.Dispose();
            RefreshMode(ref control);
            em.SetComponentData(session, control);
        }

        static void Apply(ref SimControl control, in SimClockCommand command)
        {
            switch (command.Kind)
            {
                case SimClockCommandKind.SetSessionInGame:
                    control.SessionInGame = command.Value != 0 ? (byte)1 : (byte)0;
                    control.BuildLocked = 0;
                    if (control.SessionInGame == 0)
                        control.PlayerPaused = 0;
                    break;
                case SimClockCommandKind.TogglePlayerPause:
                    if (control.SessionInGame == 0 || control.BuildLocked != 0)
                        return;
                    control.PlayerPaused = control.PlayerPaused == 0 ? (byte)1 : (byte)0;
                    break;
                case SimClockCommandKind.SetPlayerPause:
                    if (control.SessionInGame == 0 || control.BuildLocked != 0)
                        return;
                    control.PlayerPaused = command.Value != 0 ? (byte)1 : (byte)0;
                    break;
                case SimClockCommandKind.SetSpeed:
                    if (control.SessionInGame == 0 || control.BuildLocked != 0)
                        return;
                    control.Speed = ClampSpeed(command.Value);
                    control.PlayerPaused = 0;
                    break;
                case SimClockCommandKind.SetBuildLocked:
                    if (command.Value != 0 && control.SessionInGame == 0)
                        return;
                    control.BuildLocked = command.Value != 0 ? (byte)1 : (byte)0;
                    break;
                case SimClockCommandKind.Restore:
                    control.Speed = ClampSpeed(command.Value);
                    control.PlayerPaused = command.Secondary != 0 ? (byte)1 : (byte)0;
                    break;
            }
        }

        static int ClampSpeed(int speed)
        {
            if (speed < 1)
                return 1;
            if (speed > 3)
                return 3;
            return speed;
        }

        static void RefreshMode(ref SimControl control)
        {
            if (control.SessionInGame == 0)
            {
                control.Mode = SimRunMode.Off;
                return;
            }

            control.Mode = control.PlayerPaused != 0 || control.BuildLocked != 0
                ? SimRunMode.Frozen
                : SimRunMode.Running;
        }
    }
}
