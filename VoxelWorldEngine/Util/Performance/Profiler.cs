namespace VoxelWorldEngine.Util.Performance
{
    public static class Profiler
    {
        public static IProfiler CurrentProfiler { get; set; }
#if DEBUG
                = new BasicProfiler();
#else
                = new DummyProfiler();
#endif
    }
}
