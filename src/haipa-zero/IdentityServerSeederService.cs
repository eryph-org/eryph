namespace Haipa.Runtime.Zero
{
    using Haipa.IdentityDb;
    using Haipa.Modules.Identity.Seeder;

    /// <summary>
    /// Defines the <see cref="IIdentityServerSeederService" />
    /// </summary>
    public interface IIdentityServerSeederService
    {
        /// <summary>
        /// The Seed
        /// </summary>
        void Seed();
    }

    /// <summary>
    /// Defines the <see cref="IdentityServerSeederService" />
    /// </summary>
    public class IdentityServerSeederService : IIdentityServerSeederService
    {
        /// <summary>
        /// Defines the _db
        /// </summary>
        private readonly ConfigurationStoreContext _db;

        /// <summary>
        /// Initializes a new instance of the <see cref="IdentityServerSeederService"/> class.
        /// </summary>
        /// <param name="context">The context<see cref="ConfigurationStoreContext"/></param>
        public IdentityServerSeederService(ConfigurationStoreContext context)
        {
            _db = context;
        }

        /// <summary>
        /// The Seed
        /// </summary>
        public void Seed()
        {
            _db.AddRange(Config.GetClients());
            _db.AddRange(Config.GetIdentityResources());
            _db.AddRange(Config.GetApiResources());
            _db.SaveChanges();
        }
    }
}
