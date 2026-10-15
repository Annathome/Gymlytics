using Gym.Models;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Gym.Services
{
    public class SetupService
    {
        private readonly string _connectionString;
        private readonly HashingService _hashingService;

        public SetupService(
            IConfiguration configuration,
            HashingService hashingService)
        {
            _connectionString =
                configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "Connection string 'DefaultConnection' not found.");

            _hashingService = hashingService;
        }

        public async Task<bool> IsSetupCompletedAsync()
        {
            await using var connection =
                new SqlConnection(_connectionString);

            await connection.OpenAsync();

            const string sql = """
                SELECT CASE
                    WHEN EXISTS
                    (
                        SELECT 1
                        FROM dbo.Staff
                        WHERE Role IN ('Admin', 'MainAdmin')
                    )
                    OR EXISTS
                    (
                        SELECT 1
                        FROM dbo.SystemSettings
                        WHERE SettingKey = 'IsSetupCompleted'
                          AND SettingValue = 'true'
                    )
                    THEN CAST(1 AS bit)
                    ELSE CAST(0 AS bit)
                END;
                """;

            await using var command =
                new SqlCommand(sql, connection);

            object? result = await command.ExecuteScalarAsync();

            return result is true;
        }

        public async Task<(bool Success, string? ErrorMessage)>
            RegisterAdminAsync(
                AdminViewModel model,
                string passwordHash)
        {
            try
            {
                await using var connection =
                    new SqlConnection(_connectionString);

                await connection.OpenAsync();

                // Keep setup self-contained. The deployed database may have been
                // created by EF or an older script and therefore may not contain
                // dbo.usp_RegisterMainAdmin.
                const string sql = """
                    SET XACT_ABORT ON;
                    SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
                    BEGIN TRANSACTION;

                    IF EXISTS
                    (
                        SELECT 1
                        FROM dbo.Staff WITH (UPDLOCK, HOLDLOCK)
                        WHERE Role IN ('Admin', 'MainAdmin')
                    )
                    OR EXISTS
                    (
                        SELECT 1
                        FROM dbo.SystemSettings WITH (UPDLOCK, HOLDLOCK)
                        WHERE SettingKey = 'IsSetupCompleted'
                          AND SettingValue = 'true'
                    )
                        THROW 50001,
                            'System Setup Completed: Main Admin account has already been registered.',
                            1;

                    IF EXISTS
                    (
                        SELECT 1
                        FROM dbo.Staff WITH (UPDLOCK, HOLDLOCK)
                        WHERE Username = @Username OR Email = @Email
                    )
                        THROW 50002,
                            'Username or Email address is already in use.',
                            1;

                    INSERT INTO dbo.Staff
                    (
                        FullName,
                        Username,
                        PasswordHash,
                        PhoneNumber,
                        Email,
                        Role,
                        Status,
                        CreatedAt
                    )
                    VALUES
                    (
                        @FullName,
                        @Username,
                        @PasswordHash,
                        @PhoneNumber,
                        @Email,
                        'MainAdmin',
                        'Active',
                        GETDATE()
                    );

                    UPDATE dbo.SystemSettings
                    SET SettingValue = 'true'
                    WHERE SettingKey = 'IsSetupCompleted';

                    IF @@ROWCOUNT = 0
                    BEGIN
                        INSERT INTO dbo.SystemSettings (SettingKey, SettingValue)
                        VALUES ('IsSetupCompleted', 'true');
                    END

                    COMMIT TRANSACTION;
                    """;

                await using var command = new SqlCommand(sql, connection);

                command.Parameters.Add("@FullName", SqlDbType.NVarChar, 100)
                    .Value = model.FullName;
                command.Parameters.Add("@Username", SqlDbType.NVarChar, 50)
                    .Value = model.Username;
                command.Parameters.Add("@PasswordHash", SqlDbType.NVarChar, 255)
                    .Value = passwordHash;
                command.Parameters.Add("@PhoneNumber", SqlDbType.NVarChar, 20)
                    .Value = model.PhoneNumber;
                command.Parameters.Add("@Email", SqlDbType.NVarChar, 100)
                    .Value = model.Email;

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

        public async Task<(
            bool Success,
            string? ErrorMessage,
            int NewStaffId)> CreatePendingStaffAsync(
                StaffViewModel model,
                string passwordHash)
        {
            try
            {
                await using var connection =
                    new SqlConnection(_connectionString);

                await connection.OpenAsync();

                const string checkSql = """
                    SELECT COUNT(*)
                    FROM dbo.Staff
                    WHERE Email = @Email OR Username = @Username;
                    """;

                await using (var checkCommand =
                    new SqlCommand(checkSql, connection))
                {
                    checkCommand.Parameters.AddWithValue(
                        "@Email",
                        model.Email);
                    checkCommand.Parameters.AddWithValue(
                        "@Username",
                        model.Username.Trim());

                    long existingCount = Convert.ToInt64(
                        await checkCommand.ExecuteScalarAsync());

                    if (existingCount > 0)
                    {
                        return (
                            false,
                            "An account with this email address or username already exists.",
                            0);
                    }
                }

                string role =
                    model.Role is "Trainer" or "Staff"
                        ? model.Role
                        : "Staff";

                const string insertSql = """
                    INSERT INTO dbo.Staff
                    (
                        FullName,
                        Username,
                        PasswordHash,
                        Email,
                        PhoneNumber,
                        Birthday,
                        Sex,
                        Address,
                        Role,
                        Status,
                        CreatedAt
                    )
                    OUTPUT INSERTED.Id
                    VALUES
                    (
                        @FullName,
                        @Username,
                        @PasswordHash,
                        @Email,
                        @PhoneNumber,
                        @Birthday,
                        @Sex,
                        @Address,
                        @Role,
                        'Pending',
                        GETDATE()
                    );
                    """;

                await using var insertCommand =
                    new SqlCommand(insertSql, connection);

                insertCommand.Parameters.AddWithValue(
                    "@FullName",
                    DbValue(model.FullName));

                insertCommand.Parameters.Add("@Username", SqlDbType.NVarChar, 50)
                    .Value = model.Username.Trim();

                insertCommand.Parameters.Add("@PasswordHash", SqlDbType.NVarChar, 255)
                    .Value = passwordHash;

                insertCommand.Parameters.AddWithValue(
                    "@Email",
                    model.Email);

                insertCommand.Parameters.AddWithValue(
                    "@PhoneNumber",
                    DbValue(model.PhoneNumber));

                insertCommand.Parameters.AddWithValue(
                    "@Birthday",
                    model.Birthday == default
                        ? DBNull.Value
                        : model.Birthday);

                insertCommand.Parameters.AddWithValue(
                    "@Sex",
                    DbValue(model.Sex));

                insertCommand.Parameters.AddWithValue(
                    "@Address",
                    DbValue(model.Address));

                insertCommand.Parameters.AddWithValue(
                    "@Role",
                    role);

                object? result =
                    await insertCommand.ExecuteScalarAsync();

                int newStaffId = Convert.ToInt32(result);

                return (true, null, newStaffId);
            }
            catch (Exception ex)
            {
                return (
                    false,
                    $"Database error: {ex.Message}",
                    0);
            }
        }

        public async Task<(bool Success, string? ErrorMessage)>
            ActivateStaffWithCredentialsAsync(
                int staffId,
                string username,
                string passwordHash)
        {
            try
            {
                await using var connection =
                    new SqlConnection(_connectionString);

                await connection.OpenAsync();

                const string checkSql = """
                    SELECT COUNT(*)
                    FROM dbo.Staff
                    WHERE Username = @Username
                      AND Id <> @StaffId;
                    """;

                await using (var checkCommand =
                    new SqlCommand(checkSql, connection))
                {
                    checkCommand.Parameters.AddWithValue(
                        "@Username",
                        username);

                    checkCommand.Parameters.AddWithValue(
                        "@StaffId",
                        staffId);

                    int count = Convert.ToInt32(
                        await checkCommand.ExecuteScalarAsync());

                    if (count > 0)
                    {
                        return (
                            false,
                            "Username is already taken. Please choose a different one.");
                    }
                }

                const string updateSql = """
                    UPDATE dbo.Staff
                    SET Username = @Username,
                        PasswordHash = @PasswordHash,
                        Status = 'Active'
                    WHERE Id = @StaffId
                      AND Status = 'Pending';
                    """;

                await using var updateCommand =
                    new SqlCommand(updateSql, connection);

                updateCommand.Parameters.AddWithValue(
                    "@Username",
                    username);

                updateCommand.Parameters.AddWithValue(
                    "@PasswordHash",
                    passwordHash);

                updateCommand.Parameters.AddWithValue(
                    "@StaffId",
                    staffId);

                int rowsAffected =
                    await updateCommand.ExecuteNonQueryAsync();

                if (rowsAffected == 0)
                {
                    return (
                        false,
                        "Account setup failed. The registration link may be invalid, expired, or already completed.");
                }

                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, $"Database error: {ex.Message}");
            }
        }

        public async Task<(bool Success, string? ErrorMessage)>
            ActivateStaffAsync(int staffId)
        {
            await using var connection =
                new SqlConnection(_connectionString);

            await connection.OpenAsync();

            const string sql = """
                UPDATE dbo.Staff
                SET Status = 'Active'
                WHERE Id = @Id
                  AND Status = 'Pending';
                """;

            await using var command =
                new SqlCommand(sql, connection);

            command.Parameters.AddWithValue("@Id", staffId);

            int rows = await command.ExecuteNonQueryAsync();

            return rows > 0
                ? (true, null)
                : (
                    false,
                    "This confirmation link is invalid or has already been used.");
        }

        public async Task DeletePendingStaffAsync(int staffId)
        {
            await using var connection =
                new SqlConnection(_connectionString);

            await connection.OpenAsync();

            const string sql = """
                DELETE FROM dbo.Staff
                WHERE Id = @Id
                  AND Status = 'Pending';
                """;

            await using var command =
                new SqlCommand(sql, connection);

            command.Parameters.AddWithValue("@Id", staffId);

            await command.ExecuteNonQueryAsync();
        }

        public async Task<bool> PermanentlyDeletePendingStaffAsync(int staffId)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            const string sql = """
                DELETE FROM dbo.Staff
                WHERE Id = @Id
                  AND Status = 'Pending'
                  AND Role <> 'MainAdmin';
                """;

            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Id", staffId);
            return await command.ExecuteNonQueryAsync() > 0;
        }

        public async Task<List<UserRoleViewModel>>
            GetAllUsersAsync()
        {
            var users = new List<UserRoleViewModel>();

            await using var connection =
                new SqlConnection(_connectionString);

            await connection.OpenAsync();

            const string sql = """
                SELECT
                    staff.Id,
                    staff.FullName,
                    staff.Username,
                    staff.Email,
                    staff.PhoneNumber,
                    staff.Role,
                    staff.Status,
                    staff.LastLoginAt,
                    attendance.AttendanceStatus AS TodayAttendanceStatus,
                    attendance.CheckedInAt AS TodayCheckedInAt
                FROM dbo.Staff staff
                LEFT JOIN dbo.StaffAttendanceLogs attendance
                    ON attendance.StaffId = staff.Id
                   AND attendance.AttendanceDate = CAST(GETDATE() AS DATE)
                WHERE staff.Status <> 'Deleted'
                ORDER BY
                    CASE staff.Status
                        WHEN 'Pending' THEN 0
                        WHEN 'Active' THEN 1
                        ELSE 2
                    END,
                    staff.FullName;
                """;

            await using var command =
                new SqlCommand(sql, connection);

            await using var reader =
                await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                users.Add(MapUser(reader));
            }

            return users;
        }

        public async Task<(
            List<UserRoleViewModel> Users,
            int TotalCount)> GetAllUsersAsync(
                int page,
                int pageSize)
        {
            page = Math.Max(page, 1);
            pageSize = Math.Max(pageSize, 1);

            var users = new List<UserRoleViewModel>();

            await using var connection =
                new SqlConnection(_connectionString);

            await connection.OpenAsync();

            const string sql = """
                SELECT COUNT(*)
                FROM dbo.Staff
                WHERE Status <> 'Deleted';

                SELECT
                    staff.Id,
                    staff.FullName,
                    staff.Username,
                    staff.Email,
                    staff.PhoneNumber,
                    staff.Role,
                    staff.Status,
                    staff.LastLoginAt,
                    attendance.AttendanceStatus AS TodayAttendanceStatus,
                    attendance.CheckedInAt AS TodayCheckedInAt
                FROM dbo.Staff staff
                LEFT JOIN dbo.StaffAttendanceLogs attendance
                    ON attendance.StaffId = staff.Id
                   AND attendance.AttendanceDate = CAST(GETDATE() AS DATE)
                WHERE staff.Status <> 'Deleted'
                ORDER BY
                    CASE staff.Status
                        WHEN 'Pending' THEN 0
                        WHEN 'Active' THEN 1
                        ELSE 2
                    END,
                    staff.FullName
                OFFSET @Offset ROWS
                FETCH NEXT @PageSize ROWS ONLY;
                """;

            await using var command =
                new SqlCommand(sql, connection);

            command.Parameters.AddWithValue(
                "@Offset",
                (page - 1) * pageSize);

            command.Parameters.AddWithValue(
                "@PageSize",
                pageSize);

            await using var reader =
                await command.ExecuteReaderAsync();

            int totalCount = 0;

            if (await reader.ReadAsync())
            {
                totalCount = Convert.ToInt32(reader[0]);
            }

            await reader.NextResultAsync();

            while (await reader.ReadAsync())
            {
                users.Add(MapUser(reader));
            }

            return (users, totalCount);
        }

        public async Task<List<ActivityLogEntry>>
            GetActivityLogAsync(
                int staffId,
                int take = 25)
        {
            var log = new List<ActivityLogEntry>();

            await using var connection =
                new SqlConnection(_connectionString);

            await connection.OpenAsync();

            const string sql = """
                SELECT TOP (@Take)
                    ActionType,
                    OccurredAt
                FROM dbo.StaffActivityLog
                WHERE StaffId = @StaffId
                ORDER BY OccurredAt DESC;
                """;

            await using var command =
                new SqlCommand(sql, connection);

            command.Parameters.AddWithValue(
                "@Take",
                Math.Max(take, 1));

            command.Parameters.AddWithValue(
                "@StaffId",
                staffId);

            await using var reader =
                await command.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                log.Add(new ActivityLogEntry
                {
                    ActionType =
                        reader["ActionType"]?.ToString()
                        ?? string.Empty,

                    OccurredAt =
                        Convert.ToDateTime(reader["OccurredAt"])
                });
            }

            return log;
        }

        public async Task RecordLoginAsync(int staffId)
        {
            await using var connection =
                new SqlConnection(_connectionString);

            await connection.OpenAsync();

            const string sql = """
                UPDATE dbo.Staff
                SET LastLoginAt = GETDATE()
                WHERE Id = @Id;

                INSERT INTO dbo.StaffActivityLog
                    (StaffId, ActionType, OccurredAt)
                VALUES
                    (@Id, 'Login', GETDATE());

                IF EXISTS (SELECT 1 FROM dbo.Staff WHERE Id = @Id AND Role IN ('Staff', 'Trainer'))
                   AND CAST(GETDATE() AS TIME) >= CAST('07:00:00' AS TIME)
                   AND NOT EXISTS
                   (
                       SELECT 1 FROM dbo.StaffAttendanceLogs
                       WHERE StaffId = @Id AND AttendanceDate = CAST(GETDATE() AS DATE)
                   )
                BEGIN
                    INSERT INTO dbo.StaffAttendanceLogs
                        (StaffId, AttendanceDate, AttendanceStatus, CheckedInAt)
                    VALUES
                        (@Id, CAST(GETDATE() AS DATE), 'Present', GETDATE());
                END;
                """;

            await using var command =
                new SqlCommand(sql, connection);

            command.Parameters.AddWithValue("@Id", staffId);

            await command.ExecuteNonQueryAsync();
        }

        public async Task<(bool Success, string? ErrorMessage)> MarkStaffAbsentAsync(
            int staffId,
            int markedByStaffId)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            const string sql = """
                IF NOT EXISTS
                (
                    SELECT 1 FROM dbo.Staff
                    WHERE Id = @StaffId AND Role IN ('Staff', 'Trainer') AND Status = 'Active'
                )
                BEGIN
                    SELECT CAST(0 AS BIT);
                    RETURN;
                END;

                UPDATE dbo.StaffAttendanceLogs
                SET AttendanceStatus = 'Absent',
                    CheckedInAt = NULL,
                    MarkedAbsentAt = GETDATE(),
                    MarkedAbsentByStaffId = @MarkedByStaffId
                WHERE StaffId = @StaffId
                  AND AttendanceDate = CAST(GETDATE() AS DATE);

                IF @@ROWCOUNT = 0
                BEGIN
                    INSERT INTO dbo.StaffAttendanceLogs
                        (StaffId, AttendanceDate, AttendanceStatus, MarkedAbsentAt, MarkedAbsentByStaffId)
                    VALUES
                        (@StaffId, CAST(GETDATE() AS DATE), 'Absent', GETDATE(), @MarkedByStaffId);
                END;

                INSERT INTO dbo.StaffActivityLog (StaffId, ActionType, OccurredAt)
                VALUES (@StaffId, 'Absent', GETDATE());

                SELECT CAST(1 AS BIT);
                """;

            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@StaffId", staffId);
            command.Parameters.AddWithValue("@MarkedByStaffId", markedByStaffId);
            var success = Convert.ToBoolean(await command.ExecuteScalarAsync());
            return success
                ? (true, null)
                : (false, "Only active Staff or Trainer accounts can be marked absent.");
        }

        public async Task<(List<UserAttendanceItem> Items, int TotalCount)> GetUserAttendanceAsync(
            int page,
            int pageSize)
        {
            page = Math.Max(1, page);
            pageSize = Math.Max(1, pageSize);
            var items = new List<UserAttendanceItem>();

            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            const string sql = """
                SELECT COUNT(*) FROM dbo.StaffAttendanceLogs;

                SELECT attendance.StaffId,
                       staff.FullName,
                       staff.Role,
                       attendance.AttendanceDate,
                       attendance.AttendanceStatus,
                       attendance.CheckedInAt,
                       attendance.MarkedAbsentAt
                FROM dbo.StaffAttendanceLogs attendance
                INNER JOIN dbo.Staff staff ON staff.Id = attendance.StaffId
                ORDER BY attendance.AttendanceDate DESC,
                         COALESCE(attendance.CheckedInAt, attendance.MarkedAbsentAt) DESC,
                         staff.FullName
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
                """;

            await using var command = new SqlCommand(sql, connection);
            command.Parameters.AddWithValue("@Offset", (page - 1) * pageSize);
            command.Parameters.AddWithValue("@PageSize", pageSize);
            await using var reader = await command.ExecuteReaderAsync();
            await reader.ReadAsync();
            var totalCount = reader.GetInt32(0);
            await reader.NextResultAsync();

            while (await reader.ReadAsync())
            {
                items.Add(new UserAttendanceItem
                {
                    StaffId = Convert.ToInt32(reader["StaffId"]),
                    FullName = reader["FullName"]?.ToString() ?? string.Empty,
                    Role = reader["Role"]?.ToString() ?? string.Empty,
                    AttendanceDate = Convert.ToDateTime(reader["AttendanceDate"]),
                    AttendanceStatus = reader["AttendanceStatus"]?.ToString() ?? string.Empty,
                    CheckedInAt = reader["CheckedInAt"] is DBNull ? null : Convert.ToDateTime(reader["CheckedInAt"]),
                    MarkedAbsentAt = reader["MarkedAbsentAt"] is DBNull ? null : Convert.ToDateTime(reader["MarkedAbsentAt"])
                });
            }

            return (items, totalCount);
        }

        public async Task RecordLogoutAsync(int staffId)
        {
            await using var connection =
                new SqlConnection(_connectionString);

            await connection.OpenAsync();

            const string sql = """
                INSERT INTO dbo.StaffActivityLog
                    (StaffId, ActionType, OccurredAt)
                VALUES
                    (@Id, 'Logout', GETDATE());
                """;

            await using var command =
                new SqlCommand(sql, connection);

            command.Parameters.AddWithValue("@Id", staffId);

            await command.ExecuteNonQueryAsync();
        }

        public async Task<(bool Success, string? ErrorMessage)>
            SuspendUserAsync(int staffId)
        {
            await using var connection =
                new SqlConnection(_connectionString);

            await connection.OpenAsync();

            const string sql = """
                UPDATE dbo.Staff
                SET Status = 'Suspended'
                WHERE Id = @Id
                  AND Status = 'Active'
                  AND Role <> 'MainAdmin';
                """;

            await using var command =
                new SqlCommand(sql, connection);

            command.Parameters.AddWithValue("@Id", staffId);

            int rows = await command.ExecuteNonQueryAsync();

            return rows > 0
                ? (true, null)
                : (
                    false,
                    "That account cannot be suspended.");
        }

        public async Task<(bool Success, string? ErrorMessage)>
            ReactivateUserAsync(int staffId)
        {
            await using var connection =
                new SqlConnection(_connectionString);

            await connection.OpenAsync();

            const string sql = """
                UPDATE dbo.Staff
                SET Status = 'Active'
                WHERE Id = @Id
                  AND Status = 'Suspended';
                """;

            await using var command =
                new SqlCommand(sql, connection);

            command.Parameters.AddWithValue("@Id", staffId);

            int rows = await command.ExecuteNonQueryAsync();

            return rows > 0
                ? (true, null)
                : (
                    false,
                    "That account is not currently suspended.");
        }

        public async Task<(bool Success, string? ErrorMessage)>
            DeleteUserAsync(int staffId)
        {
            await using var connection =
                new SqlConnection(_connectionString);

            await connection.OpenAsync();

            const string sql = """
                UPDATE dbo.Staff
                SET Status = 'Deleted'
                WHERE Id = @Id
                  AND Role <> 'MainAdmin'
                  AND Status IN ('Active', 'Suspended');
                """;

            await using var command =
                new SqlCommand(sql, connection);

            command.Parameters.AddWithValue("@Id", staffId);

            int rows = await command.ExecuteNonQueryAsync();

            return rows > 0
                ? (true, null)
                : (
                    false,
                    "That account cannot be deleted or is no longer registered.");
        }

        public async Task<(
            bool IsValid,
            string? Role,
            int UserId,
            string? Username,
            string? FullName,
            string? Email,
            string? Phone,
            string? ErrorMessage)> ValidateUserAsync(
                string loginIdentifier,
                string password)
        {
            await using var connection =
                new SqlConnection(_connectionString);

            await connection.OpenAsync();

            const string sql = """
                SELECT TOP (1)
                    Id AS StaffId,
                    Username,
                    FullName,
                    Email,
                    PhoneNumber,
                    PasswordHash,
                    Role,
                    Status
                FROM dbo.Staff
                WHERE Email = @Identifier
                   OR Username = @Identifier;
                """;

            await using var command =
                new SqlCommand(sql, connection);

            command.Parameters.AddWithValue(
                "@Identifier",
                loginIdentifier);

            await using var reader =
                await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                return InvalidCredentials();
            }

            int userId =
                Convert.ToInt32(reader["StaffId"]);

            string? username =
                reader["Username"]?.ToString();

            string? fullName =
                reader["FullName"]?.ToString();

            string? email =
                reader["Email"]?.ToString();

            string? phone =
                reader["PhoneNumber"]?.ToString();

            string? storedHash =
                reader["PasswordHash"]?.ToString();

            string? role =
                reader["Role"]?.ToString();

            string? status =
                reader["Status"]?.ToString();

            if (string.IsNullOrWhiteSpace(storedHash) ||
                !_hashingService.VerifyPassword(
                    password,
                    storedHash))
            {
                return InvalidCredentials();
            }

            if (status == "Pending")
            {
                return (
                    false,
                    null,
                    0,
                    null,
                    null,
                    null,
                    null,
                    "Your account is pending email confirmation.");
            }

            if (status == "Suspended")
            {
                return (
                    false,
                    null,
                    0,
                    null,
                    null,
                    null,
                    null,
                    "This account is suspended and cannot log in until an administrator reactivates it.");
            }

            if (status != "Active")
            {
                return (
                    false,
                    null,
                    0,
                    null,
                    null,
                    null,
                    null,
                    "This account is no longer active and cannot log in.");
            }

            string normalizedRole =
                role is "MainAdmin" or "Admin"
                    ? "Admin"
                    : role ?? "Staff";

            return (
                true,
                normalizedRole,
                userId,
                username,
                fullName,
                email,
                phone,
                null);
        }

        public async Task<(int Id, string Hash)?> FindPasswordResetAccountAsync(string email)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new SqlCommand("SELECT Id, PasswordHash FROM dbo.Staff WHERE Email = @Email AND Status = 'Active'", connection);
            command.Parameters.Add("@Email", SqlDbType.NVarChar, 255).Value = email;
            await using var reader = await command.ExecuteReaderAsync();
            return await reader.ReadAsync() ? (reader.GetInt32(0), reader.GetString(1)) : null;
        }

        public async Task<bool> CompletePasswordResetAsync(int id, string previousHash, string newHash)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            await using var command = new SqlCommand("UPDATE dbo.Staff SET PasswordHash = @NewHash WHERE Id = @Id AND Status = 'Active' AND PasswordHash = @PreviousHash", connection);
            command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
            command.Parameters.Add("@PreviousHash", SqlDbType.NVarChar, 255).Value = previousHash;
            command.Parameters.Add("@NewHash", SqlDbType.NVarChar, 255).Value = newHash;
            return await command.ExecuteNonQueryAsync() == 1;
        }

        public async Task<bool> UpdatePasswordHashAsync(
            int staffId,
            string passwordHash)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            const string sql = """
                UPDATE dbo.Staff
                SET PasswordHash = @PasswordHash
                WHERE Id = @StaffId
                  AND Status = 'Active';
                """;

            await using var command = new SqlCommand(sql, connection);
            command.Parameters.Add("@PasswordHash", SqlDbType.NVarChar, 255)
                .Value = passwordHash;
            command.Parameters.Add("@StaffId", SqlDbType.Int)
                .Value = staffId;

            return await command.ExecuteNonQueryAsync() == 1;
        }

        public async Task<(bool Success, string? ErrorMessage)> UpdateCredentialsAsync(
            int staffId,
            string username,
            string passwordHash)
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            const string duplicateSql = """
                SELECT COUNT(*)
                FROM dbo.Staff
                WHERE Username = @Username AND Id <> @StaffId;
                """;
            await using (var duplicateCommand = new SqlCommand(duplicateSql, connection))
            {
                duplicateCommand.Parameters.Add("@Username", SqlDbType.NVarChar, 50).Value = username;
                duplicateCommand.Parameters.Add("@StaffId", SqlDbType.Int).Value = staffId;
                if (Convert.ToInt32(await duplicateCommand.ExecuteScalarAsync()) > 0)
                {
                    return (false, "Username is already taken. Please choose another username.");
                }
            }

            const string updateSql = """
                UPDATE dbo.Staff
                SET Username = @Username, PasswordHash = @PasswordHash
                WHERE Id = @StaffId AND Status = 'Active';
                """;
            await using var updateCommand = new SqlCommand(updateSql, connection);
            updateCommand.Parameters.Add("@Username", SqlDbType.NVarChar, 50).Value = username;
            updateCommand.Parameters.Add("@PasswordHash", SqlDbType.NVarChar, 255).Value = passwordHash;
            updateCommand.Parameters.Add("@StaffId", SqlDbType.Int).Value = staffId;
            return await updateCommand.ExecuteNonQueryAsync() == 1
                ? (true, null)
                : (false, "The credentials could not be updated. Confirm that the account is active.");
        }

        public async Task<List<RegisteredTrainerInfo>> GetActiveRegisteredTrainersAsync()
        {
            var trainers = new List<RegisteredTrainerInfo>();
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            const string sql = """
                SELECT Id, FullName, Email, PhoneNumber
                FROM dbo.Staff
                WHERE Role = 'Trainer'
                  AND Status = 'Active'
                ORDER BY FullName;
                """;

            await using var command = new SqlCommand(sql, connection);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                trainers.Add(new RegisteredTrainerInfo
                {
                    StaffId = Convert.ToInt32(reader["Id"]),
                    FullName = reader["FullName"]?.ToString() ?? string.Empty,
                    Email = reader["Email"]?.ToString() ?? string.Empty,
                    PhoneNumber = reader["PhoneNumber"]?.ToString() ?? string.Empty
                });
            }

            return trainers;
        }

        private static UserRoleViewModel MapUser(
            SqlDataReader reader)
        {
            return new UserRoleViewModel
            {
                Id = Convert.ToInt32(reader["Id"]),
                FullName =
                    reader["FullName"]?.ToString()
                    ?? string.Empty,
                Username =
                    reader["Username"]?.ToString()
                    ?? string.Empty,
                Email =
                    reader["Email"]?.ToString()
                    ?? string.Empty,
                PhoneNumber =
                    reader["PhoneNumber"]?.ToString()
                    ?? string.Empty,
                Role =
                    reader["Role"]?.ToString()
                    ?? string.Empty,
                Status =
                    reader["Status"]?.ToString()
                    ?? "Active",
                LastLoginAt =
                    reader["LastLoginAt"] is DBNull
                        ? null
                        : Convert.ToDateTime(
                            reader["LastLoginAt"]),
                TodayAttendanceStatus =
                    reader["TodayAttendanceStatus"] is DBNull
                        ? null
                        : reader["TodayAttendanceStatus"].ToString(),
                TodayCheckedInAt =
                    reader["TodayCheckedInAt"] is DBNull
                        ? null
                        : Convert.ToDateTime(reader["TodayCheckedInAt"])
            };
        }

        private static object DbValue(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? DBNull.Value
                : value;
        }

        private static (
            bool IsValid,
            string? Role,
            int UserId,
            string? Username,
            string? FullName,
            string? Email,
            string? Phone,
            string? ErrorMessage) InvalidCredentials()
        {
            return (
                false,
                null,
                0,
                null,
                null,
                null,
                null,
                "Invalid login credentials.");
        }
    }

}
