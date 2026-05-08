namespace SqlDatabaseDump;

using System;
using PicoArgs_dotnet;
using IniParser;
using IniParser.Model;

internal static class Program
{
	private const int DefaultMaxParallel = 8;

	private static void Main(string[] args)
	{
		AppDomain.CurrentDomain.UnhandledException += App_UnhandledException;

		var ver = GitVersion.VersionInfo.Get();
		Console.WriteLine($"SqlDatabaseDump {ver.GetVersionHash(12)}");

		var config = BuildConfig(args);

		Console.WriteLine(
			$"Dumping '{config.DatabaseName}' from '{config.InstanceName}' into '{config.OutputDirectory}'");
		if (config.SingleThread) {
			Console.WriteLine("Single thread processing");
		}

		if (config.ReplaceExistingFiles) {
			Console.WriteLine("Replacing existing files");
		}

		if (config.ExtendedProperties) {
			Console.WriteLine("Including extended properties");
			Shared.WithExtendedProperties();
		}

		if (config.WithDependencies) {
			Console.WriteLine("Including dependencies");
			Shared.WithDependencies();
		}

		var stopwatch = System.Diagnostics.Stopwatch.StartNew();

		using var cancellationToken = new CancellationTokenSource();
		var types = Enum.GetValues<Scriptable>();

		if (config.SingleThread || config.MaxParallel == 1) {
			// run in sequence
			SequentialProcess(types, config, cancellationToken);
		} else {
			// run in parallel
			ParallelProcess(types, config, cancellationToken);
		}

		if (!config.SkipErrors) {
			WriteAnyErrors(config);
		}

		ReferenceTableDumper.Dump(config);

		stopwatch.Stop();
		var seconds = Convert.ToDouble(stopwatch.ElapsedMilliseconds) / 1000.0;

		Console.WriteLine(
			$"Items found: {Shared.MaxCounter.Value}, files written: {Shared.WrittenCounter.Value}, errors: {Shared.ErrorObjects.Count}, remaining: {Shared.QueueCounter.Value}");
		Console.WriteLine($"Execution Time: {seconds:f1} secs");
	}

	private static void SequentialProcess(IEnumerable<Scriptable> types, Config config,
		CancellationTokenSource cancellationToken)
	{
		foreach (var type in types) {
			Console.WriteLine($"Starting {type}...");
			var dumper = new DumpDb(config, type, cancellationToken);
			dumper.Run();
		}
	}

	private static void ParallelProcess(IEnumerable<Scriptable> types, Config config,
		CancellationTokenSource cancellationToken)
	{
		try {
			_ = Parallel.ForEach(types,
				new ParallelOptions {
					CancellationToken = cancellationToken.Token, MaxDegreeOfParallelism = config.MaxParallel
				}, type => {
					ThreadsafeWrite.Write($"Starting {type}...");

					var dumper = new DumpDb(config, type, cancellationToken);
					dumper.Run();

					ThreadsafeWrite.Write($"Finished {type}.");
				});
		}
		catch (AggregateException ae) {
			// handle exceptions from Parallel.ForEach, but ignore OperationCancelledException, they are just a side-effect
			foreach (var e in ae.InnerExceptions) {
				if (e != ae.InnerException && e is not OperationCanceledException) {
					// display significant inner exceptions
					Console.WriteLine($"INNER ERROR: {e.Message}");
				}
			}

			// rethrow either the first inner exception (true error) or the AggregateException as a fallback
			if (ae.InnerException != null) {
				throw ae.InnerException;
			}

			throw;
		}
	}

	/// <summary>
	/// Global exception handler (for unhandled exceptions)
	/// </summary>
	private static void App_UnhandledException(object sender, UnhandledExceptionEventArgs e)
	{
		Console.WriteLine();
		if (e.ExceptionObject is Exception ex) {
			Console.WriteLine($"ERROR: {ex.Message}");
		} else {
			Console.WriteLine($"ERROR value: {0}", e.ExceptionObject?.ToString() ?? "?");
		}

		Console.WriteLine();
		Console.WriteLine(CommandLineMessage);
		Environment.Exit(1);
	}

	private static Config BuildConfig(string[] args)
	{
		var pico = new PicoArgs(args);

		// handle help
		if (pico.Contains("-h", "--help", "-?")) {
			Console.WriteLine(CommandLineMessage);
			Environment.Exit(0);
		}

		// parse command line parameters
		var config_file = pico.GetParamOpt("-c", "--config");

		if (!string.IsNullOrWhiteSpace(config_file)) {
			var config = Load(config_file);
			return config;
		}

		var server = pico.GetParamOpt("-s", "--server") ?? Environment.GetEnvironmentVariable("DB_SERVER");

		var login = pico.GetParamOpt("-u", "--username") ?? Environment.GetEnvironmentVariable("DB_USERNAME");
		var password = pico.GetParamOpt("-p", "--password") ?? Environment.GetEnvironmentVariable("DB_PASSWORD");
		var referenceTablesStringList = pico.GetParamOpt("-t", "--reference-tables") ??
		                                Environment.GetEnvironmentVariable("DB_REFTABLES");

		var database = pico.GetParamOpt("-d", "--database") ?? Environment.GetEnvironmentVariable("DB_DATABASE");
		var dir = pico.GetParamOpt("-o", "--dir") ?? Environment.GetEnvironmentVariable("DB_DIR");
		var maxParallel = ParseOrDefault(pico.GetParamOpt("-p", "--parallel"), DefaultMaxParallel);

		var allExtras = pico.Contains("-a", "--all");
		var extendedProperties = pico.Contains("-e", "--extended-properties");
		var withDependencies = pico.Contains("-w", "--with-dependencies");

		var singleThread = pico.Contains("-s", "--single-thread");
		var replace = pico.Contains("-r", "--replace");
		var skipErrors = pico.Contains("-k", "--skip-errors");

		pico.Finished();

		if (string.IsNullOrWhiteSpace(referenceTablesStringList)) {
			referenceTablesStringList = "";
		}


		// ensure required parameters are present
		if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(database) ||
		    string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password) ||
		    string.IsNullOrWhiteSpace(dir) || maxParallel < 1 || maxParallel > 16) {
			Console.WriteLine(CommandLineMessage);
			Environment.Exit(1);
		}

		var referenceTables = referenceTablesStringList.Split(",").ToList();

		dir = DumpDb.EnsurePathExists(dir);

		return new Config(server, login, password, database, dir, maxParallel,
			singleThread, replace, skipErrors, extendedProperties || allExtras, withDependencies || allExtras,
			referenceTables
		);
	}

	private static Config Load(string configFile)
	{ 
		
		Console.WriteLine($"Leyendo {configFile}");

		var parser = new IniDataParser();

		string fileContent = File.ReadAllText(configFile);
		IniData data = parser.Parse(fileContent);
		
		var server = data.Global["Server"];
		var login = data.Global["Login"];
		var password = data.Global["Password"];
		var database = data.Global["Database"];
		var dir = data.Global["OutputDirectory"];
		var referenceTablesStringList = data.Global["ReferenceTables"] ?? "";
		
		var maxParallel = data.Global["MaxParallel"] != null ? int.Parse(data.Global["MaxParallel"]) : DefaultMaxParallel;
		var singleThread = data.Global["SingleThread"] != null && bool.Parse(data.Global["SingleThread"]);
		var replace = data.Global["ReplaceExistingFiles"] != null && bool.Parse(data.Global["ReplaceExistingFiles"]);
		var skipErrors = data.Global["SkipErrors"] != null && bool.Parse(data.Global["SkipErrors"]);
		
		var extendedProperties = data.Global["ExtendedProperties"] != null&& bool.Parse(data.Global["ExtendedProperties"]);
		var withDependencies = data.Global["WithDependencies"] != null && bool.Parse(data.Global["WithDependencies"]);
		var allExtras = data.Global["AllExtras"] != null && bool.Parse(data.Global["AllExtras"]);

		Console.WriteLine($"Configuración cargada para {server}/{database} en {dir}");

		// ensure required parameters are present
		if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(database) ||
		    string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password) ||
		    string.IsNullOrWhiteSpace(dir) || maxParallel < 1 || maxParallel > 16) {
			Console.WriteLine($"Error reading config file {configFile}");
			Environment.Exit(1);
		}

		var referenceTables = referenceTablesStringList.Split(",").ToList();
		dir = DumpDb.EnsurePathExists(dir);


		return new Config(server, login, password, database, dir, maxParallel,
			singleThread, replace, skipErrors, extendedProperties || allExtras, withDependencies || allExtras,
			referenceTables
		);
	}

	private static void WriteAnyErrors(Config config)
	{
		if (Shared.ErrorObjects.IsEmpty) {
			return;
		}

		// sort the concurrent bag
		IReadOnlyList<string> items = [.. Shared.ErrorObjects.AsEnumerable().Order()];

		var filename = $"{config.OutputDirectory}{config.DatabaseName}-Errors.TXT";
		using var wr = new StreamWriter(filename);

		wr.WriteLine($"Objects with errors in {config.DatabaseName} {DateTime.Now}:");
		wr.WriteLine();

		foreach (var s in items) {
			wr.WriteLine(s);
		}

		wr.Close();
	}

	private static int ParseOrDefault(string? value, int defaultValue) =>
		int.TryParse(value, out var result) ? result : defaultValue;

	private const string CommandLineMessage = """
	                                          Usage: SqlDatabaseDump.exe --server <server> -u <user> -p <password> --database <db> --dir <dir>

	                                          Required:
	                                            -c  --config <ini>         Config file
	                                          Or:
	                                            -s, --server <server>      SQL Server to connect to           (or DB_SERVER environment variable)
	                                            -u, --username <login>     Username to login                  (or DB_USERNAME environment variable)
	                                            -p, --password <pass>      Password for login                 (or DB_PASSWORD environment variable)
	                                            -d, --database <db>        Database to process                (or DB_DATABASE environment variable)
	                                            -o, --dir <dir>            Output directory                   (or DB_DIR environment variable)

	                                          Options:
	                                            -r, --reference-tables <table1,table2>  Reference tables to include (comma separated)
	                                            -e, --extended-properties               Include extended properties
	                                            -w, --with-dependencies                 Include dependencies
	                                            -a, --all                               Include all extras (extended properties and dependencies)

	                                            -r, --replace                           Replace existing files (default is to fail if file exists)
	                                            -s, --single-thread                     Single thread processing
	                                            -p, --parallel <n>                      Maximum parallel tasks 1..16 (default is 8)
	                                            -k, --skip-errors                       Skip errors without writing to file
	                                            -h, --help, -?                          Help information
	                                          """;
}