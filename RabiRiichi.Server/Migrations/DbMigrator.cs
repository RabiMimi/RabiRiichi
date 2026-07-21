using System.Reflection;
using Microsoft.Data.Sqlite;

namespace RabiRiichi.Server.Migrations {
  /// <summary>
  /// Applies pending SQL migrations in filename order. Each migration is an
  /// embedded resource under <c>Migrations/*.sql</c>; to add one, drop a new
  /// numbered <c>.sql</c> file in that folder. Applied migrations are recorded
  /// in <c>schema_migrations</c> and never re-run.
  /// </summary>
  public static class DbMigrator {
    private const string ResourcePrefix = "RabiRiichi.Server.Migrations.";
    private const string ResourceSuffix = ".sql";

    public static void Apply(SqliteConnection connection) {
      EnsureHistoryTable(connection);
      var applied = LoadApplied(connection);

      foreach (var (name, sql) in LoadMigrations()) {
        if (applied.Contains(name)) {
          continue;
        }
        using var transaction = connection.BeginTransaction();
        using (var command = connection.CreateCommand()) {
          command.Transaction = transaction;
          command.CommandText = sql;
          command.ExecuteNonQuery();
        }
        using (var record = connection.CreateCommand()) {
          record.Transaction = transaction;
          record.CommandText =
              "INSERT INTO schema_migrations (name) VALUES ($name);";
          record.Parameters.AddWithValue("$name", name);
          record.ExecuteNonQuery();
        }
        transaction.Commit();
      }
    }

    private static void EnsureHistoryTable(SqliteConnection connection) {
      using var command = connection.CreateCommand();
      command.CommandText = @"
        CREATE TABLE IF NOT EXISTS schema_migrations (
          name TEXT PRIMARY KEY,
          applied_at DATETIME DEFAULT CURRENT_TIMESTAMP
        );
      ";
      command.ExecuteNonQuery();
    }

    private static HashSet<string> LoadApplied(SqliteConnection connection) {
      var applied = new HashSet<string>();
      using var command = connection.CreateCommand();
      command.CommandText = "SELECT name FROM schema_migrations;";
      using var reader = command.ExecuteReader();
      while (reader.Read()) {
        applied.Add(reader.GetString(0));
      }
      return applied;
    }

    private static IEnumerable<(string Name, string Sql)> LoadMigrations() {
      var assembly = Assembly.GetExecutingAssembly();
      return assembly.GetManifestResourceNames()
          .Where(resource =>
              resource.StartsWith(ResourcePrefix, StringComparison.Ordinal)
              && resource.EndsWith(ResourceSuffix, StringComparison.Ordinal))
          .OrderBy(resource => resource, StringComparer.Ordinal)
          .Select(resource => (
              Name: resource.Substring(
                  ResourcePrefix.Length,
                  resource.Length - ResourcePrefix.Length - ResourceSuffix.Length),
              Sql: ReadResource(assembly, resource)));
    }

    private static string ReadResource(Assembly assembly, string resource) {
      using var stream = assembly.GetManifestResourceStream(resource)
          ?? throw new InvalidOperationException($"Missing migration resource: {resource}");
      using var reader = new StreamReader(stream);
      return reader.ReadToEnd();
    }
  }
}
