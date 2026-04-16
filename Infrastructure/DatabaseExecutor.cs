using System.Data;
using System.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using CadProcessorService.Models;

namespace CadProcessorService.Infrastructure;

public class DatabaseExecutor
{
    private readonly string _connectionString;
    private readonly ILogger<DatabaseExecutor> _logger;

    public DatabaseExecutor(IConfiguration configuration, ILogger<DatabaseExecutor> logger)
    {
        _connectionString = configuration.GetConnectionString("CadDatabase")
                           ?? configuration["ConnectionStrings:CadDatabase"]
                           ?? throw new InvalidOperationException("CadDatabase connection string is missing.");

        _logger = logger;
    }

    private SqlConnection CreateConnection() => new SqlConnection(_connectionString);

    public DataTable ExecuteStoredProcedureToDataTable(
        string procName,
        Dictionary<string, object?> parameters)
    {
        var dt = new DataTable();

        using var conn = CreateConnection();
        using var cmd = new SqlCommand(procName, conn)
        {
            CommandType = CommandType.StoredProcedure
        };

        if (parameters != null)
        {
            foreach (var kvp in parameters)
                cmd.Parameters.AddWithValue(kvp.Key, kvp.Value ?? DBNull.Value);
        }

        using var adapter = new SqlDataAdapter(cmd);

        try
        {
            conn.Open();
            adapter.Fill(dt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing stored procedure {ProcName}", procName);
            throw;
        }

        return dt;
    }

    public int ExecuteNonQuery(
        string procName,
        Dictionary<string, object?> parameters,
        string? returnParamName = null)
    {
        using var conn = CreateConnection();
        using var cmd = new SqlCommand(procName, conn)
        {
            CommandType = CommandType.StoredProcedure
        };

        if (parameters != null)
        {
            foreach (var kvp in parameters)
                cmd.Parameters.AddWithValue(kvp.Key, kvp.Value ?? DBNull.Value);
        }

        SqlParameter? returnParam = null;
        if (!string.IsNullOrWhiteSpace(returnParamName))
        {
            returnParam = new SqlParameter(returnParamName, SqlDbType.Int)
            {
                Direction = ParameterDirection.ReturnValue
            };
            cmd.Parameters.Add(returnParam);
        }

        try
        {
            conn.Open();
           // _logger.LogDebug("Executing {Proc} params: {Params}", procName,string.Join(", ", parameters.Select(p => $"{p.Key}={p.Value ?? "NULL"}")));
            cmd.ExecuteNonQuery();

            if (returnParam != null && returnParam.Value != DBNull.Value)
                return Convert.ToInt32(returnParam.Value);

            return 0;
        }
        catch (SqlException ex) when (
               ex.Number == 547   // FK violation
            || ex.Number == 2627  // PK violation
            || ex.Number == 2601  // Unique index
            || ex.Number == 515   // NOT NULL
            || ex.Number == 8114  // Conversion
            || ex.Number == 245   // Conversion
        )
        {
            _logger.LogWarning(
                ex,
                "Data constraint violation in stored procedure {ProcName} (SQL {ErrorNumber})",
                procName,
                ex.Number
            );

            return -1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error executing stored procedure {ProcName}", procName);
            throw;
        }
    }

    // --- DB DRIVEN CONFIG METHODS ---

    public int GetApplicationId(string applicationCode)
    {
        using var conn = CreateConnection();
        using var cmd = new SqlCommand("""
        SELECT ApplicationId
        FROM CAD_Application
        WHERE ApplicationCode = @code AND IsActive = 1
    """, conn);

        cmd.Parameters.AddWithValue("@code", applicationCode);

        conn.Open();
        var result = cmd.ExecuteScalar();

        if (result == null)
            throw new InvalidOperationException($"Application not found: {applicationCode}");

        return Convert.ToInt32(result);
    }

    public List<DbApplication> GetApplications()
    {
        var list = new List<DbApplication>();

        using var conn = CreateConnection();
        using var cmd = new SqlCommand("""
            SELECT ApplicationId, ApplicationCode, ApplicationName, IsActive, CreatedBy, CreatedDate, UpdatedBy, UpdatedDate
            FROM CAD_Application
            ORDER BY ApplicationCode
        """, conn);

        conn.Open();
        using var rdr = cmd.ExecuteReader();

        while (rdr.Read())
        {
            list.Add(new DbApplication
            {
                ApplicationId = rdr.GetInt32(0),
                ApplicationCode = rdr.GetString(1),
                ApplicationName = rdr.IsDBNull(2) ? string.Empty : rdr.GetString(2),
                IsActive = rdr.GetBoolean(3),
                CreatedBy = rdr.GetInt32(4),
                CreatedDate = rdr.GetDateTime(5),
                UpdatedBy = rdr.IsDBNull(6) ? null : rdr.GetInt32(6),
                UpdatedDate = rdr.IsDBNull(7) ? null : rdr.GetDateTime(7)
            });
        }

        return list;
    }

    public int SaveApplication(DbApplication application)
    {
        using var conn = CreateConnection();
        using var cmd = new SqlCommand();
        cmd.Connection = conn;

        if (application.ApplicationId > 0)
        {
            cmd.CommandText = """
                UPDATE CAD_Application
                SET ApplicationCode = @code,
                    ApplicationName = @name,
                    UpdatedBy = 0,
                    UpdatedDate = GETDATE()
                WHERE ApplicationId = @id
            """;
            cmd.Parameters.AddWithValue("@id", application.ApplicationId);
        }
        else
        {
            cmd.CommandText = """
                INSERT INTO CAD_Application (ApplicationCode, ApplicationName, IsActive, CreatedBy, CreatedDate)
                VALUES (@code, @name, 1, 0, GETDATE());
                SELECT SCOPE_IDENTITY();
            """;
        }

        cmd.Parameters.AddWithValue("@code", application.ApplicationCode);
        cmd.Parameters.AddWithValue("@name", application.ApplicationName ?? string.Empty);

        conn.Open();

        if (application.ApplicationId > 0)
        {
            cmd.ExecuteNonQuery();
            return application.ApplicationId;
        }

        return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
    }

    public void DeleteApplication(int applicationId)
    {
        using var conn = CreateConnection();
        using var cmd = new SqlCommand("""
            DELETE FROM CAD_Application
            WHERE ApplicationId = @id
        """, conn);

        cmd.Parameters.AddWithValue("@id", applicationId);
        conn.Open();
        cmd.ExecuteNonQuery();
    }

    public DbApplicationSettings GetApplicationSettings(int applicationId)
    {
        var settings = new DbApplicationSettings { ApplicationId = applicationId };

        using var conn = CreateConnection();
        using var cmd = new SqlCommand("""
            SELECT SettingKey, SettingValue
            FROM CAD_ApplicationSettings
            WHERE ApplicationId = @appId
              AND IsActive = 1
        """, conn);

        cmd.Parameters.AddWithValue("@appId", applicationId);
        conn.Open();

        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            settings.HasSettings = true;
            var key = rdr.GetString(0);
            var value = rdr.GetString(1);

            switch (key)
            {
                case "ServiceMode":
                    settings.ServiceMode = value;
                    break;
                case "PollIntervalSeconds":
                    if (int.TryParse(value, out var poll))
                        settings.PollIntervalSeconds = poll;
                    break;
                case "SystemUserId":
                    if (int.TryParse(value, out var uid))
                        settings.SystemUserId = uid;
                    break;
                case "EnableParallelPipeline":
                    if (bool.TryParse(value, out var parallel))
                        settings.EnableParallelPipeline = parallel;
                    break;
                case "LogFolder":
                    settings.LogFolder = value;
                    break;
                case "MaxQueueSize":
                    if (int.TryParse(value, out var maxQueue))
                        settings.MaxQueueSize = maxQueue;
                    break;
                case "WorkerCount":
                    if (int.TryParse(value, out var workerCount))
                        settings.WorkerCount = workerCount;
                    break;
                default:
                    settings.AdditionalSettings[key] = value;
                    break;
            }
        }

        return settings;
    }

    public void SaveApplicationSettings(DbApplicationSettings settings)
    {
        UpsertApplicationSetting(settings.ApplicationId, "ServiceMode", settings.ServiceMode);
        UpsertApplicationSetting(settings.ApplicationId, "PollIntervalSeconds", settings.PollIntervalSeconds.ToString());
        UpsertApplicationSetting(settings.ApplicationId, "SystemUserId", settings.SystemUserId.ToString());
        UpsertApplicationSetting(settings.ApplicationId, "EnableParallelPipeline", settings.EnableParallelPipeline.ToString());
        UpsertApplicationSetting(settings.ApplicationId, "LogFolder", settings.LogFolder ?? string.Empty);
        UpsertApplicationSetting(settings.ApplicationId, "MaxQueueSize", settings.MaxQueueSize.ToString());
        UpsertApplicationSetting(settings.ApplicationId, "WorkerCount", settings.WorkerCount.ToString());

        if (settings.AdditionalSettings != null)
        {
            foreach (var kvp in settings.AdditionalSettings)
            {
                UpsertApplicationSetting(settings.ApplicationId, kvp.Key, kvp.Value ?? string.Empty);
            }
        }
    }

    public ServiceMetaDataDto GetServiceMetaData(int applicationId)
    {
        var metadata = new ServiceMetaDataDto();

        using var conn = CreateConnection();
        using var cmd = new SqlCommand("""
            SELECT SettingKey, SettingValue
            FROM CAD_ApplicationSettings
            WHERE ApplicationId = @appId
              AND IsActive = 1
        """, conn);

        cmd.Parameters.AddWithValue("@appId", applicationId);
        conn.Open();

        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            var key = rdr.GetString(0);
            var value = rdr.GetString(1);

            switch (key)
            {
                case "ServiceName":
                    metadata.ServiceName = value;
                    break;
                case "ServiceMode":
                    metadata.ServiceMode = value;
                    break;
                case "Description":
                    metadata.Description = value;
                    break;
                default:
                    metadata.AdditionalSettings[key] = value;
                    break;
            }
        }

        return metadata;
    }

    public void SaveServiceMetaData(int applicationId, ServiceMetaDataDto metadata)
    {
        UpsertApplicationSetting(applicationId, "ServiceName", metadata.ServiceName);
        UpsertApplicationSetting(applicationId, "ServiceMode", metadata.ServiceMode);
        UpsertApplicationSetting(applicationId, "Description", metadata.Description);

        if (metadata.AdditionalSettings != null)
        {
            foreach (var kvp in metadata.AdditionalSettings)
            {
                UpsertApplicationSetting(applicationId, kvp.Key, kvp.Value ?? string.Empty);
            }
        }
    }

    public void SaveRetrySettings(int applicationId, DbRetryConfig settings)
    {
        using var conn = CreateConnection();
        using var cmd = new SqlCommand("""
            IF EXISTS (
                SELECT 1 FROM CAD_RetrySettings
                WHERE ApplicationId = @appId AND IsActive = 1
            )
                UPDATE CAD_RetrySettings
                SET Enabled = @enabled,
                    MaxAttempts = @maxAttempts,
                    DelaySeconds = @delaySeconds,
                    UpdatedBy = 0,
                    UpdatedDate = GETDATE()
                WHERE ApplicationId = @appId AND IsActive = 1
            ELSE
                INSERT INTO CAD_RetrySettings
                    (ApplicationId, Enabled, MaxAttempts, DelaySeconds, IsActive, CreatedBy, CreatedDate)
                VALUES
                    (@appId, @enabled, @maxAttempts, @delaySeconds, 1, 0, GETDATE())
        """, conn);

        cmd.Parameters.AddWithValue("@appId", applicationId);
        cmd.Parameters.AddWithValue("@enabled", settings.Enabled);
        cmd.Parameters.AddWithValue("@maxAttempts", settings.MaxAttempts);
        cmd.Parameters.AddWithValue("@delaySeconds", settings.DelaySeconds);

        conn.Open();
        cmd.ExecuteNonQuery();
    }

    public void SaveEmailSettings(int applicationId, DbEmailConfig settings)
    {
        using var conn = CreateConnection();
        using var cmd = new SqlCommand("""
            IF EXISTS (
                SELECT 1 FROM CAD_EmailSettings
                WHERE ApplicationId = @appId AND IsActive = 1
            )
                UPDATE CAD_EmailSettings
                SET Enabled = @enabled,
                    Host = @host,
                    Port = @port,
                    EnableSsl = @enableSsl,
                    FromEmail = @fromEmail,
                    ToEmail = @toEmail,
                    UserName = @userName,
                    Password = @password,
                    SendOnSuccess = @sendOnSuccess,
                    SendOnFailure = @sendOnFailure,
                    UpdatedBy = 0,
                    UpdatedDate = GETDATE()
                WHERE ApplicationId = @appId AND IsActive = 1
            ELSE
                INSERT INTO CAD_EmailSettings
                    (ApplicationId, Enabled, Host, Port, EnableSsl, FromEmail, ToEmail, UserName, Password, SendOnSuccess, SendOnFailure, IsActive, CreatedBy, CreatedDate)
                VALUES
                    (@appId, @enabled, @host, @port, @enableSsl, @fromEmail, @toEmail, @userName, @password, @sendOnSuccess, @sendOnFailure, 1, 0, GETDATE())
        """, conn);

        cmd.Parameters.AddWithValue("@appId", applicationId);
        cmd.Parameters.AddWithValue("@enabled", settings.Enabled);
        cmd.Parameters.AddWithValue("@host", settings.Host ?? string.Empty);
        cmd.Parameters.AddWithValue("@port", settings.Port);
        cmd.Parameters.AddWithValue("@enableSsl", settings.EnableSsl);
        cmd.Parameters.AddWithValue("@fromEmail", settings.FromEmail ?? string.Empty);
        cmd.Parameters.AddWithValue("@toEmail", settings.ToEmail ?? string.Empty);
        cmd.Parameters.AddWithValue("@userName", settings.UserName ?? string.Empty);
        cmd.Parameters.AddWithValue("@password", settings.Password ?? string.Empty);
        cmd.Parameters.AddWithValue("@sendOnSuccess", settings.SendOnSuccess);
        cmd.Parameters.AddWithValue("@sendOnFailure", settings.SendOnFailure);

        conn.Open();
        cmd.ExecuteNonQuery();
    }

    private void UpsertApplicationSetting(int applicationId, string key, string value)
    {
        using var conn = CreateConnection();
        using var cmd = new SqlCommand("""
            IF EXISTS(
                SELECT 1
                FROM CAD_ApplicationSettings
                WHERE ApplicationId = @appId
                  AND SettingKey = @key
                  AND IsActive = 1
            )
                UPDATE CAD_ApplicationSettings
                SET SettingValue = @value,
                    UpdatedBy = 0,
                    UpdatedDate = GETDATE()
                WHERE ApplicationId = @appId
                  AND SettingKey = @key
                  AND IsActive = 1
            ELSE
                INSERT INTO CAD_ApplicationSettings
                    (ApplicationId, SettingKey, SettingValue, IsActive, CreatedBy, CreatedDate)
                VALUES (@appId, @key, @value, 1, 0, GETDATE())
        """, conn);

        cmd.Parameters.AddWithValue("@appId", applicationId);
        cmd.Parameters.AddWithValue("@key", key);
        cmd.Parameters.AddWithValue("@value", value);

        conn.Open();
        cmd.ExecuteNonQuery();
    }

    public DbRetryConfig GetProviderRetrySettings(int providerId)
    {
        var applicationId = GetProviderApplicationId(providerId);
        if (applicationId == 0)
        {
            return new DbRetryConfig { Enabled = false, ProviderId = providerId, ApplicationId = applicationId };
        }

        using var conn = CreateConnection();
        using var cmd = new SqlCommand("""
            SELECT RetrySettingId, ApplicationId, ProviderId, Enabled, MaxAttempts, DelaySeconds
            FROM CAD_RetrySettings
            WHERE ProviderId = @providerId AND IsActive = 1
        """, conn);

        cmd.Parameters.AddWithValue("@providerId", providerId);
        conn.Open();

        using var rdr = cmd.ExecuteReader();
        if (rdr.Read())
        {
            return new DbRetryConfig
            {
                RetrySettingId = rdr.GetInt32(0),
                ApplicationId = rdr.GetInt32(1),
                ProviderId = rdr.GetInt32(2),
                Enabled = rdr.GetBoolean(3),
                MaxAttempts = rdr.GetInt32(4),
                DelaySeconds = rdr.GetInt32(5)
            };
        }

        return new DbRetryConfig { Enabled = false, ProviderId = providerId, ApplicationId = applicationId };
    }

    public void SaveProviderRetrySettings(int providerId, DbRetryConfig settings)
    {
        var applicationId = GetProviderApplicationId(providerId);
        if (applicationId == 0)
        {
            return;
        }

        using var conn = CreateConnection();
        using var cmd = new SqlCommand("""
            IF EXISTS (
                SELECT 1 FROM CAD_RetrySettings
                WHERE ProviderId = @providerId AND IsActive = 1
            )
                UPDATE CAD_RetrySettings
                SET Enabled = @enabled,
                    MaxAttempts = @maxAttempts,
                    DelaySeconds = @delaySeconds,
                    UpdatedBy = 0,
                    UpdatedDate = GETDATE()
                WHERE ProviderId = @providerId AND IsActive = 1
            ELSE
                INSERT INTO CAD_RetrySettings
                    (ApplicationId, ProviderId, Enabled, MaxAttempts, DelaySeconds, IsActive, CreatedBy, CreatedDate)
                VALUES
                    (@appId, @providerId, @enabled, @maxAttempts, @delaySeconds, 1, 0, GETDATE())
        """, conn);

        cmd.Parameters.AddWithValue("@appId", applicationId);
        cmd.Parameters.AddWithValue("@providerId", providerId);
        cmd.Parameters.AddWithValue("@enabled", settings.Enabled);
        cmd.Parameters.AddWithValue("@maxAttempts", settings.MaxAttempts);
        cmd.Parameters.AddWithValue("@delaySeconds", settings.DelaySeconds);

        conn.Open();
        cmd.ExecuteNonQuery();
    }

    public DbEmailConfig GetProviderEmailSettings(int providerId)
    {
        var applicationId = GetProviderApplicationId(providerId);
        if (applicationId == 0)
        {
            return new DbEmailConfig { Enabled = false, ProviderId = providerId, ApplicationId = applicationId };
        }

        using var conn = CreateConnection();
        using var cmd = new SqlCommand("""
            SELECT EmailSettingId, ApplicationId, ProviderId, Enabled, Host, Port, EnableSsl, FromEmail, ToEmail, UserName, Password, SendOnSuccess, SendOnFailure
            FROM CAD_EmailSettings
            WHERE ProviderId = @providerId AND IsActive = 1
        """, conn);

        cmd.Parameters.AddWithValue("@providerId", providerId);
        conn.Open();

        using var rdr = cmd.ExecuteReader();
        if (rdr.Read())
        {
            return new DbEmailConfig
            {
                EmailSettingId = rdr.GetInt32(0),
                ApplicationId = rdr.GetInt32(1),
                ProviderId = rdr.GetInt32(2),
                Enabled = rdr.GetBoolean(3),
                Host = rdr.IsDBNull(4) ? "" : rdr.GetString(4),
                Port = rdr.GetInt32(5),
                EnableSsl = rdr.GetBoolean(6),
                FromEmail = rdr.IsDBNull(7) ? "" : rdr.GetString(7),
                ToEmail = rdr.IsDBNull(8) ? "" : rdr.GetString(8),
                UserName = rdr.IsDBNull(9) ? null : rdr.GetString(9),
                Password = rdr.IsDBNull(10) ? null : rdr.GetString(10),
                SendOnSuccess = rdr.GetBoolean(11),
                SendOnFailure = rdr.GetBoolean(12)
            };
        }

        return new DbEmailConfig { Enabled = false, ProviderId = providerId, ApplicationId = applicationId };
    }

    public void SaveProviderEmailSettings(int providerId, DbEmailConfig settings)
    {
        var applicationId = GetProviderApplicationId(providerId);
        if (applicationId == 0)
        {
            return;
        }

        using var conn = CreateConnection();
        using var cmd = new SqlCommand("""
            IF EXISTS (
                SELECT 1 FROM CAD_EmailSettings
                WHERE ProviderId = @providerId AND IsActive = 1
            )
                UPDATE CAD_EmailSettings
                SET Enabled = @enabled,
                    Host = @host,
                    Port = @port,
                    EnableSsl = @enableSsl,
                    FromEmail = @fromEmail,
                    ToEmail = @toEmail,
                    UserName = @userName,
                    Password = @password,
                    SendOnSuccess = @sendOnSuccess,
                    SendOnFailure = @sendOnFailure,
                    UpdatedBy = 0,
                    UpdatedDate = GETDATE()
                WHERE ProviderId = @providerId AND IsActive = 1
            ELSE
                INSERT INTO CAD_EmailSettings
                    (ApplicationId, ProviderId, Enabled, Host, Port, EnableSsl, FromEmail, ToEmail, UserName, Password, SendOnSuccess, SendOnFailure, IsActive, CreatedBy, CreatedDate)
                VALUES
                    (@appId, @providerId, @enabled, @host, @port, @enableSsl, @fromEmail, @toEmail, @userName, @password, @sendOnSuccess, @sendOnFailure, 1, 0, GETDATE())
        """, conn);

        cmd.Parameters.AddWithValue("@appId", applicationId);
        cmd.Parameters.AddWithValue("@providerId", providerId);
        cmd.Parameters.AddWithValue("@enabled", settings.Enabled);
        cmd.Parameters.AddWithValue("@host", settings.Host ?? string.Empty);
        cmd.Parameters.AddWithValue("@port", settings.Port);
        cmd.Parameters.AddWithValue("@enableSsl", settings.EnableSsl);
        cmd.Parameters.AddWithValue("@fromEmail", settings.FromEmail ?? string.Empty);
        cmd.Parameters.AddWithValue("@toEmail", settings.ToEmail ?? string.Empty);
        cmd.Parameters.AddWithValue("@userName", settings.UserName ?? string.Empty);
        cmd.Parameters.AddWithValue("@password", settings.Password ?? string.Empty);
        cmd.Parameters.AddWithValue("@sendOnSuccess", settings.SendOnSuccess);
        cmd.Parameters.AddWithValue("@sendOnFailure", settings.SendOnFailure);

        conn.Open();
        cmd.ExecuteNonQuery();
    }

    private int GetProviderApplicationId(int providerId)
    {
        using var conn = CreateConnection();
        using var cmd = new SqlCommand("""
            SELECT ApplicationId
            FROM CAD_Provider
            WHERE ProviderId = @provId
              AND IsActive = 1
        """, conn);

        cmd.Parameters.AddWithValue("@provId", providerId);
        conn.Open();

        var result = cmd.ExecuteScalar();
        return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
    }

    private void UpsertProviderSetting(int providerId, string key, string value)
    {
        var applicationId = GetProviderApplicationId(providerId);
        if (applicationId == 0)
        {
            return;
        }

        using var conn = CreateConnection();
        using var cmd = new SqlCommand("""
            IF EXISTS(
                SELECT 1
                FROM CAD_ApplicationSettings
                WHERE ApplicationId = @appId
                  AND SettingKey = @key
                  AND IsActive = 1
            )
                UPDATE CAD_ApplicationSettings
                SET SettingValue = @value,
                    UpdatedBy = 0,
                    UpdatedDate = GETDATE()
                WHERE ApplicationId = @appId
                  AND SettingKey = @key
                  AND IsActive = 1
            ELSE
                INSERT INTO CAD_ApplicationSettings
                    (ApplicationId, SettingKey, SettingValue, IsActive, CreatedBy, CreatedDate)
                VALUES (@appId, @key, @value, 1, 0, GETDATE())
        """, conn);

        cmd.Parameters.AddWithValue("@appId", applicationId);
        cmd.Parameters.AddWithValue("@key", $"Provider:{providerId}:{key}");
        cmd.Parameters.AddWithValue("@value", value);

        conn.Open();
        cmd.ExecuteNonQuery();
    }

    public ServiceMetaDataDto GetProviderServiceMetaData(int providerId)
    {
        var metadata = new ServiceMetaDataDto();
        var applicationId = GetProviderApplicationId(providerId);
        if (applicationId == 0)
        {
            return metadata;
        }

        using var conn = CreateConnection();
        using var cmd = new SqlCommand("""
            SELECT SettingKey, SettingValue
            FROM CAD_ApplicationSettings
            WHERE ApplicationId = @appId
              AND SettingKey LIKE @keyPrefix
              AND IsActive = 1
        """, conn);

        cmd.Parameters.AddWithValue("@appId", applicationId);
        cmd.Parameters.AddWithValue("@keyPrefix", $"Provider:{providerId}:%");
        conn.Open();

        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            var rawKey = rdr.GetString(0);
            var value = rdr.GetString(1);
            var key = rawKey.Substring(rawKey.IndexOf(':', rawKey.IndexOf(':') + 1) + 1);

            switch (key)
            {
                case "ServiceName":
                    metadata.ServiceName = value;
                    break;
                case "ServiceMode":
                    metadata.ServiceMode = value;
                    break;
                case "Description":
                    metadata.Description = value;
                    break;
                default:
                    metadata.AdditionalSettings[key] = value;
                    break;
            }
        }

        return metadata;
    }

    public void SaveProviderServiceMetaData(int providerId, ServiceMetaDataDto metadata)
    {
        UpsertProviderSetting(providerId, "ServiceName", metadata.ServiceName);
        UpsertProviderSetting(providerId, "ServiceMode", metadata.ServiceMode);
        UpsertProviderSetting(providerId, "Description", metadata.Description);

        if (metadata.AdditionalSettings != null)
        {
            foreach (var kvp in metadata.AdditionalSettings)
            {
                UpsertProviderSetting(providerId, kvp.Key, kvp.Value ?? string.Empty);
            }
        }
    }

    public List<DbServiceMetaData> GetProviderServiceMetaDataRows(int providerId)
    {
        var list = new List<DbServiceMetaData>();
        using var conn = CreateConnection();
        using var cmd = new SqlCommand("""
            SELECT CAD_ServiceMetaDataId, ApplicationId, ProviderId, OperatorType, Value, ORINum, IsActive, CreatedBy, CreatedDate, UpdatedBy, UpdatedDate
            FROM CAD_ServiceMetaData
            WHERE ProviderId = @providerId
              
        """, conn);

        cmd.Parameters.AddWithValue("@providerId", providerId);

        conn.Open();
        using var rdr = cmd.ExecuteReader();
        while (rdr.Read())
        {
            list.Add(new DbServiceMetaData
            {
                CdServiceMetaDataId = rdr.GetInt32(0),
                ApplicationId = rdr.GetInt32(1),
                ProviderId = rdr.GetInt32(2),
                OperatorType = rdr.IsDBNull(3) ? 0 : rdr.GetInt32(3),
                Value = rdr.IsDBNull(4) ? string.Empty : rdr.GetString(4),
                ORINum = rdr.IsDBNull(5) ? string.Empty : rdr.GetString(5),
                IsActive = rdr.GetBoolean(6),
                CreatedBy = rdr.IsDBNull(7) ? 0 : rdr.GetInt32(7),
                CreatedDate = rdr.IsDBNull(8) ? DateTime.MinValue : rdr.GetDateTime(8),
                UpdatedBy = rdr.IsDBNull(9) ? 0 : rdr.GetInt32(9),
                UpdatedDate = rdr.IsDBNull(10) ? null : rdr.GetDateTime(10),
            });
        }

        return list;
    }

    public int SaveProviderServiceMetaDataRow(int providerId, DbServiceMetaData metadata)
    {
        var applicationId = GetProviderApplicationId(providerId);
        if (applicationId == 0)
        {
            return 0;
        }

        using var conn = CreateConnection();
        using var cmd = new SqlCommand(metadata.CdServiceMetaDataId > 0 ? """
            UPDATE CAD_ServiceMetaData
            SET OperatorType = @operatorType,
                Value = @value,
                ORINum = @oriNum,
                IsActive = @isActive,
                UpdatedBy = 0,
                UpdatedDate = GETDATE()
            WHERE CAD_ServiceMetaDataId = @id
        """ : """
            INSERT INTO CAD_ServiceMetaData
                (ApplicationId, ProviderId, OperatorType, Value, ORINum, IsActive, CreatedBy, CreatedDate)
            VALUES
                (@applicationId, @providerId, @operatorType, @value, @oriNum, @isActive, 0, GETDATE());
            SELECT SCOPE_IDENTITY();
        """, conn);

        cmd.Parameters.AddWithValue("@applicationId", applicationId);
        cmd.Parameters.AddWithValue("@providerId", providerId);
        cmd.Parameters.AddWithValue("@operatorType", metadata.OperatorType);
        cmd.Parameters.AddWithValue("@value", metadata.Value ?? string.Empty);
        cmd.Parameters.AddWithValue("@oriNum", metadata.ORINum ?? string.Empty);
        cmd.Parameters.AddWithValue("@isActive", metadata.IsActive);
        cmd.Parameters.AddWithValue("@id", metadata.CdServiceMetaDataId);

        conn.Open();
        if (metadata.CdServiceMetaDataId > 0)
        {
            cmd.ExecuteNonQuery();
            return metadata.CdServiceMetaDataId;
        }

        var insertedId = Convert.ToInt32(cmd.ExecuteScalar());
        return insertedId;
    }

  public void DeleteProviderServiceMetaData(int serviceMetaDataId)
{
    using var conn = CreateConnection();
    using var cmd = new SqlCommand(@"
        DELETE FROM CAD_ServiceMetaData
        WHERE CAD_ServiceMetaDataId = @id
    ", conn);

    cmd.Parameters.AddWithValue("@id", serviceMetaDataId);

    conn.Open();
    cmd.ExecuteNonQuery();
}
    public List<DbProviderConfig> GetProviders(int applicationId)
    {
        var list = new List<DbProviderConfig>();

        using var conn = CreateConnection();
        using var cmd = new SqlCommand("""
            SELECT ProviderId, ApplicationId, ProviderCode, ProviderName, IdentificationPath, IdentifierValue, CallerNameNode, CallNumberNode, PrimaryOfficerNameNode, OfficersNode
            FROM CAD_Provider
            WHERE ApplicationId = @appId AND IsActive = 1
        """, conn);

        cmd.Parameters.AddWithValue("@appId", applicationId);

        conn.Open();
        using var rdr = cmd.ExecuteReader();

        while (rdr.Read())
        {
            list.Add(new DbProviderConfig
            {
                ProviderId = rdr.GetInt32(0),
                ApplicationId = rdr.GetInt32(1),
                ProviderCode = rdr.GetString(2),
                ProviderName = rdr.IsDBNull(3) ? string.Empty : rdr.GetString(3),
                IdentificationPath = rdr.IsDBNull(4) ? string.Empty : rdr.GetString(4),
                IdentifierValue = rdr.IsDBNull(5) ? string.Empty : rdr.GetString(5),
                CallerNameNode = rdr.IsDBNull(6) ? string.Empty : rdr.GetString(6),
                CallNumberNode = rdr.IsDBNull(7) ? string.Empty : rdr.GetString(7),
                PrimaryOfficerNameNode = rdr.IsDBNull(8) ? string.Empty : rdr.GetString(8),
                OfficersNode = rdr.IsDBNull(9) ? string.Empty : rdr.GetString(9)
            });
        }

        return list;
    }

    public int SaveProvider(DbProviderConfig provider)
    {
        using var conn = CreateConnection();
        using var cmd = new SqlCommand();
        cmd.Connection = conn;

        if (provider.ProviderId > 0)
        {
            cmd.CommandText = """
                UPDATE CAD_Provider
                SET ProviderCode = @code,
                    ProviderName = @name,
                    IdentificationPath = @identificationPath,
                    IdentifierValue = @identifierValue,
                    CallerNameNode = @callerNameNode,
                    CallNumberNode = @callNumberNode,
                    PrimaryOfficerNameNode = @primaryOfficerNameNode,
                    OfficersNode = @officersNode,
                    UpdatedBy = 0,
                    UpdatedDate = GETDATE()
                WHERE ProviderId = @id
            """;
            cmd.Parameters.AddWithValue("@id", provider.ProviderId);
        }
        else
        {
            cmd.CommandText = """
                INSERT INTO CAD_Provider
                    (ApplicationId, ProviderCode, ProviderName, IdentificationPath, IdentifierValue, CallerNameNode, CallNumberNode, PrimaryOfficerNameNode, OfficersNode, IsActive, CreatedBy, CreatedDate)
                VALUES
                    (@applicationId, @code, @name, @identificationPath, @identifierValue, @callerNameNode, @callNumberNode, @primaryOfficerNameNode, @officersNode, 1, 0, GETDATE());
                SELECT SCOPE_IDENTITY();
            """;
            cmd.Parameters.AddWithValue("@applicationId", provider.ApplicationId);
        }

        cmd.Parameters.AddWithValue("@code", provider.ProviderCode);
        cmd.Parameters.AddWithValue("@name", provider.ProviderName ?? string.Empty);
        cmd.Parameters.AddWithValue("@identificationPath", provider.IdentificationPath ?? string.Empty);
        cmd.Parameters.AddWithValue("@identifierValue", provider.IdentifierValue ?? string.Empty);
        cmd.Parameters.AddWithValue("@callerNameNode", provider.CallerNameNode ?? string.Empty);
        cmd.Parameters.AddWithValue("@callNumberNode", provider.CallNumberNode ?? string.Empty);
        cmd.Parameters.AddWithValue("@primaryOfficerNameNode", provider.PrimaryOfficerNameNode ?? string.Empty);
        cmd.Parameters.AddWithValue("@officersNode", provider.OfficersNode ?? string.Empty);

        conn.Open();
        if (provider.ProviderId > 0)
        {
            cmd.ExecuteNonQuery();
            return provider.ProviderId;
        }

        return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
    }

    public void DeleteProvider(int providerId)
    {
        using var conn = CreateConnection();
        conn.Open();
        using var transaction = conn.BeginTransaction();
        try
        {
            // Delete in order to avoid foreign key violations
            // Delete service metadata first
            using (var cmd = new SqlCommand("""
                DELETE FROM CAD_ServiceMetaData
                WHERE ProviderId = @id
            """, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@id", providerId);
                cmd.ExecuteNonQuery();
            }

            // Delete provider field rules
            using (var cmd = new SqlCommand("""
                DELETE FROM CAD_ProviderFieldRules
                WHERE ProviderId = @id
            """, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@id", providerId);
                cmd.ExecuteNonQuery();
            }

            // Delete field mappings
            using (var cmd = new SqlCommand("""
                DELETE FROM CAD_FieldMapping
                WHERE ProviderId = @id
            """, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@id", providerId);
                cmd.ExecuteNonQuery();
            }

            // Delete procedures
            using (var cmd = new SqlCommand("""
                DELETE FROM CAD_Procedure
                WHERE ProviderId = @id
            """, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@id", providerId);
                cmd.ExecuteNonQuery();
            }

            // Delete provider folder config
            using (var cmd = new SqlCommand("""
                DELETE FROM CAD_ProviderFolderConfig
                WHERE ProviderId = @id
            """, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@id", providerId);
                cmd.ExecuteNonQuery();
            }

            // Delete provider retry settings
            using (var cmd = new SqlCommand("""
                DELETE FROM CAD_RetrySettings
                WHERE ProviderId = @id
            """, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@id", providerId);
                cmd.ExecuteNonQuery();
            }

            // Delete provider email settings
            using (var cmd = new SqlCommand("""
                DELETE FROM CAD_EmailSettings
                WHERE ProviderId = @id
            """, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@id", providerId);
                cmd.ExecuteNonQuery();
            }

            // Finally delete the provider
            using (var cmd = new SqlCommand("""
                DELETE FROM CAD_Provider
                WHERE ProviderId = @id
            """, conn, transaction))
            {
                cmd.Parameters.AddWithValue("@id", providerId);
                cmd.ExecuteNonQuery();
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public DbFolderConfig? GetProviderFolders(int providerId)
    {
        using var conn = CreateConnection();
        using var cmd = new SqlCommand("""
            SELECT FolderConfigId, ApplicationId, ProviderId, SourceFolder, DoneFolder, ErrorFolder, RetryFolder, OtherAgencyFolder
            FROM CAD_ProviderFolderConfig
            WHERE ProviderId = @pid AND IsActive = 1
        """, conn);

        cmd.Parameters.AddWithValue("@pid", providerId);

        conn.Open();
        using var rdr = cmd.ExecuteReader();

        if (!rdr.Read())
        {
            _logger.LogWarning("No folder configuration found for ProviderId={ProviderId}. Provider will be skipped.", providerId);
            return null;
        }

        return new DbFolderConfig
        {
            FolderConfigId = rdr.GetInt32(0),
            ApplicationId = rdr.GetInt32(1),
            ProviderId = rdr.GetInt32(2),
            SourceFolder = rdr.GetString(3),
            DoneFolder = rdr.GetString(4),
            ErrorFolder = rdr.GetString(5),
            RetryFolder = rdr.GetString(6),
            OtherAgencyFolder = rdr.GetString(7)
        };
    }

    public void DeleteFieldMapping(int mappingId)
{
    using var conn = CreateConnection();
    using var cmd = new SqlCommand("""
        DELETE FROM CAD_FieldMapping
        WHERE MappingId = @mappingId
    """, conn);

    cmd.Parameters.AddWithValue("@mappingId", mappingId);
    conn.Open();
    cmd.ExecuteNonQuery();
}

   public int SaveProviderFolders(int providerId, DbFolderConfig folderConfig)
{
    var applicationId = GetProviderApplicationId(providerId);
    if (applicationId == 0) return 0;

    using var conn = CreateConnection();
    conn.Open();

    // Step 1: Check if exists
    using var checkCmd = new SqlCommand("""
        SELECT COUNT(1) FROM CAD_ProviderFolderConfig
        WHERE ProviderId = @pid AND IsActive = 1
    """, conn);
    checkCmd.Parameters.AddWithValue("@pid", providerId);
    var exists = (int)checkCmd.ExecuteScalar() > 0;

    // Step 2: Update or Insert separately
    if (exists)
    {
        using var updateCmd = new SqlCommand("""
            UPDATE CAD_ProviderFolderConfig
            SET SourceFolder = @sourceFolder,
                DoneFolder = @doneFolder,
                ErrorFolder = @errorFolder,
                RetryFolder = @retryFolder,
                OtherAgencyFolder = @otherAgencyFolder,
                UpdatedBy = 0,
                UpdatedDate = GETDATE()
            WHERE ProviderId = @pid AND IsActive = 1
        """, conn);
        updateCmd.Parameters.AddWithValue("@pid", providerId);
        updateCmd.Parameters.AddWithValue("@sourceFolder", folderConfig.SourceFolder ?? string.Empty);
        updateCmd.Parameters.AddWithValue("@doneFolder", folderConfig.DoneFolder ?? string.Empty);
        updateCmd.Parameters.AddWithValue("@errorFolder", folderConfig.ErrorFolder ?? string.Empty);
        updateCmd.Parameters.AddWithValue("@retryFolder", folderConfig.RetryFolder ?? string.Empty);
        updateCmd.Parameters.AddWithValue("@otherAgencyFolder", folderConfig.OtherAgencyFolder ?? string.Empty);
        updateCmd.ExecuteNonQuery();
        return folderConfig.FolderConfigId;
    }
    else
    {
        using var insertCmd = new SqlCommand("""
            INSERT INTO CAD_ProviderFolderConfig
                (ApplicationId, ProviderId, SourceFolder, DoneFolder, ErrorFolder, RetryFolder, OtherAgencyFolder, IsActive, CreatedBy, CreatedDate)
            VALUES
                (@applicationId, @pid, @sourceFolder, @doneFolder, @errorFolder, @retryFolder, @otherAgencyFolder, 1, 0, GETDATE());
            SELECT SCOPE_IDENTITY();
        """, conn);
        insertCmd.Parameters.AddWithValue("@applicationId", applicationId);
        insertCmd.Parameters.AddWithValue("@pid", providerId);
        insertCmd.Parameters.AddWithValue("@sourceFolder", folderConfig.SourceFolder ?? string.Empty);
        insertCmd.Parameters.AddWithValue("@doneFolder", folderConfig.DoneFolder ?? string.Empty);
        insertCmd.Parameters.AddWithValue("@errorFolder", folderConfig.ErrorFolder ?? string.Empty);
        insertCmd.Parameters.AddWithValue("@retryFolder", folderConfig.RetryFolder ?? string.Empty);
        insertCmd.Parameters.AddWithValue("@otherAgencyFolder", folderConfig.OtherAgencyFolder ?? string.Empty);
        return Convert.ToInt32(insertCmd.ExecuteScalar() ?? 0);
    }
}

    public List<DbProcedureConfig> GetProcedures(int providerId)
    {
        var list = new List<DbProcedureConfig>();

        using var conn = CreateConnection();
        using var cmd = new SqlCommand("""
            SELECT ProcedureId, ProcedureName, ExecutionOrder, IsRepeatable
            FROM CAD_Procedure
            WHERE ProviderId = @providerId AND IsActive = 1
            ORDER BY ExecutionOrder
        """, conn);

        cmd.Parameters.AddWithValue("@providerId", providerId);

        conn.Open();
        using var rdr = cmd.ExecuteReader();

        while (rdr.Read())
        {
            list.Add(new DbProcedureConfig
            {
                ProcedureId = rdr.GetInt32(0),
                ProcedureName = rdr.GetString(1),
                ExecutionOrder = rdr.GetInt32(2),
                IsRepeatable = rdr.GetBoolean(3)
            });
        }

        return list;
    }

   public int SaveProcedure(int providerId, DbProcedureConfig procedure)
{

    var applicationId = GetProviderApplicationId(providerId);

    using var conn = CreateConnection();
    using var cmd = new SqlCommand();
    cmd.Connection = conn;

    if (procedure.ProcedureId > 0)
    {
        cmd.CommandText = """
            UPDATE CAD_Procedure
            SET ProcedureName = @name,
                ExecutionOrder = @executionOrder,
                IsRepeatable = @isRepeatable,
                UpdatedBy = 0,
                UpdatedDate = GETDATE()
            WHERE ProcedureId = @id
        """;
        cmd.Parameters.AddWithValue("@id", procedure.ProcedureId);
    }
    else
    {
        cmd.CommandText = """
            INSERT INTO CAD_Procedure
                (ApplicationId, ProviderId, ProcedureName, ExecutionOrder, IsRepeatable, IsActive, CreatedBy, CreatedDate)
            VALUES
                (@applicationId, @providerId, @name, @executionOrder, @isRepeatable, 1, 0, GETDATE());
            SELECT SCOPE_IDENTITY();
        """;
        cmd.Parameters.AddWithValue("@applicationId", applicationId); 
        cmd.Parameters.AddWithValue("@providerId", providerId);
    }

    cmd.Parameters.AddWithValue("@name", procedure.ProcedureName ?? string.Empty);
    cmd.Parameters.AddWithValue("@executionOrder", procedure.ExecutionOrder);
    cmd.Parameters.AddWithValue("@isRepeatable", procedure.IsRepeatable);

    conn.Open();
    if (procedure.ProcedureId > 0)
    {
        cmd.ExecuteNonQuery();
        return procedure.ProcedureId;
    }

    return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
}
    public void DeleteProcedure(int procedureId)
    {
        using var conn = CreateConnection();
        using var cmd = new SqlCommand("""
            DELETE FROM CAD_Procedure
            WHERE ProcedureId = @id
        """, conn);

        cmd.Parameters.AddWithValue("@id", procedureId);
        conn.Open();
        cmd.ExecuteNonQuery();
    }

    public List<DbFieldMapping> GetFieldMappings(int providerId, int procedureId)
    {
        var list = new List<DbFieldMapping>();

        using var conn = CreateConnection();
        using var cmd = new SqlCommand("""
            SELECT MappingId, ApplicationId, ProviderId, ProcedureId, ParameterName, XmlPath, IsRequired, DefaultValue, IsActive
            FROM CAD_FieldMapping
            WHERE ProviderId = @pid
              AND ProcedureId = @procId
              AND IsActive = 1
        """, conn);

        cmd.Parameters.AddWithValue("@pid", providerId);
        cmd.Parameters.AddWithValue("@procId", procedureId);

        conn.Open();
        using var rdr = cmd.ExecuteReader();

        while (rdr.Read())
        {
            list.Add(new DbFieldMapping
            {
                MappingId = rdr.GetInt32(0),
                ApplicationId = rdr.GetInt32(1),
                ProviderId = rdr.GetInt32(2),
                ProcedureId = rdr.GetInt32(3),
                ParameterName = rdr.GetString(4),
                XmlPath = rdr.GetString(5),
                IsRequired = rdr.GetBoolean(6),
                DefaultValue = rdr.IsDBNull(7) ? null : rdr.GetString(7),
                IsActive = rdr.GetBoolean(8)
            });
        }

        return list;
    }

    public List<DbFieldMapping> GetFieldMappings(int providerId)
    {
        var list = new List<DbFieldMapping>();

        using var conn = CreateConnection();
        using var cmd = new SqlCommand("""
            SELECT MappingId, ApplicationId, ProviderId, ProcedureId, ParameterName, XmlPath, IsRequired, DefaultValue, IsActive
            FROM CAD_FieldMapping
            WHERE ProviderId = @pid
              AND IsActive = 1
        """, conn);

        cmd.Parameters.AddWithValue("@pid", providerId);

        conn.Open();
        using var rdr = cmd.ExecuteReader();

        while (rdr.Read())
        {
            list.Add(new DbFieldMapping
            {
                MappingId = rdr.GetInt32(0),
                ApplicationId = rdr.GetInt32(1),
                ProviderId = rdr.GetInt32(2),
                ProcedureId = rdr.GetInt32(3),
                ParameterName = rdr.GetString(4),
                XmlPath = rdr.GetString(5),
                IsRequired = rdr.GetBoolean(6),
                DefaultValue = rdr.IsDBNull(7) ? null : rdr.GetString(7),
                IsActive = rdr.GetBoolean(8)
            });
        }

        return list;
    }

public int SaveFieldMapping(int providerId, int procedureId, DbFieldMapping mapping)
{
    var applicationId = GetProviderApplicationId(providerId);

   
    if (mapping.MappingId == 0 && procedureId <= 0)
    {
        _logger.LogWarning("SaveFieldMapping skipped: procedureId is 0 for providerId={ProviderId}", providerId);
        return 0;
    }

    using var conn = CreateConnection();
    using var cmd = new SqlCommand();
    cmd.Connection = conn;

    if (mapping.MappingId > 0)
    {
        cmd.CommandText = """
            UPDATE CAD_FieldMapping
            SET ParameterName = @parameterName,
                XmlPath = @xmlPath,
                IsRequired = @isRequired,
                DefaultValue = @defaultValue
            WHERE MappingId = @mappingId
        """;
        cmd.Parameters.AddWithValue("@mappingId", mapping.MappingId);
    }
    else
    {
        cmd.CommandText = """
            INSERT INTO CAD_FieldMapping
                (ApplicationId, ProviderId, ProcedureId, ParameterName, XmlPath, IsRequired, DefaultValue, IsActive, CreatedBy, CreatedDate)
            VALUES
                (@applicationId, @providerId, @procedureId, @parameterName, @xmlPath, @isRequired, @defaultValue, 1, 0, GETDATE());
            SELECT SCOPE_IDENTITY();
        """;
        cmd.Parameters.AddWithValue("@applicationId", applicationId);
        cmd.Parameters.AddWithValue("@providerId", providerId);
        cmd.Parameters.AddWithValue("@procedureId", procedureId); // ✅ must be > 0
    }

    cmd.Parameters.AddWithValue("@parameterName", mapping.ParameterName ?? string.Empty);
    cmd.Parameters.AddWithValue("@xmlPath", mapping.XmlPath ?? string.Empty);
    cmd.Parameters.AddWithValue("@isRequired", mapping.IsRequired);
    cmd.Parameters.AddWithValue("@defaultValue", mapping.DefaultValue ?? string.Empty);

    conn.Open();
    if (mapping.MappingId > 0)
    {
        cmd.ExecuteNonQuery();
        return mapping.MappingId;
    }

    return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
}

    public List<DbProviderFieldRule> GetProviderFieldRules(int providerId)
    {
        var list = new List<DbProviderFieldRule>();

        using var conn = CreateConnection();
        using var cmd = new SqlCommand(@"
        SELECT RuleId, ProviderId, ProcedureId, ParameterName, RuleType, RuleValue, RuleCategory, RuleOrder, IsActive
        FROM CAD_ProviderFieldRules
        WHERE ProviderId = @pid
          AND IsActive = 1
        ORDER BY RuleOrder
    ", conn);

        cmd.Parameters.AddWithValue("@pid", providerId);

        conn.Open();
        using var rdr = cmd.ExecuteReader();

        while (rdr.Read())
        {
            list.Add(new DbProviderFieldRule
            {
                RuleId = rdr.GetInt32(0),
                ProviderId = rdr.GetInt32(1),
                ProcedureId = rdr.GetInt32(2),
                ParameterName = rdr.GetString(3),
                RuleType = rdr.GetString(4),
                RuleValue = rdr.IsDBNull(5) ? null : rdr.GetString(5),
                RuleCategory = rdr.IsDBNull(6) ? null : rdr.GetString(6),
                RuleOrder = rdr.GetInt32(7),
                IsActive = rdr.GetBoolean(8)
            });
        }

        return list;
    }

    public List<DbProviderFieldRule> GetProviderFieldRules(int providerId, int procedureId)
    {
        var list = new List<DbProviderFieldRule>();

        using var conn = CreateConnection();
        using var cmd = new SqlCommand(@"
        SELECT RuleId, ParameterName, RuleType, RuleValue, RuleCategory, RuleOrder, IsActive
        FROM CAD_ProviderFieldRules
        WHERE ProviderId = @pid
          AND ProcedureId = @procId
          AND IsActive = 1
        ORDER BY RuleOrder
    ", conn);

        cmd.Parameters.AddWithValue("@pid", providerId);
        cmd.Parameters.AddWithValue("@procId", procedureId);

        conn.Open();
        using var rdr = cmd.ExecuteReader();

        while (rdr.Read())
        {
            list.Add(new DbProviderFieldRule
            {
                RuleId = rdr.GetInt32(0),
                ParameterName = rdr.GetString(1),
                RuleType = rdr.GetString(2),
                RuleValue = rdr.IsDBNull(3) ? null : rdr.GetString(3),
                RuleCategory = rdr.IsDBNull(4) ? null : rdr.GetString(4),
                RuleOrder = rdr.GetInt32(5),
                IsActive = rdr.GetBoolean(6)
            });
        }

        return list;
    }

    public int SaveProviderFieldRule(int providerId, DbProviderFieldRule rule)
    {
        using var conn = CreateConnection();
        using var cmd = new SqlCommand();
        cmd.Connection = conn;

        if (rule.RuleId > 0)
        {
            cmd.CommandText = """
    UPDATE CAD_ProviderFieldRules
    SET ProviderId = @providerId,
        ProcedureId = @procedureId,
        ParameterName = @parameterName,
        RuleType = @ruleType,
        RuleValue = @ruleValue,
        RuleCategory = @ruleCategory,
        RuleOrder = @ruleOrder
    WHERE RuleId = @ruleId
""";
            cmd.Parameters.AddWithValue("@ruleId", rule.RuleId);
        }
        else
        {
            cmd.CommandText = """
                INSERT INTO CAD_ProviderFieldRules
                    (ProviderId, ProcedureId, ParameterName, RuleType, RuleValue, RuleCategory, RuleOrder, IsActive, CreatedBy, CreatedDate)
                VALUES
                    (@providerId, @procedureId, @parameterName, @ruleType, @ruleValue, @ruleCategory, @ruleOrder, 1, 0, GETDATE());
                SELECT SCOPE_IDENTITY();
            """;
        }

        cmd.Parameters.AddWithValue("@providerId", providerId);
        cmd.Parameters.AddWithValue("@procedureId", rule.ProcedureId);
        cmd.Parameters.AddWithValue("@parameterName", rule.ParameterName ?? string.Empty);
        cmd.Parameters.AddWithValue("@ruleType", rule.RuleType ?? string.Empty);
        cmd.Parameters.AddWithValue("@ruleValue", rule.RuleValue ?? string.Empty);
        cmd.Parameters.AddWithValue("@ruleCategory", rule.RuleCategory ?? string.Empty);
        cmd.Parameters.AddWithValue("@ruleOrder", rule.RuleOrder);

        conn.Open();

        if (rule.RuleId > 0)
        {
            cmd.ExecuteNonQuery();
            return rule.RuleId;
        }

        return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
    }

    public void DeleteProviderFieldRule(int ruleId)
    {
        using var conn = CreateConnection();
        using var cmd = new SqlCommand("""
            DELETE FROM CAD_ProviderFieldRules
            WHERE RuleId = @ruleId
        """, conn);

        cmd.Parameters.AddWithValue("@ruleId", ruleId);
        conn.Open();
        cmd.ExecuteNonQuery();
    }

    public List<DbCallerRuleCategoryConfig> GetCallerRuleCategoryConfig(int applicationId)
    {
        var list = new List<DbCallerRuleCategoryConfig>();

        using var conn = CreateConnection();
        using var cmd = new SqlCommand(@"
        SELECT ConfigId, ApplicationId, CategoryName, CategoryRole, 
               IsRequired, FallbackRole, IsActive
        FROM CAD_CallerRuleCategoryConfig
        WHERE ApplicationId = @appId
          AND IsActive = 1
    ", conn);

        cmd.Parameters.AddWithValue("@appId", applicationId);

        conn.Open();
        using var rdr = cmd.ExecuteReader();

        while (rdr.Read())
        {
            list.Add(new DbCallerRuleCategoryConfig
            {
                ConfigId = rdr.GetInt32(0),
                ApplicationId = rdr.GetInt32(1),
                CategoryName = rdr.GetString(2),
                CategoryRole = rdr.GetString(3),
                IsRequired = rdr.GetBoolean(4),
                FallbackRole = rdr.IsDBNull(5) ? null : rdr.GetString(5),
                IsActive = rdr.GetBoolean(6)
            });
        }

        return list;
    }



    public DbRetryConfig GetRetrySettings(int applicationId)
    {
        using var conn = CreateConnection();
        using var cmd = new SqlCommand("""
            SELECT ApplicationId, Enabled, MaxAttempts, DelaySeconds
            FROM CAD_RetrySettings
            WHERE ApplicationId = @appId AND IsActive = 1
        """, conn);

        cmd.Parameters.AddWithValue("@appId", applicationId);

        conn.Open();
        using var rdr = cmd.ExecuteReader();

        if (!rdr.Read())
            return new DbRetryConfig { Enabled = false, ApplicationId = applicationId };

        return new DbRetryConfig
        {
            ApplicationId = rdr.GetInt32(0),
            Enabled = rdr.GetBoolean(1),
            MaxAttempts = rdr.GetInt32(2),
            DelaySeconds = rdr.GetInt32(3)
        };
    }

    public DbEmailConfig GetEmailSettings(int applicationId)
    {
        using var conn = CreateConnection();
        using var cmd = new SqlCommand("""
            SELECT ApplicationId, Enabled, Host, Port, EnableSsl, FromEmail, ToEmail,
                   UserName, Password, SendOnSuccess, SendOnFailure
            FROM CAD_EmailSettings
            WHERE ApplicationId = @appId AND IsActive = 1
        """, conn);

        cmd.Parameters.AddWithValue("@appId", applicationId);

        conn.Open();
        using var rdr = cmd.ExecuteReader();

        if (!rdr.Read())
            return new DbEmailConfig { Enabled = false, ApplicationId = applicationId };

        return new DbEmailConfig
        {
            ApplicationId = rdr.GetInt32(0),
            Enabled = rdr.GetBoolean(1),
            Host = rdr.GetString(2),
            Port = rdr.GetInt32(3),
            EnableSsl = rdr.GetBoolean(4),
            FromEmail = rdr.GetString(5),
            ToEmail = rdr.GetString(6),
            UserName = rdr.IsDBNull(7) ? null : rdr.GetString(7),
            Password = rdr.IsDBNull(8) ? null : rdr.GetString(8),
            SendOnSuccess = rdr.GetBoolean(9),
            SendOnFailure = rdr.GetBoolean(10)
        };
    }
}
