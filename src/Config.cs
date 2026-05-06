namespace SqlDatabaseDump;

internal sealed record class Config(
	string InstanceName,
	string Login,
	string Password,
	string DatabaseName,
	string OutputDirectory,
	int MaxParallel,
	bool SingleThread,
	bool ReplaceExistingFiles,
	bool SkipErrors,
	bool ExtendedProperties,
	bool WithDependencies);