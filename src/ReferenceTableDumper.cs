namespace SqlDatabaseDump;

using Microsoft.SqlServer.Management.Smo;

internal static class ReferenceTableDumper
{
	public static void Dump(Config config)
	{
		List<string> tableNames = config.ReferenceTables;

		if (tableNames.Count == 0) {
			return;
		}

		var theServer = new Server(config.InstanceName) {
			ConnectionContext = {
				LoginSecure = false, // False = SQL Authentication
				Login = config.Login,
				Password = config.Password
			}
		};

		theServer.ConnectionContext.Connect();

		var myDb = theServer.Databases[config.DatabaseName];

		if (myDb == null) {
			throw new InvalidOperationException(
				$"Database '{config.DatabaseName}' not found on '{config.InstanceName}'");
		}


		// Define the scripting options correctly
		var options = new ScriptingOptions {
			ScriptData = true, // Enable data generation
			ScriptSchema = false, // Set to false if you ONLY want INSERTs
			IncludeHeaders = true,
			ToFileOnly = false
		};

//		Scripter scripter = new Scripter(theServer);
//		scripter.Options.ScriptData = true; // Script the data
//		scripter.Options.ScriptSchema = false; // Only data, no CREATE TABLE
//		scripter.Options.IncludeHeaders = true;

		var dirname = Path.Combine(config.OutputDirectory, "references");
		if (!Directory.Exists(dirname)) {
			_ = Directory.CreateDirectory(dirname);
		}

		foreach (var tableName in tableNames) {
			var tbl = myDb.Tables[tableName];
			if (tbl == null) {
				continue;
			}

			// CRITICAL: Use tbl.EnumScript instead of scripter.Script
			var scriptLines = tbl.EnumScript(options);

			var filePath = Path.Combine(dirname, $"{tableName}.sql");

			File.WriteAllLines(filePath, scriptLines);
			Console.WriteLine($"Exported {tableName} to {filePath}");
		}
	}
}