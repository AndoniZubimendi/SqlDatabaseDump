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

		var theServer = new Server(config.InstanceName);

		theServer.ConnectionContext.LoginSecure = false; // False = SQL Authentication
		theServer.ConnectionContext.Login = config.Login;
		theServer.ConnectionContext.Password = config.Password;

		theServer.ConnectionContext.Connect();

		var myDB = theServer.Databases[config.DatabaseName];

		if (myDB == null) {
			throw new InvalidOperationException(
				$"Database '{config.DatabaseName}' not found on '{config.InstanceName}'");
		}


		// Define the scripting options correctly
		ScriptingOptions options = new ScriptingOptions {
			ScriptData = true, // Enable data generation
			ScriptSchema = false, // Set to false if you ONLY want INSERTs
			IncludeHeaders = true,
			ToFileOnly = false
		};

//		Scripter scripter = new Scripter(theServer);
//		scripter.Options.ScriptData = true; // Script the data
//		scripter.Options.ScriptSchema = false; // Only data, no CREATE TABLE
//		scripter.Options.IncludeHeaders = true;

		foreach (string tableName in tableNames) {
			Table tbl = myDB.Tables[tableName];
			if (tbl == null) continue;

			// CRITICAL: Use tbl.EnumScript instead of scripter.Script
			var scriptLines = tbl.EnumScript(options);

			string filePath = Path.Combine(config.OutputDirectory, "references", $"{tableName}_data.sql");
			File.WriteAllLines(filePath, scriptLines);
			Console.WriteLine($"Exported {tableName} to {filePath}");
		}
	}
}