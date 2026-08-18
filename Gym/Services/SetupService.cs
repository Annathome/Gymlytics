using Gym.Models;
using Microsoft.Data.SqlClient;

namespace Gym.Services
{
    public class SetupService
    {
        private readonly string _connectionString;
        private readonly HashingService _hashingService;

        public SetupService(IConfiguration configuration, HashingService hashingService)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
            _hashingService = hashingService;
        }

        public async Task<bool> IsSetupCompletedAsync()
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            string query = @"
            SELECT CASE 
            WHEN EXISTS (SELECT 1 FROM Staff WHERE Role IN ('Admin', 'MainAdmin'))
             OR EXISTS (SELECT 1 FROM SystemSettings WHERE SettingKey = 'IsSetupCompleted' AND SettingValue = 'true')
            THEN CAST(1 AS BIT)
            ELSE CAST(0 AS BIT)
            END;";

            using var command = new SqlCommand(query, connection);
            object? result = await command.ExecuteScalarAsync();
            return result != null && (bool)result;
        }

        public async Task<(bool Success, string? ErrorMessage)> RegisterAdminAsync(AdminViewModel model, string passwordHash)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                using var command = new SqlCommand("dbo.usp_RegisterMainAdmin", connection)
                {
                    CommandType = System.Data.CommandType.StoredProcedure
                };

                command.Parameters.AddWithValue("@FullName", model.FullName);
                command.Parameters.AddWithValue("@Username", model.Username);
                command.Parameters.AddWithValue("@PasswordHash", passwordHash);
                command.Parameters.AddWithValue("@PhoneNumber", model.PhoneNumber);
                command.Parameters.AddWithValue("@Email", model.Email);

                await command.ExecuteNonQueryAsync();
                return (true, null);
            }
            catch (SqlException ex)
            {
                return (false, ex.Message);
            }
            catch (Exception ex)
            {
                return (false, $"System error: {ex.Message}");
            }
        }

        public async Task<(bool Success, string? ErrorMessage, int NewStaffId)> CreatePendingStaffAsync(StaffViewModel model)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                string checkQuery = "SELECT COUNT(*) FROM Staff WHERE Email = @Email";
                using (var checkCmd = new SqlCommand(checkQuery, connection))
                {
                    checkCmd.Parameters.AddWithValue("@Email", model.Email);
                    if (Convert.ToInt64(await checkCmd.ExecuteScalarAsync()) > 0)
                    {
                        return (false, "An account with this email address already exists.", 0);
                    }
                }

                string role = model.Role is "Trainer" or "Staff" ? model.Role : "Staff";

                string insertQuery = @"
                    INSERT INTO Staff (FullName, Email, PhoneNumber, Birthday, Sex, Address, Role, Status, CreatedAt) 
                    OUTPUT INSERTED.Id
                    VALUES (@FullName, @Email, @PhoneNumber, @Birthday, @Sex, @Address, @Role, 'Pending', GETDATE())";

                using var insertCmd = new SqlCommand(insertQuery, connection);
                insertCmd.Parameters.AddWithValue("@FullName", model.FullName ?? (object)DBNull.Value);
                insertCmd.Parameters.AddWithValue("@Email", model.Email);
                insertCmd.Parameters.AddWithValue("@PhoneNumber", model.PhoneNumber ?? (object)DBNull.Value);
                insertCmd.Parameters.AddWithValue("@Birthday", model.Birthday == default ? DBNull.Value : model.Birthday);
                insertCmd.Parameters.AddWithValue("@Sex", model.Sex ?? (object)DBNull.Value);
                insertCmd.Parameters.AddWithValue("@Address", model.Address ?? (object)DBNull.Value);
                insertCmd.Parameters.AddWithValue("@Role", role);

                object? idResult = await insertCmd.ExecuteScalarAsync();
                int newId = Convert.ToInt32(idResult);

                return (true, null, newId);
            }
            catch (Exception ex)
            {
                return (false, $"Database error: {ex.Message}", 0);
            }
        }

        public async Task<(bool Success, string? ErrorMessage)> ActivateStaffWithCredentialsAsync(int staffId, string username, string passwordHash)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();

                string checkUsernameQuery = "SELECT COUNT(*) FROM Staff WHERE Username = @Username AND Id <> @StaffId";
                using (var checkCmd = new SqlCommand(checkUsernameQuery, connection))
                {
                    checkCmd.Parameters.AddWithValue("@Username", username);
                    checkCmd.Parameters.AddWithValue("@StaffId", staffId);

                    int exists = Convert.ToInt32(await checkCmd.ExecuteScalarAsync());
                    if (exists > 0)
                    {
                        return (false, "Username is already taken. Please choose a different one.");
                    }
                }

                string updateQuery = @"
                    UPDATE Staff 
                    SET Username = @Username, 
                        PasswordHash = @PasswordHash, 
                        Status = 'Active' 
                    WHERE Id = @StaffId AND Status = 'Pending'";

                using (var updateCmd = new SqlCommand(updateQuery, connection))
                {
                    updateCmd.Parameters.AddWithValue("@Username", username);
                    updateCmd.Parameters.AddWithValue("@PasswordHash", passwordHash);
                    updateCmd.Parameters.AddWithValue("@StaffId", staffId);

                    int rowsAffected = await updateCmd.ExecuteNonQueryAsync();

                    if (rowsAffected == 0)
                    {
                        return (false, "Account setup failed. The registration link may be invalid, expired, or already completed.");
                    }
                }

                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, $"Database error: {ex.Message}");
            }
        }

        public async Task<(bool Success, string? ErrorMessage)> ActivateStaffAsync(int staffId)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            string query = "UPDATE Staff SET Status = 'Active' WHERE Id = @Id AND Status = 'Pending'";
            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@Id", staffId);

            int rows = await command.ExecuteNonQueryAsync();
            return rows > 0
                ? (true, null)
                : (false, "This confirmation link is invalid or has already been used.");
        }

        public async Task DeletePendingStaffAsync(int staffId)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            string query = "DELETE FROM Staff WHERE Id = @Id AND Status = 'Pending'";
            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@Id", staffId);
            await command.ExecuteNonQueryAsync();
        }

        public async Task<List<UserRoleViewModel>> GetAllUsersAsync()
        {
            var users = new List<UserRoleViewModel>();

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            string query = @"
                SELECT Id, FullName, Username, Email, Role, Status, LastLoginAt
                FROM Staff
                ORDER BY 
                    CASE Status WHEN 'Pending' THEN 0 WHEN 'Active' THEN 1 ELSE 2 END,
                    FullName";

            using var command = new SqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                users.Add(new UserRoleViewModel
                {
                    Id = Convert.ToInt32(reader["Id"]),
                    FullName = reader["FullName"]?.ToString() ?? string.Empty,
                    Username = reader["Username"]?.ToString() ?? string.Empty,
                    Email = reader["Email"]?.ToString() ?? string.Empty,
                    Role = reader["Role"]?.ToString() ?? string.Empty,
                    Status = reader["Status"]?.ToString() ?? "Active",
                    LastLoginAt = reader["LastLoginAt"] is DBNull ? null : Convert.ToDateTime(reader["LastLoginAt"])
                });
            }

            return users;
        }

        public async Task<(List<UserRoleViewModel> Users, int TotalCount)> GetAllUsersAsync(int page, int pageSize)
        {
            page = Math.Max(page, 1);
            var users = new List<UserRoleViewModel>();
            int totalCount;

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            string query = @"
        SELECT COUNT(*) FROM Staff;

        SELECT Id, FullName, Username, Email, Role, Status, LastLoginAt
        FROM Staff
        ORDER BY 
            CASE Status WHEN 'Pending' THEN 0 WHEN 'Active' THEN 1 ELSE 2 END,
            FullName
        OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@Offset", (page - 1) * pageSize);
            command.Parameters.AddWithValue("@PageSize", pageSize);

            using var reader = await command.ExecuteReaderAsync();

            await reader.ReadAsync();
            totalCount = Convert.ToInt32(reader[0]);

            await reader.NextResultAsync();
            while (await reader.ReadAsync())
            {
                users.Add(new UserRoleViewModel
                {
                    Id = Convert.ToInt32(reader["Id"]),
                    FullName = reader["FullName"]?.ToString() ?? string.Empty,
                    Username = reader["Username"]?.ToString() ?? string.Empty,
                    Email = reader["Email"]?.ToString() ?? string.Empty,
                    Role = reader["Role"]?.ToString() ?? string.Empty,
                    Status = reader["Status"]?.ToString() ?? "Active",
                    LastLoginAt = reader["LastLoginAt"] is DBNull ? null : Convert.ToDateTime(reader["LastLoginAt"])
                });
            }

            return (users, totalCount);
        }

        public async Task<List<ActivityLogEntry>> GetActivityLogAsync(int staffId, int take = 25)
        {
            var log = new List<ActivityLogEntry>();

            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            string query = @"
                SELECT TOP (@Take) ActionType, OccurredAt
                FROM StaffActivityLog
                WHERE StaffId = @StaffId
                ORDER BY OccurredAt DESC";

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@Take", take);
            command.Parameters.AddWithValue("@StaffId", staffId);

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                log.Add(new ActivityLogEntry
                {
                    ActionType = reader["ActionType"]?.ToString() ?? string.Empty,
                    OccurredAt = Convert.ToDateTime(reader["OccurredAt"])
                });
            }

            return log;
        }

        public async Task RecordLoginAsync(int staffId)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            string query = @"
                UPDATE Staff SET LastLoginAt = GETDATE() WHERE Id = @Id;
                INSERT INTO StaffActivityLog (StaffId, ActionType, OccurredAt) VALUES (@Id, 'Login', GETDATE());";

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@Id", staffId);
            await command.ExecuteNonQueryAsync();
        }

        public async Task RecordLogoutAsync(int staffId)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            string query = "INSERT INTO StaffActivityLog (StaffId, ActionType, OccurredAt) VALUES (@Id, 'Logout', GETDATE())";
            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@Id", staffId);
            await command.ExecuteNonQueryAsync();
        }

        public async Task<(bool Success, string? ErrorMessage)> SuspendUserAsync(int staffId)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            string query = @"
                UPDATE Staff SET Status = 'Suspended' 
                WHERE Id = @Id AND Status = 'Active' AND Role NOT IN ('MainAdmin')";

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@Id", staffId);
            int rows = await command.ExecuteNonQueryAsync();

            return rows > 0
                ? (true, null)
                : (false, "That account can't be suspended (already suspended, pending, or a protected admin account).");
        }

        public async Task<(bool Success, string? ErrorMessage)> ReactivateUserAsync(int staffId)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            string query = "UPDATE Staff SET Status = 'Active' WHERE Id = @Id AND Status = 'Suspended'";
            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@Id", staffId);
            int rows = await command.ExecuteNonQueryAsync();

            return rows > 0
                ? (true, null)
                : (false, "That account isn't currently suspended.");
        }

        public async Task<(bool Success, string? ErrorMessage)> DeleteUserAsync(int staffId)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            string query = "DELETE FROM Staff WHERE Id = @Id AND Role NOT IN ('MainAdmin')";
            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@Id", staffId);
            int rows = await command.ExecuteNonQueryAsync();

            return rows > 0
                ? (true, null)
                : (false, "That account can't be deleted (not found, or a protected admin account).");
        }

        public async Task<(bool IsValid, string? Role, int UserId, string? Username, string? Email, string? Phone, string? ErrorMessage)> ValidateUserAsync(string loginIdentifier, string password)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            string query = @"
                SELECT TOP 1 Id AS StaffID, Username, Email, PhoneNumber, PasswordHash, Role, Status 
                FROM Staff 
                WHERE Email = @Identifier OR Username = @Identifier";

            using var command = new SqlCommand(query, connection);
            command.Parameters.AddWithValue("@Identifier", loginIdentifier);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                int userId = Convert.ToInt32(reader["StaffID"]);
                string? username = reader["Username"]?.ToString();
                string? email = reader["Email"]?.ToString();
                string? phone = reader["PhoneNumber"]?.ToString();
                string? storedHash = reader["PasswordHash"]?.ToString();
                string? role = reader["Role"]?.ToString();
                string? status = reader["Status"]?.ToString();

                if (!string.IsNullOrEmpty(storedHash) && _hashingService.VerifyPassword(password, storedHash))
                {
                    if (status == "Pending")
                    {
                        return (false, null, 0, null, null, null, "Your account is pending email confirmation. Check your inbox for the confirmation link.");
                    }
                    if (status == "Suspended")
                    {
                        return (false, null, 0, null, null, null, "Your account has been suspended. Contact an administrator.");
                    }

                    string normalizedRole = (role == "MainAdmin" || role == "Admin")
                        ? "Admin"
                        : (role ?? "Staff");

                    return (true, normalizedRole, userId, username, email, phone, null);
                }
            }

            return (false, null, 0, null, null, null, "Invalid login credentials.");
        }
    }
}