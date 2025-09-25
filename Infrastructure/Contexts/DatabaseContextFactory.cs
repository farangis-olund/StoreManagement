using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Infrastructure.Contexts
{
	public class DatabaseContextFactory : IDesignTimeDbContextFactory<DatabaseContext>
	{
		public DatabaseContext CreateDbContext(string[] args)
		{
			var optionsBuilder = new DbContextOptionsBuilder<DatabaseContext>();

			// ⚠️ Use your connection string (same as in App.xaml.cs)
			optionsBuilder.UseSqlServer(
				"Data Source=(LocalDB)\\MSSQLLocalDB;AttachDbFilename=D:\\projects\\StoreManagementSoftware-main\\Infrastructure\\Data\\Database.mdf;Integrated Security=True");

			return new DatabaseContext(optionsBuilder.Options);
		}
	}
}