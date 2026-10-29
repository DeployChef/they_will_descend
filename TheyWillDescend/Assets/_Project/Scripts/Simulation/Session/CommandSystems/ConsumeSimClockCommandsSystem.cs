using Unity.Collections;
using Unity.Entities;

namespace TheyWillDescend.Simulation.Session
{
    [UpdateInGroup(typeof(CommandSystemGroup), OrderFirst = true)]
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
                case SimClockCommandKind.TogglePlayerPause:
                case SimClockCommandKind.ToggleTimePause:
                    if (control.BuildLocked != 0)
                        return;
                    control.TimePaused = control.TimePaused == 0 ? (byte)1 : (byte)0;
                    break;
                case SimClockCommandKind.SetPlayerPause:
                    if (control.BuildLocked != 0)
                        return;
                    control.PlayerPaused = command.Value != 0 ? (byte)1 : (byte)0;
                    break;
                case SimClockCommandKind.SetTimePause:
                    if (control.BuildLocked != 0)
                        return;
                    control.TimePaused = command.Value != 0 ? (byte)1 : (byte)0;
                    break;
                case SimClockCommandKind.SetSpeed:
                    if (control.BuildLocked != 0)
                        return;
                    control.Speed = ClampSpeed(command.Value);
                    control.TimePaused = 0;
                    break;
                case SimClockCommandKind.SetBuildLocked:
                    control.BuildLocked = command.Value != 0 ? (byte)1 : (byte)0;
                    break;
                case SimClockCommandKind.Restore:
                    control.Speed = ClampSpeed(command.Value);
                    control.TimePaused = command.Secondary != 0 ? (byte)1 : (byte)0;
                    control.PlayerPaused = 0;
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
            control.Mode = control.TimePaused != 0
                || control.PlayerPaused != 0
                || control.BuildLocked != 0
                ? SimRunMode.Frozen
                : SimRunMode.Running;
        }
    }
}
