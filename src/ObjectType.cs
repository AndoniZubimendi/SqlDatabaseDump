namespace SqlDatabaseDump;

public static class ObjectType
{
	public const string Table = "Table";
	public const string View = "View";
	public const string StoredProcedure = "StoredProcedure";
	public const string Function = "Function";
	public const string Role = "Role";
	public const string Rule = "Rule";
	public const string Trigger = "Trigger";
	public const string UserDefinedType = "UserDefinedType";
	public const string Schema = "Schema";
	public const string UserDefinedDataType = "UserDefinedDataType";
	public const string Sequence = "Sequence";
	public const string Synonym = "Synonym";
	// public const string Other = "Other";

	public static string directoryName(string objectType)
	{
		switch (objectType) {
			case Table: return "tables";
			case View: return "views";
			case StoredProcedure: return "stores";
			case Function: return "functions";
			case Role: return "roles";
			case Rule: return "rules";
			case Trigger: return "triggers";
			case UserDefinedType: return "udts";
			case Schema: return "schemas";
			case UserDefinedDataType: return "udt";
			case Sequence: return "sequences";
			case Synonym: return "synonyms";
		}

		return objectType;
	}
}