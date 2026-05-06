namespace SqlDatabaseDump;

using System.Collections.Specialized;
using System.Text.RegularExpressions;
using Microsoft.SqlServer.Management.Smo;

/// <summary>
/// Types of db objects that can be scripted
/// </summary>
internal enum Scriptable
{
	// order is important here because each spawns a task and can run in parallel
	// start with the big 4: tables, views, stored procedures, user defined functions
	Tables,
	Views,
	StoredProcedures,
	UserDefinedFunctions,

	// then smaller items like schemas and roles
	Schemas,
	Roles,
	DatabaseTriggers,

	// then the last bits, frequently empty
	Sequences,
	UserDefinedDataTypes,
	UserDefinedTypes,
	Rules,
	Synonyms
}

/// <summary>
/// A wrapper class for database objects to allow polymorphic processing
/// </summary>
[System.Diagnostics.DebuggerDisplay("{FullName,nq}")]
internal sealed partial class ScriptableObject
{
	private IScriptable Scriptable { get; }

	private readonly ScriptingOptions Options;

	private string? Schema { get; }

	private string Ext { get; }

	private string? OverrideFilename { get; }

	public string Name { get; }

	/// <summary>
	/// Override the full name, or [schema.]name.ext
	/// </summary>
	public string FullName => OverrideFilename ?? $"{Ext}/" + $"{Schema}.{Name}.sql".TrimStart('.');

	public string DirName => (OverrideFilename != null) ? "" : $"{Ext}".TrimStart('.');

	public override string ToString() => FullName;

	/// <summary>
	/// Constructor for general scriptable objects
	/// </summary>
	public ScriptableObject(IScriptable script, string? schema, string name, string extension, ScriptingOptions options)
	{
		Scriptable = script;
		this.Schema = schema != null ? schema.StartsWith("dbo") ? schema.Substring(3) : schema : null;

		Name = name.Replace('\\', '-');
		Ext = extension;
		Options = options;
	}

	/// <summary>
	/// Constructor for database settings
	/// </summary>
	public ScriptableObject(Database db, string databaseName)
	{
		Scriptable = db;
		OverrideFilename = $"{databaseName}-Settings.TXT";
		Schema = null;
		Name = "database settings";
		Ext = string.Empty;
		Options = Shared.ScriptOptionsMinimal;
	}

	/// <summary>
	/// Script the object, and any nested sub-scripts
	/// </summary>
	public IEnumerable<string> Script()
	{
		var main = Scriptable.Script(Options);

		foreach (var s in main) {
			if (string.IsNullOrWhiteSpace(s)) {
				continue;
			}

			yield return s;
			yield return "GO";
			yield return string.Empty;
		}
	}
}