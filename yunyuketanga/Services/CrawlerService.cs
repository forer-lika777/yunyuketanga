using HtmlAgilityPack;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using yunyuketanga.Constants;
using yunyuketanga.Models;

namespace yunyuketanga.Services;

public class CrawlerService : IDisposable
{
    private readonly HttpClient httpClient;
    private readonly HttpClientHandler httpClientHandler;
    private string sesskey = string.Empty;
    private readonly IDebug Debug;

    public CrawlerService(IDebug debuger)
    {
        Debug = debuger;
        httpClientHandler = new HttpClientHandler()
        {
            AllowAutoRedirect = true,
        };
        httpClient = new HttpClient(httpClientHandler);
        httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
        httpClient.DefaultRequestHeaders.Add("Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8");
        httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
        httpClient.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
        httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "document");
        httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
        httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Site", "none");
        httpClient.DefaultRequestHeaders.Add("Sec-Ch-Ua", "\"Not_A Brand\";v=\"8\", \"Chromium\";v=\"120\", \"Google Chrome\";v=\"120\"");
        httpClient.DefaultRequestHeaders.Add("Sec-Ch-Ua-Mobile", "?0");
        httpClient.DefaultRequestHeaders.Add("Sec-Ch-Ua-Platform", "\"Windows\"");
    }

    public async Task<LoginResult> PerformLoginAsync(string username, string password)
    {
        var uiaLoginView = new UIALoginService(httpClient, httpClientHandler);
        var loginResult = await uiaLoginView.LoginAsync(new LoginOption
        {
            UserName = username,
            LoadCookie = false,
            ExportCookie = false,
            Password = password,
            RememberMe = false
        });
        return loginResult;
    }

    /// <summary>
    /// 从视频页面 HTML 提取 playerdata
    /// </summary>
    private async Task<PlayerData?> GetPlayerDataAsync(int courseId)
    {
        var url = $"{Urls.SiteUrl}/mod/fsresource/view.php?id={courseId}";
        var html = await httpClient.GetStringAsync(url);

        string filepath = "D:\\Common folders\\Development\\data\\subcoursepage.json";
        await File.WriteAllTextAsync(filepath, html, Encoding.UTF8);

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var scriptNode = doc.DocumentNode
            .SelectNodes("//script")
            ?.FirstOrDefault(s => s.InnerHtml.Contains("playerdata"));

        if (scriptNode == null) return null;

        var scriptContent = scriptNode.InnerHtml;

        // 2. 在 script 内容里定位
        var start = scriptContent.IndexOf("var playerdata = ");
        if (start == -1) return null;

        start = scriptContent.IndexOf("{", start);
        if (start == -1) return null;

        // 3. 括号匹配
        int braceCount = 0;
        int end = start;
        for (int i = start; i < scriptContent.Length; i++)
        {
            if (scriptContent[i] == '{') braceCount++;
            else if (scriptContent[i] == '}') braceCount--;

            if (braceCount == 0)
            {
                end = i;
                break;
            }
        }

        var jsonText = scriptContent.Substring(start, end - start + 1);
        jsonText = jsonText.Replace("\\", "\\\\");
        jsonText = jsonText.Replace("\"", "\\\"");
        jsonText = jsonText.Replace("'", "\"");


        filepath = "D:\\Common folders\\Development\\data\\jsontext.json";
        await File.WriteAllTextAsync(filepath, jsonText, Encoding.UTF8);

        try
        {
            var playerData = JsonSerializer.Deserialize<PlayerData>(jsonText);

            // source 字段是嵌套 JSON，需要二次解析
            if (!string.IsNullOrEmpty(playerData?.Source))
            {
                playerData.SourceObj = JsonSerializer.Deserialize<VideoSources>(playerData.Source);
            }

            return playerData;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
            return null;

        }
    }

    private class PlayerData
    {
        [JsonPropertyName("userid")]
        public int Userid { get; set; }

        [JsonPropertyName("source")]
        public string Source { get; set; } = string.Empty;

        [JsonPropertyName("width")]
        public int Width { get; set; }

        [JsonPropertyName("height")]
        public int Height { get; set; }

        [JsonPropertyName("progress")]
        public int Progress { get; set; }

        [JsonPropertyName("fsresourceid")]
        public int Fsresourceid { get; set; }

        [JsonPropertyName("sesskey")]
        public string Sesskey { get; set; } = string.Empty;

        [JsonPropertyName("siteUrl")]
        public string SiteUrl { get; set; } = string.Empty;

        public VideoSources? SourceObj { get; set; }
    }

    private class VideoSources
    {
        [JsonPropertyName("OD")]
        public string OD { get; set; }  // 超清

        [JsonPropertyName("FD")]
        public string FD { get; set; }  // 标清
    }

    public async Task WatchVideoAsync(int courseId, IProgress<string>? progress = null)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        int reportInterval = 15;

        Debug.WriteLine("正在获取 playerdata 数据");
        var playerData = await GetPlayerDataAsync(courseId);
        if (playerData == null)
        {
            Debug.WriteLine("Cannot get player data.");
            return;
        }

        sesskey = playerData.Sesskey;

        string? videoUrl = playerData.SourceObj?.OD ?? playerData.SourceObj?.FD;
        int fsresourceId = playerData.Fsresourceid;

        if (videoUrl == null)
        {
            Debug.WriteLine($"Failed to get videoUrl");
            return;
        }

        Debug.WriteLine("正在获取视频时长");
        int durationSeconds = await GetVideoDurationAsync(videoUrl);
        Debug.WriteLine($"获取到视频时长：{durationSeconds}");

        string unique = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}_{Random.Shared.NextDouble()}";
        var sw = Stopwatch.StartNew();
        int lastReported = 0;

        while (lastReported < durationSeconds)
        {
            // 下次上报的目标时间（秒）
            int target = Math.Min(lastReported + reportInterval, durationSeconds);
            Debug.WriteLine($"下次目标上报时间：{target}");
            double remaining = target - sw.Elapsed.TotalSeconds;

            if (remaining > 0)
            {
                // 加随机抖动（只加延迟，不提前）
                int waitTime = (int)(remaining * 1000);
                Debug.WriteLine($"等待 {waitTime} 毫秒时间");
                await Task.Delay(waitTime);
            }

            // 当前实际经过的秒数
            int now = (int)Math.Min(sw.Elapsed.TotalSeconds, durationSeconds);
            Debug.WriteLine($"总时长：{durationSeconds}，当前实际观看秒数：{now}，剩余观看秒数：{durationSeconds - now}");
            if (now <= lastReported) continue; // 还没到上报点

            int increment = now - lastReported;
            double percent = (double)now / durationSeconds * 100;
            bool finished = now >= durationSeconds;

            var result = await ReportProgressAsync(fsresourceId, increment, finished, percent, unique);
            if (result.Success)
            {
                lastReported = now;
            }
            // 如果失败，保留 lastReported，下次重试
        }

        Debug.WriteLine("观看已完成。由于观看有概率未完成，可能需要手动登录网站检查完成进度，并完成最后一小部分。");

        //var timer = new Stopwatch();

        //timer.Start();

        //while (reportedTime < duration)
        //{
        //    var watchedTime = timer.Elapsed;

        //    var gap = watchedTime.TotalSeconds - reportedTime;

        //    if (gap < 1) gap = 1;

        //    reportedTime += (int)gap;

        //    double percent = (double)reportedTime / duration * 100;
        //    bool finished = reportedTime >= duration;

        //    var result = new ReportResult
        //    {
        //        Success = false
        //    };

        //    timer.Start();

        //    while (!result.Success)
        //    {

        //        Debug.WriteLine($"上报时间：{reportInterval}");
        //        result = await ReportProgressAsync(fsresourceId, reportInterval, finished, percent, unique);

        //        if (result.Success)
        //        {
        //            Debug.WriteLine($"成功上报进度 {percent:F1}% ({reportedTime}/{duration}秒)");
        //            progress?.Report($"进度 {percent:F1}% ({reportedTime}/{duration}秒)");
        //        }
        //        else
        //        {
        //            Debug.WriteLine($"请求失败，原因：{result.Message}。将等待 5 秒后重试。");
        //            await Task.Delay(5000);
        //        }
        //    }

        //    if (gap < reportInterval)
        //    {
        //        await Task.Delay((int)(gap * 1000));
        //    }
        //}
    }

    /// <summary>
    /// 从 m3u8 文件获取视频总时长
    /// </summary>
    private async Task<int> GetVideoDurationAsync(string m3u8Url)
    {
        var m3u8Content = await httpClient.GetStringAsync(m3u8Url);
        var duration = 0.0;

        var lines = m3u8Content.Split('\n');
        foreach (var line in lines)
        {
            if (line.StartsWith("#EXTINF:"))
            {
                var parts = line.Split(',');
                var seconds = double.Parse(parts[0].Replace("#EXTINF:", "").Trim());
                duration += seconds;
            }
        }

        return (int)Math.Ceiling(duration);
    }

    private async Task<ReportResult> ReportProgressAsync(int fsresourceId, int watchSeconds, bool finished, double progress, string uniqueS)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var url = $"{Urls.SiteUrl}/lib/ajax/service.php?sesskey={sesskey}&timestamp={timestamp}";

        var data = new[]
        {
            new
            {
                index = 0,
                methodname = "mod_fsresource_set_time",
                args = new
                {
                    fsresourceid = fsresourceId,
                    time = watchSeconds,
                    finish = finished ? 1 : 0,
                    progress = progress.ToString("F2"),
                    unique = uniqueS,
                }
            }
        };

        var json = JsonSerializer.Serialize(data);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            RequestUri = new Uri(url),
            Content = content,
        };

        request.Headers.Add("Referer", $"{Urls.SiteUrl}/mod/fsresource/view.php?id={fsresourceId}");

        try
        {
            Debug.WriteLine($"对 {request.RequestUri} 发送 Post 请求。\n   内容：{json}");
            var response = await httpClient.SendAsync(request);
            var responseJson = await response.Content.ReadAsStringAsync();

            return new ReportResult
            {
                Success = responseJson.Contains("\"status\":true")
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
            return new ReportResult
            {
                Success = false,
                Message = ex.Message
            };
        }

    }
    // 在 CrawlerService 里
    public async Task<List<Course>> GetAllCoursesAsync()
    {
        var jsonString = await FetchAllCoursesAsync();

        // 解析外层数组
        var responses = JsonSerializer.Deserialize<List<AjaxResponse<CourseListData>>>(jsonString);

        if (responses != null && responses.Count > 0 && !responses[0].Error)
        {
            return responses[0].Data?.Courses ?? new List<Course>();
        }

        // 处理错误
        if (responses != null && responses[0].Exception != null)
        {
            throw new Exception(responses[0].Exception.Message);
        }

        return new List<Course>();
    }

    private async Task RefreshSesskey(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);

        request.Headers.Add("Referer", "https://courses.gdut.edu.cn/");

        var response = httpClient.SendAsync(request);

        var html = await response.Result.Content.ReadAsStringAsync();

        // 匹配 M.cfg = { ... "sesskey":"xxx" ... }
        var match = Regex.Match(html, @"M\.cfg\s*=\s*{[^}]*""sesskey""\s*:\s*""([^""]+)""");

        if (match.Success)
        {
            sesskey = match.Groups[1].Value;
            Debug.WriteLine($"获取到 sesskey: {sesskey}");
            return;
        }

        // 备选：直接搜 sesskey
        match = Regex.Match(html, @"""sesskey""\s*:\s*""([^""]+)""");
        if (match.Success)
        {
            sesskey = match.Groups[1].Value;
            Debug.WriteLine($"获取到 sesskey (备选): {sesskey}");
            return;
        }

        throw new Exception("无法获取 sesskey");
    }

    private async Task<string> FetchAllCoursesAsync()
    {
        await RefreshSesskey(Urls.SiteUrl + "/my");

        if (string.IsNullOrEmpty(sesskey)) throw new Exception("sesskey无效");
        var url = $"https://courses.gdut.edu.cn/lib/ajax/service.php?sesskey={sesskey}&info=core_course_get_enrolled_courses_by_timeline_classification";

        // 构建请求体
        var requestBody = new List<AjaxRequest>
    {
        new()
        {
            Index = 0,
            MethodName = "core_course_get_enrolled_courses_by_timeline_classification",
            Args = new RequestArgs
            {
                Offset = 0,
                Limit = 0,
                Classification = "all",
                Sort = "fullname",
                CustomFieldName = "",
                CustomFieldValue = "",
                RequiredFields = new List<string>
                {
                    "id",
                    "fullname",
                    "shortname",
                    "showcoursecategory",
                    "showshortname",
                    "visible",
                    "enddate"
                }
            }
        }
    };

        var json = JsonSerializer.Serialize(requestBody, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(url, content);
        var responseString = await response.Content.ReadAsStringAsync();

        return responseString;
    }

    public async Task<List<SubCourse>> GetSubCourseIdsAsync(int courseId)
    {
        if (courseId == 0) return [];

        var url = $"{Urls.SiteUrl}/grade/report/user/index.php?id={courseId}";
        var html = await httpClient.GetStringAsync(url);

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var subCourseIds = new List<SubCourse>();

        // 找所有 href 包含 mod/fsresource/view.php 的链接
        var links = doc.DocumentNode.SelectNodes("//a[contains(@href, 'mod/fsresource/view.php')]");

        if (links != null)
        {
            foreach (var link in links)
            {
                var href = link.GetAttributeValue("href", "");
                string name = link.InnerText.Trim();

                var match = Regex.Match(href, @"id=(\d+)");
                if (!match.Success) continue;

                int id = int.Parse(match.Groups[1].Value); 

                var subCourse = new SubCourse
                {
                    CourseId = id,
                    Name = name
                };

                var tableRow = link.ParentNode.ParentNode.ParentNode.ParentNode.ParentNode;

                Debug.WriteLine(tableRow.InnerHtml);

                var weightNode = tableRow.SelectSingleNode(".//td[contains(@class, 'column-weight')]");
                subCourse.Weight = weightNode.InnerText.Trim();

                var scoreNode = tableRow.SelectSingleNode(".//td[contains(@class, 'column-grade')]");
                subCourse.Score = scoreNode.InnerText.Trim();

                var rangeNode = tableRow.SelectSingleNode(".//td[contains(@class, 'column-range')]");
                subCourse.Range = rangeNode.InnerText.Trim();

                var percentageNode = tableRow.SelectSingleNode(".//td[contains(@class, 'column-percentage')]");
                subCourse.Percentage = percentageNode.InnerText.Trim();

                var contributionPercentageNode = tableRow.SelectSingleNode(".//td[contains(@class, 'column-contributiontocoursetotal')]");
                subCourse.ContributionPercentage = contributionPercentageNode.InnerText.Trim();

                if (!subCourseIds.Exists(s => s.CourseId == id))
                {
                    subCourseIds.Add(subCourse);
                }
            }
        }

        // 将页面 HTML 保存到文件（确保目录存在），修正了 MemoryStream/WriteAsync 的错误用法
        var filePath = @"D:\Common folders\Development\data\courses.json";
        try
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            await File.WriteAllTextAsync(filePath, html, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"保存 courses.json 失败: {ex.Message}");
        }

        return subCourseIds;
    }

    public void Dispose()
    {
        httpClient.Dispose();
        httpClientHandler.Dispose();
    }
}

public class ReportResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}
