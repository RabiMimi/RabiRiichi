using Microsoft.Data.Sqlite;
using RabiRiichi.Server.Generated.Rpc;
using RabiRiichi.Server.Utils;
using Google.Protobuf;
using System;
using System.Security.Cryptography;
using System.Text;

namespace RabiRiichi.Server.Services {
  public class DbUser {
    public int Id { get; set; }
    public string Username { get; set; }
    public UserData UserData { get; set; }
  }


  public class DbService {
    private readonly string connectionString;

    public DbService() {
      var dbPath = Environment.GetEnvironmentVariable("RABIRIICHI_DB_PATH") ?? "rabiriichi.db";
      connectionString = $"Data Source={dbPath}";
    }

    public void InitializeDatabase() {
      using var connection = new SqliteConnection(connectionString);
      connection.Open();

      using var command = connection.CreateCommand();
      command.CommandText = @"
        CREATE TABLE IF NOT EXISTS users (
          id INTEGER PRIMARY KEY AUTOINCREMENT,
          username TEXT NOT NULL UNIQUE COLLATE NOCASE,
          user_data BLOB NOT NULL,
          password_hash TEXT NOT NULL,
          created_at DATETIME DEFAULT CURRENT_TIMESTAMP
        );
        CREATE UNIQUE INDEX IF NOT EXISTS idx_users_username ON users(username);
      ";
      command.ExecuteNonQuery();
    }

    public int CreateUser(string username, UserData userData, string passwordHash, out string error) {
      error = null;
      if (string.IsNullOrWhiteSpace(username)) {
        error = "Username cannot be empty";
        return -1;
      }
      if (string.IsNullOrWhiteSpace(passwordHash)) {
        error = "Password cannot be empty";
        return -1;
      }

      byte[] userDataBytes = userData.ToByteArray();

      try {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
          INSERT INTO users (username, user_data, password_hash)
          VALUES ($username, $user_data, $password_hash);
          SELECT last_insert_rowid();
        ";
        command.Parameters.AddWithValue("$username", username.Trim());
        command.Parameters.AddWithValue("$user_data", userDataBytes);
        command.Parameters.AddWithValue("$password_hash", passwordHash);

        var result = command.ExecuteScalar();
        return Convert.ToInt32(result);
      } catch (SqliteException ex) when (ex.SqliteErrorCode == 19 || ex.Message.Contains("UNIQUE constraint failed")) {
        error = "Username already exists";
        return -1;
      } catch (Exception ex) {
        error = ex.Message;
        return -1;
      }
    }

    public DbUser AuthenticateUser(string username, string passwordHash, out string error) {
      error = null;
      if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(passwordHash)) {
        error = "Invalid username or password";
        return null;
      }



      try {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
          SELECT id, username, user_data, password_hash
          FROM users
          WHERE username = $username;
        ";
        command.Parameters.AddWithValue("$username", username.Trim());

        using var reader = command.ExecuteReader();
        if (reader.Read()) {
          string dbPasswordHash = reader.GetString(3);
          if (dbPasswordHash == passwordHash) {
            var id = reader.GetInt32(0);
            var uname = reader.GetString(1);
            var userDataBytes = (byte[])reader.GetValue(2);
            var userData = UserData.Parser.ParseFrom(userDataBytes);
            return new DbUser {
              Id = id,
              Username = uname,
              UserData = userData
            };
          }
        }
        error = "Invalid username or password";
        return null;
      } catch (Exception ex) {
        error = ex.Message;
        return null;
      }
    }

    public DbUser GetUserById(int userId) {
      try {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
          SELECT id, username, user_data
          FROM users
          WHERE id = $id;
        ";
        command.Parameters.AddWithValue("$id", userId);

        using var reader = command.ExecuteReader();
        if (reader.Read()) {
          var id = reader.GetInt32(0);
          var uname = reader.GetString(1);
          var userDataBytes = (byte[])reader.GetValue(2);
          var userData = UserData.Parser.ParseFrom(userDataBytes);
          return new DbUser {
            Id = id,
            Username = uname,
            UserData = userData
          };
        }
        return null;
      } catch {
        return null;
      }
    }
  }
}
