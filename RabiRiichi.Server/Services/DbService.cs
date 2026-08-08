using Microsoft.Data.Sqlite;
using RabiRiichi.Server.Generated.Rpc;
using RabiRiichi.Server.Migrations;
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
    // Incremented on every password change so previously issued access tokens
    // (which embed the version they were minted with) can be invalidated.
    public int TokenVersion { get; set; }
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
      DbMigrator.Apply(connection);
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
          SELECT id, username, user_data, password_hash, token_version
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
              UserData = userData,
              TokenVersion = reader.GetInt32(4)
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
          SELECT id, username, user_data, token_version
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
            UserData = userData,
            TokenVersion = reader.GetInt32(3)
          };
        }
        return null;
      } catch {
        return null;
      }
    }

    public bool UpdateNickname(int userId, string nickname, out string error) {
      error = null;
      if (string.IsNullOrWhiteSpace(nickname)) {
        error = "Nickname cannot be empty";
        return false;
      }

      try {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        var existing = GetUserById(userId);
        if (existing == null) {
          error = "User not found";
          return false;
        }
        // Merge into the stored UserData so future fields survive the update.
        existing.UserData.Nickname = nickname.Trim();

        using var command = connection.CreateCommand();
        command.CommandText = @"
          UPDATE users SET user_data = $user_data WHERE id = $id;
        ";
        command.Parameters.AddWithValue("$user_data", existing.UserData.ToByteArray());
        command.Parameters.AddWithValue("$id", userId);
        return command.ExecuteNonQuery() > 0;
      } catch (Exception ex) {
        error = ex.Message;
        return false;
      }
    }

    /// <summary>
    /// Verifies the old password and, on success, sets the new password and
    /// bumps the user's token version. Returns the new version so a fresh token
    /// can be minted; all previously issued tokens become invalid.
    /// </summary>
    public DbUser ChangePassword(
        string username, string oldPasswordHash, string newPasswordHash, out string error) {
      error = null;
      if (string.IsNullOrWhiteSpace(newPasswordHash)) {
        error = "Password cannot be empty";
        return null;
      }

      var dbUser = AuthenticateUser(username, oldPasswordHash, out error);
      if (dbUser == null) {
        return null;
      }

      try {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = @"
          UPDATE users
          SET password_hash = $password_hash, token_version = token_version + 1
          WHERE id = $id;
          SELECT token_version FROM users WHERE id = $id;
        ";
        command.Parameters.AddWithValue("$password_hash", newPasswordHash);
        command.Parameters.AddWithValue("$id", dbUser.Id);
        var result = command.ExecuteScalar();
        dbUser.TokenVersion = Convert.ToInt32(result);
        return dbUser;
      } catch (Exception ex) {
        error = ex.Message;
        return null;
      }
    }
  }
}
