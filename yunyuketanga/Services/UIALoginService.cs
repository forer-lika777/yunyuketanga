using yunyuketanga.Constants;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace yunyuketanga.Services;

public class UIALoginService
{
    private readonly HttpClient httpClient;
    private readonly HttpClientHandler httpClientHandler;

    private const string AES_CHARS = "ABCDEFGHJKMNPQRSTWXYZabcdefhijkmnprstwxyz2345678";
    private readonly Random random;

    public UIALoginService(HttpClient httpClient, HttpClientHandler httpClientHandler)
    {
        this.httpClient = httpClient;
        this.httpClientHandler = httpClientHandler;

        random = new Random();
    }

    public async Task<LoginResult> LoginAsync(LoginOption loginOption)
    {
        try
        {
            Debug.WriteLine(AccountServiceStr.BeginLogin);

            var response = await httpClient.GetAsync(Urls.UIALoginUrl);
            string? responseurl = response.RequestMessage?.RequestUri?.ToString() ?? string.Empty;

            if (responseurl.Contains(Urls.MainPageUrl))
            {
                return new LoginResult
                {
                    Success = true,
                    Message = AccountServiceStr.UIA_ValidSession,
                    StatusCode = response.StatusCode,
                };
            }
            else if (!responseurl.Contains(Urls.UIALoginUrl))
            {
                throw new Exception(AccountServiceStr.PageRedirectedToOtherUrl);
            }

            if (loginOption.LoadCookie)
            {
                Debug.WriteLine(AccountServiceStr.UIA_LoadCookieLoginFailed);
            }

            if (string.IsNullOrEmpty(loginOption.UserName) || string.IsNullOrEmpty(loginOption.Password))
            {
                throw new Exception("Username or password is empty. Login failed.");
            }

            string html = await response.Content.ReadAsStringAsync();

            var content = BuildLoginData(html, loginOption);

            Debug.WriteLine(AccountServiceStr.SendPost);

            response = await httpClient.PostAsync(Urls.UIALoginUrl, content);

            if ((response.RequestMessage?.RequestUri?.ToString().Contains(Urls.MainPageUrl)) ?? false)
            {
                Debug.WriteLine(AccountServiceStr.LoginSuccess);

                var result = new LoginResult
                {
                    Success = true,
                    Message = AccountServiceStr.LoginSuccess,
                    StatusCode = response.StatusCode,
                };

                return result;
            }

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                if (loginOption.UserName.Length == 10)
                {
                    throw new Exception("用户名或密码错误");
                }
                else
                {
                    throw new Exception("用户名或密码错误");
                }
            }

            return new LoginResult
            {
                Success = false,
                Message = "未知原因登录失败",
                StatusCode = response.StatusCode
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex);
            return new LoginResult
            {
                Success = false,
                Message = ex.Message
            };
        }
    }

    private FormUrlEncodedContent BuildLoginData(string html, LoginOption loginOption)
    {
        Debug.WriteLine(AccountServiceStr.UIA_BuildLoginData);
        string execution = ExtractValue(html, "name=\"execution\" value=\"");
        string encryptSalt = ExtractValue(html, "id=\"pwdEncryptSalt\" value=\"");
        if (string.IsNullOrWhiteSpace(execution) || string.IsNullOrWhiteSpace(encryptSalt))
        {
            throw new Exception(AccountServiceStr.UIA_CannotExtractExecutionOrEncryptSalt);
        }

        string encryptedPassword = EncryptPassword(loginOption.Password, encryptSalt);

        // Build login data，username, password, _eventId, execution data is neccessary，
        // But we will keep the same with the original post.
        // preserve insertion order and allow inserting in the middle
        var formList = new List<KeyValuePair<string, string>>
            {
                new("username", loginOption.UserName),
                new("password", encryptedPassword),
                new("captcha", ""),
                new("_eventId", "submit"),
                new("cllt", "userNameLogin"),
                new("dllt", "generalLogin"),
                new("lt", ""),
                new("execution", execution),
            };

        // Insert UIArememberMe at a specific position (e.g. after "dllt")
        if (loginOption.RememberMe)
        {
            formList.Insert(3, new KeyValuePair<string, string>("rememberMe", "true"));
            Debug.WriteLine(AccountServiceStr.UIA_EnableRememberMe);
        }

        return new FormUrlEncodedContent(formList);
    }

    private static string ExtractValue(string html, string pattern)
    {
        try
        {
            int start = html.IndexOf(pattern);
            if (start == -1) return "";
            start += pattern.Length;
            int end = html.IndexOf('"', start);
            return end > start ? html[start..end] : "";
        }
        catch (Exception ex)
        {
            Debug.WriteLine(AccountServiceStr.UIA_ExtractValueFailed, ex.Message);
            return "";
        }
    }

    /// <summary>
    /// 广东工厂大学统一身份认证密码加密的方式。加密的 javascript 代码可以直接从前端获取，此处翻译为 csharp 代码
    /// </summary>
    /// <param name="password"></param>
    /// <param name="salt"></param>
    /// <returns></returns>
    private string EncryptPassword(string password, string salt)
    {
        // 生成 64 位随机字符串前缀，前缀的每一位从 AES_CHARS 中选取
        // 请使用 RandomString() 方法生成有范围的随机比特数组
        string plainText = RandomString(64) + password;

        using var aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = Encoding.UTF8.GetBytes(salt);

        // 生成 16 位随机 iv，iv 的每一位从 AES_CHARS 中选取
        // 请使用 RandomString() 方法生成有范围的随机比特数组
        aes.IV = Encoding.UTF8.GetBytes(RandomString(16));

        using var encryptor = aes.CreateEncryptor();
        byte[] plainBytes = Encoding.UTF8.GetBytes(plainText);
        byte[] encryptBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        string encryptPassword = Convert.ToBase64String(encryptBytes);
        return encryptPassword;
    }

    // 生成随机字符串
    private string RandomString(int length)
    {
        char[] result = new char[length];
        for (int i = 0; i < length; i++)
        {
            result[i] = AES_CHARS[random.Next(AES_CHARS.Length)];
        }
        return new string(result);
    }
}

public class LoginResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public HttpStatusCode StatusCode { get; set; }
    public string ResponseContent { get; set; } = string.Empty;
    public string CookieContent { get; set; } = string.Empty;
}

public class LoginOption
{
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public bool RememberMe { get; set; } = true;
    public bool LoadCookie { get; set; } = true;
    public bool ExportCookie { get; set; } = false;
    public string CookieContent { get; set; } = string.Empty;
}