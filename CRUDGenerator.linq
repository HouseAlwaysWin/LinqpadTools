<Query Kind="Program">
  <NuGetReference>Dapper</NuGetReference>
  <NuGetReference>System.Data.SqlClient</NuGetReference>
  <Namespace>System.Data.SqlClient</Namespace>
  <Namespace>Dapper</Namespace>
</Query>


void Main()
{
	try
	{
		using (var conn = new SqlConnection("Data Source=localhost;Initial Catalog=nopCommerce;Integrated Security=true"))
		{
			conn.GenerateCRUD("dbo.Country").Dump();
			//conn.GenerateAllTables().Dump();
			//conn.GenerateClass("SELECT * FROM dbo.Country").Dump();
			//conn.Query("SELECT * FROM dbo.Country").Dump();
		}
	}
	catch (SqlException ex)
	{
		Console.WriteLine(ex.ToString());
	}
	catch (Exception ex)
	{
		Console.WriteLine(ex.ToString());
	}
}

[Flags]
public enum GeneratorBehavior
{
	Default = 0x0,
	View = 0x1,
	DapperContrib = 0x2,
	Comment = 0x4
}



public static class PocoClassGenerator
{
	#region Property
	private static readonly Dictionary<Type, string> TypeAliases = new Dictionary<Type, string> {
			   { typeof(int), "int" },
			   { typeof(short), "short" },
			   { typeof(byte), "byte" },
			   { typeof(byte[]), "byte[]" },
			   { typeof(long), "long" },
			   { typeof(double), "double" },
			   { typeof(decimal), "decimal" },
			   { typeof(float), "float" },
			   { typeof(bool), "bool" },
			   { typeof(string), "string" }
	   };

	private static readonly Dictionary<string, string> QuerySqls = new Dictionary<string, string> {
			   {"sqlconnection", "select  *  from [{0}] where 1=2" },
			   {"sqlceserver", "select  *  from [{0}] where 1=2" },
			   {"sqliteconnection", "select  *  from [{0}] where 1=2" },
			   {"oracleconnection", "select  *  from \"{0}\" where 1=2" },
			   {"mysqlconnection", "select  *  from `{0}` where 1=2" },
			   {"npgsqlconnection", "select  *  from \"{0}\" where 1=2" }
	   };

	private static readonly Dictionary<string, string> TableSchemaSqls = new Dictionary<string, string> {
			   {"sqlconnection", "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERe TABLE_TYPE = 'BASE TABLE'" },
			   {"sqlceserver", "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES  WHERE TABLE_TYPE = 'BASE TABLE'" },
			   {"sqliteconnection", "SELECT NAME FROM SQLITE_MASTER WHERE TYPE = 'TABLE'" },
			   {"oracleconnection", "SELECT TABLE_NAME from USER_TABLES WHERE TABLE_NAME NOT IN (SELECT View_NAME FROM USER_VIEWS)" },
			   {"mysqlconnection", "SELECT TABLE_NAME FROM  INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'" },
			   {"npgsqlconnection", "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'" }
	   };


	private static readonly HashSet<Type> NullableTypes = new HashSet<Type> {
			   typeof(int),
			   typeof(short),
			   typeof(long),
			   typeof(double),
			   typeof(decimal),
			   typeof(float),
			   typeof(bool),
			   typeof(DateTime)
	   };
	#endregion

	public static string GenerateAllTables(this System.Data.Common.DbConnection connection, GeneratorBehavior generatorBehavior = GeneratorBehavior.Default)
	{
		if (connection.State != ConnectionState.Open) connection.Open();

		var conneciontName = connection.GetType().Name.ToLower();
		var tables = new List<string>();
		var sql = generatorBehavior.HasFlag(GeneratorBehavior.View) ? TableSchemaSqls[conneciontName].Split("where")[0] : TableSchemaSqls[conneciontName];
		using (var command = connection.CreateCommand(sql))
		using (var reader = command.ExecuteReader())
		{
			while (reader.Read())
				tables.Add(reader.GetString(0));
		}

		var sb = new StringBuilder();
		sb.AppendLine("namespace Models { ");
		tables.ForEach(table => sb.Append(connection.GenerateClass(
			   string.Format(QuerySqls[conneciontName], table), table, generatorBehavior: generatorBehavior
		)));
		sb.AppendLine("}");
		return sb.ToString();
	}

	public static string GenerateClass(this IDbConnection connection, string sql, GeneratorBehavior generatorBehavior)
		 => connection.GenerateClass(sql, null, generatorBehavior);

	public static string GenerateClass(this IDbConnection connection, string sql, string className = null, GeneratorBehavior generatorBehavior = GeneratorBehavior.Default)
	{
		if (connection.State != ConnectionState.Open) connection.Open();

		var builder = new StringBuilder();

		//Get Table Name
		//Fix : [When View using CommandBehavior.KeyInfo will get duplicate columns ¡P Issue #8 ¡P shps951023/PocoClassGenerator](https://github.com/shps951023/PocoClassGenerator/issues/8 )
		var isFromMutiTables = false;
		using (var command = connection.CreateCommand(sql))
		using (var reader = command.ExecuteReader(CommandBehavior.KeyInfo | CommandBehavior.SingleRow))
		{
			var tables = reader.GetSchemaTable().Select().Select(s => s["BaseTableName"] as string).Distinct();
			var tableName = string.IsNullOrWhiteSpace(className) ? tables.First() ?? "Info" : className;

			isFromMutiTables = tables.Count() > 1;

			if (generatorBehavior.HasFlag(GeneratorBehavior.DapperContrib) && !isFromMutiTables)
				builder.AppendFormat("	[Dapper.Contrib.Extensions.Table(\"{0}\")]{1}", tableName, Environment.NewLine);
			builder.AppendFormat("	public class {0}{1}", tableName.Replace(" ", ""), Environment.NewLine);
			builder.AppendLine("	{");
		}

		//Get Columns 
		var behavior = isFromMutiTables ? (CommandBehavior.SchemaOnly | CommandBehavior.SingleRow) : (CommandBehavior.KeyInfo | CommandBehavior.SingleRow);

		using (var command = connection.CreateCommand(sql))
		using (var reader = command.ExecuteReader(behavior))
		{
			do
			{
				var schema = reader.GetSchemaTable();
				foreach (DataRow row in schema.Rows)
				{
					var type = (Type)row["DataType"];
					var name = TypeAliases.ContainsKey(type) ? TypeAliases[type] : type.FullName;
					var isNullable = (bool)row["AllowDBNull"] && NullableTypes.Contains(type);
					var collumnName = (string)row["ColumnName"];

					if (generatorBehavior.HasFlag(GeneratorBehavior.Comment) && !isFromMutiTables)
					{
						var comments = new[] { "DataTypeName", "IsUnique", "IsKey", "IsAutoIncrement", "IsReadOnly" }
							   .Select(s =>
							   {
								   if (row[s] is bool && ((bool)row[s]))
									   return s;
								   if (row[s] is string && !string.IsNullOrWhiteSpace((string)row[s]))
									   return string.Format(" {0} : {1} ", s, row[s]);
								   return null;
							   }).Where(w => w != null).ToArray();
						var sComment = string.Join(" , ", comments);

						builder.AppendFormat("		/// <summary>{0}</summary>{1}", sComment, Environment.NewLine);
					}

					if (generatorBehavior.HasFlag(GeneratorBehavior.DapperContrib) && !isFromMutiTables)
					{
						var isKey = (bool)row["IsKey"];
						var isAutoIncrement = (bool)row["IsAutoIncrement"];
						if (isKey && isAutoIncrement)
							builder.AppendLine("		[Dapper.Contrib.Extensions.Key]");
						if (isKey && !isAutoIncrement)
							builder.AppendLine("		[Dapper.Contrib.Extensions.ExplicitKey]");
						if (!isKey && isAutoIncrement)
							builder.AppendLine("		[Dapper.Contrib.Extensions.Computed]");
					}

					builder.AppendLine(string.Format("		public {0}{1} {2} {{ get; set; }}", name, isNullable ? "?" : string.Empty, collumnName));
				}

				builder.AppendLine("	}");
				builder.AppendLine();
			} while (reader.NextResult());

			return builder.ToString();
		}
	}

	public static string GenerateCRUD(this IDbConnection connection, string tableName)
	{
		if (connection.State != ConnectionState.Open) connection.Open();

		using (var command = connection.CreateCommand($"SELECT * FROM {tableName}"))
		using (var reader = command.ExecuteReader(CommandBehavior.KeyInfo | CommandBehavior.SingleRow))
		{
			var schema = reader.GetSchemaTable();
			List<string> propertyNames = new List<string>();
			List<string> updatePropertyNames = new List<string>();
			StringBuilder builder = new StringBuilder();
			foreach (DataRow row in schema.Rows)
			{
				var collumnName = (string)row["ColumnName"];
				if (!string.IsNullOrEmpty(collumnName))
				{
					propertyNames.Add(collumnName);
					updatePropertyNames.Add($"{collumnName} = {collumnName}");
				}
			}
			builder.AppendLine("SELECT ");
			builder.AppendLineJoin(",", propertyNames);
			builder.AppendLine($"FROM {tableName}");
			builder.AppendLine("WHERE");

			builder.AppendLine();

			builder.AppendLine($"UPDATE {tableName} SET");
			builder.AppendLineJoin(",", updatePropertyNames);
			builder.AppendLine(" WHERE");

			builder.AppendLine();

			builder.AppendLine($"INSERT INTO {tableName} (");
			builder.AppendLineJoin(",", propertyNames);
			builder.AppendLine(") VALUES (");
			builder.AppendLineJoin(",", propertyNames);
			builder.AppendLine(")");

			builder.AppendLine();

			builder.AppendLine($"DELETE FROM {tableName}");
			builder.AppendLine("WHERE");
			builder.AppendLineJoin(" AND ", updatePropertyNames);
			return builder.ToString();
		}
	}

	#region Private
	private static string[] Split(this string text, string splitText) => text.Split(new[] { splitText }, StringSplitOptions.None);
	private static IDbCommand CreateCommand(this IDbConnection connection, string sql)
	{
		var cmd = connection.CreateCommand();
		cmd.CommandText = sql;
		return cmd;
	}
	#endregion
}



public static class StringBuilderExtension
{
	public static StringBuilder AppendLineJoin(this StringBuilder builder, string symbol, List<string> items)
	{
		for (int i = 0; i < items.Count; i++)
		{
			var item = items[i];
			if (i == items.Count - 1)
			{
				builder.AppendLine($"    {item}");
			}
			else
			{
				builder.AppendLine($"    {item}{symbol}");
			}
		}
		return builder;
	}

}