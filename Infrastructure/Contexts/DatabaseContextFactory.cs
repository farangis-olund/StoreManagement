using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Infrastructure.Contexts
{
    public class DatabaseContextFactory : IDesignTimeDbContextFactory<DatabaseContext>
    {
        public DatabaseContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<DatabaseContext>();
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dataDir = Path.Combine(local, "StoreManagement", "StoreManagementApp", "Data");
            Directory.CreateDirectory(dataDir);

            var dbPath = Path.Combine(dataDir, "DataBase.mdf");

            var connectionString =
                $@"Data Source=(LocalDB)\MSSQLLocalDB;
               AttachDbFilename={dbPath};
               Initial Catalog=StoreManagementDatabase;
               Integrated Security=True;
               MultipleActiveResultSets=True;
               Connect Timeout=30";


            //var dbPath = Path.Combine(
            //    Directory.GetCurrentDirectory(),
            //    "Data",
            //    "DataBase.mdf");

            //var connectionString =
            //    $@"Data Source=(LocalDB)\MSSQLLocalDB;
            //   AttachDbFilename={dbPath};
            //   Integrated Security=True;
            //   MultipleActiveResultSets=True;
            //   Connect Timeout=30";



            optionsBuilder.UseSqlServer(connectionString);

            return new DatabaseContext(optionsBuilder.Options);
        }
    }
}