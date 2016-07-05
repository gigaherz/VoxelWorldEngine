namespace VoxelWorldEngine.Registry
{
    public class RegistrableObject<T>
        where T : RegistrableObject<T>
    {
        public ObjectKey Key { get; }

        protected RegistrableObject(string domain, string name)
        {
            Key = new ObjectKey(domain, name);
            RegistryManager.GetRegistry<T>().Register((T)this);
        } 
    }
}