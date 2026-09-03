namespace TheyWillDescend.Simulation.City
{
    public static class ConstructionCrew
    {
        public const int DefaultSlots = 10;

        public static int ResolveSlots(int slots) => slots < 1 ? DefaultSlots : slots;
    }
}
