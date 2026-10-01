namespace TheyWillDescend.Shell
{
    /// <summary>
    /// App-lifetime facts that must survive scene loads. Not the simulation session.
    /// One instance on the root scope.
    /// </summary>
    public sealed class AppContext
    {
        public bool IsFirstStart { get; set; }
    }
}
