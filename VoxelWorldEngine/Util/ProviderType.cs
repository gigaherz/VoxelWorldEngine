using VoxelWorldEngine.Util.Providers;

namespace VoxelWorldEngine.Util
{
    public static class ProviderType
    {
        public static ProviderType<ValueProvider3D<double>> RAW_DENSITY =
            new ProviderType<ValueProvider3D<double>>("raw_density");
        public static ProviderType<ValueProvider3D<double>> DENSITY =
            new ProviderType<ValueProvider3D<double>>("density");
        public static ProviderType<ValueProvider2D<(double, double, double)>> TOPOLOGY =
            new ProviderType<ValueProvider2D<(double, double, double)>>("topology");
    }

    public class ProviderType<T>
        where T: IValueProvider
    {
        public ProviderType(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }
}