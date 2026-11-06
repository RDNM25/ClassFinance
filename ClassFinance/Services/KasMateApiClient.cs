using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ClassFinance.Services
{
    internal sealed class KasMateApiClient
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };

        private static readonly HttpClient Http = CreateHttpClient();
        public static KasMateApiClient Instance { get; } = new();

        private string _token;

        private KasMateApiClient()
        {
            JsonOptions.Converters.Add(new JsonStringEnumConverter());
        }

        public async Task CheckHealthAsync()
        {
            using var response = await Http.GetAsync("desktop/health.php");
            await ReadDataAsync<ApiHealth>(response);
        }

        public async Task<ApiUserIdentity> LoginAsync(string username, string password)
        {
            _token = null;
            using var response = await Http.PostAsJsonAsync("auth/login.php",
                new { username, password }, JsonOptions);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                return null;

            var data = await ReadDataAsync<ApiLoginData>(response);
            _token = data.Token;
            return data.User;
        }

        public async Task<ApiUserIdentity> UpdateProfileAsync(string currentPassword, string username, string newPassword)
        {
            using var request = CreateAuthorizedRequest(HttpMethod.Post, "auth/update_profile.php");
            request.Content = JsonContent.Create(new
            {
                currentPassword,
                username,
                newPassword
            }, options: JsonOptions);
            using var response = await Http.SendAsync(request);
            return await ReadDataAsync<ApiUserIdentity>(response);
        }

        public async Task<ApiSnapshotData> GetSnapshotAsync()
        {
            using var request = CreateAuthorizedRequest(HttpMethod.Get, "desktop/snapshot.php");
            using var response = await Http.SendAsync(request);
            return await ReadDataAsync<ApiSnapshotData>(response);
        }

        public async Task ApplyChangesAsync(DatabaseChanges changes)
        {
            using var request = CreateAuthorizedRequest(HttpMethod.Post, "desktop/sync.php");
            request.Content = JsonContent.Create(changes, options: JsonOptions);
            using var response = await Http.SendAsync(request);
            await ReadDataAsync<ApiMessage>(response);
        }

        public async Task LogoutAsync()
        {
            var token = _token;
            if (string.IsNullOrWhiteSpace(token)) return;

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "auth/logout.php");
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                using var response = await Http.SendAsync(request);
                await ReadDataAsync<ApiMessage>(response);
            }
            catch (HttpRequestException)
            {
                // The local session still ends even when Apache is temporarily unreachable.
            }
            catch (TaskCanceledException)
            {
                // Avoid leaving an unobserved fire-and-forget logout exception.
            }
            finally
            {
                if (string.Equals(_token, token, StringComparison.Ordinal))
                    _token = null;
            }
        }

        private static HttpClient CreateHttpClient()
        {
            var baseAddress = Environment.GetEnvironmentVariable("KASMATE_API_BASE_URL")
                ?? "http://127.0.0.1/KasMateAPI/";
            if (!baseAddress.EndsWith("/", StringComparison.Ordinal))
                baseAddress += "/";

            if (!Uri.TryCreate(baseAddress, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                throw new InvalidOperationException("KASMATE_API_BASE_URL must be a valid HTTP or HTTPS URL.");

            return new HttpClient
            {
                BaseAddress = uri,
                Timeout = TimeSpan.FromSeconds(20)
            };
        }

        private HttpRequestMessage CreateAuthorizedRequest(HttpMethod method, string path)
        {
            if (string.IsNullOrWhiteSpace(_token))
                throw new InvalidOperationException("Please sign in to KasMate again.");

            var request = new HttpRequestMessage(method, path);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);
            return request;
        }

        private static async Task<T> ReadDataAsync<T>(HttpResponseMessage response)
        {
            var body = await response.Content.ReadAsStringAsync();
            ApiEnvelope<T> envelope;
            try
            {
                envelope = JsonSerializer.Deserialize<ApiEnvelope<T>>(body, JsonOptions);
            }
            catch (JsonException)
            {
                throw new HttpRequestException("The KasMate API returned an invalid response.", null, response.StatusCode);
            }

            if (!response.IsSuccessStatusCode || envelope?.Success != true || envelope.Data == null)
            {
                var message = envelope?.Error;
                if (string.IsNullOrWhiteSpace(message))
                    message = $"KasMate API request failed ({(int) response.StatusCode}).";
                throw new HttpRequestException(message, null, response.StatusCode);
            }

            return envelope.Data;
        }
    }

    internal sealed class ApiEnvelope<T>
    {
        public bool Success { get; set; }
        public T Data { get; set; }
        public string Error { get; set; }
    }

    internal sealed class ApiHealth
    {
        public string Service { get; set; }
        public string Database { get; set; }
    }

    internal sealed class ApiMessage
    {
        public string Message { get; set; }
    }

    internal sealed class ApiLoginData
    {
        public string Token { get; set; }
        public ApiUserIdentity User { get; set; }
    }

    internal sealed class ApiUserIdentity
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Username { get; set; }
        public string Role { get; set; }
        public int? ClassId { get; set; }
    }

    internal sealed class ApiSnapshotData
    {
        public ApiIdCounters IdCounters { get; set; } = new();
        public System.Collections.Generic.List<ApiClass> Classes { get; set; } = new();
        public System.Collections.Generic.List<ApiSchedule> Schedules { get; set; } = new();
        public System.Collections.Generic.List<ApiStaff> Staff { get; set; } = new();
        public System.Collections.Generic.List<ApiStudent> Students { get; set; } = new();
        public System.Collections.Generic.List<ApiBill> Bills { get; set; } = new();
        public System.Collections.Generic.List<ApiBillStudent> BillStudents { get; set; } = new();
        public System.Collections.Generic.List<ApiTransaction> Transactions { get; set; } = new();
        public System.Collections.Generic.List<ApiNotification> Notifications { get; set; } = new();
    }

    internal sealed class ApiIdCounters
    {
        public int MaxUserId { get; set; }
        public int MaxClassId { get; set; }
        public int MaxScheduleId { get; set; }
        public int MaxBillId { get; set; }
        public int MaxBillStudentId { get; set; }
        public int MaxTransactionId { get; set; }
        public int MaxNotificationId { get; set; }
    }

    internal sealed class ApiClass
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string AcademicYear { get; set; }
    }

    internal sealed class ApiSchedule
    {
        public int Id { get; set; }
        public int ClassId { get; set; }
        public string Name { get; set; }
        public decimal Amount { get; set; }
        public DateTime StartsOn { get; set; }
        public DateTime? EndsOn { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    internal sealed class ApiStaff
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Username { get; set; }
        public string Role { get; set; }
        public int? ClassId { get; set; }
    }

    internal sealed class ApiStudent
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Nis { get; set; }
        public int ClassId { get; set; }
        public decimal Balance { get; set; }
    }

    internal sealed class ApiBill
    {
        public int Id { get; set; }
        public int ClassId { get; set; }
        public string Name { get; set; }
        public decimal Amount { get; set; }
        public DateTime CreatedAt { get; set; }
        public int? ScheduleId { get; set; }
        public DateTime? PeriodStart { get; set; }
        public DateTime? PeriodEnd { get; set; }
        public bool IsArchived { get; set; }
    }

    internal sealed class ApiBillStudent
    {
        public int Id { get; set; }
        public int BillId { get; set; }
        public int StudentId { get; set; }
        public string Status { get; set; }
        public decimal AmountDue { get; set; }
        public decimal AmountPaid { get; set; }
        public decimal AssessedAmount { get; set; }
    }

    internal sealed class ApiTransaction
    {
        public int Id { get; set; }
        public int ClassId { get; set; }
        public string Type { get; set; }
        public string Method { get; set; }
        public decimal Amount { get; set; }
        public string Description { get; set; }
        public int? StudentId { get; set; }
        public int? BillStudentId { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool AffectsKas { get; set; }
        public bool IsRefund { get; set; }
        public bool IsPendingRefund { get; set; }
    }

    internal sealed class ApiNotification
    {
        public int Id { get; set; }
        public int StudentId { get; set; }
        public int? BillId { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
