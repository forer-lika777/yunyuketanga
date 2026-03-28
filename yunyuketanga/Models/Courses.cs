using System.Text.Json.Serialization;

namespace yunyuketanga.Models;

public class AjaxResponse<T>
{
    [JsonPropertyName("error")]
    public bool Error { get; set; }

    [JsonPropertyName("data")]
    public T? Data { get; set; }

    [JsonPropertyName("exception")]
    public ExceptionInfo? Exception { get; set; }
}

public class ExceptionInfo
{
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("errorcode")]
    public string ErrorCode { get; set; } = string.Empty;
}

public class CourseListData
{
    [JsonPropertyName("courses")]
    public List<Course> Courses { get; set; } = new();
}

public class Course
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("fullname")]
    public string FullName { get; set; } = string.Empty;

    [JsonPropertyName("shortname")]
    public string ShortName { get; set; } = string.Empty;

    [JsonPropertyName("coursecategory")]
    public string CourseCategory { get; set; } = string.Empty;

    [JsonPropertyName("viewurl")]
    public string ViewUrl { get; set; } = string.Empty;

    [JsonPropertyName("progress")]
    public int Progress { get; set; }

    [JsonPropertyName("visible")]
    public bool Visible { get; set; }

    [JsonPropertyName("startdate")]
    public long StartDate { get; set; }

    [JsonPropertyName("enddate")]
    public long EndDate { get; set; }

    // 辅助属性
    public DateTime StartDateTime => DateTimeOffset.FromUnixTimeSeconds(StartDate).LocalDateTime;
    public DateTime EndDateTime => EndDate > 0 ? DateTimeOffset.FromUnixTimeSeconds(EndDate).LocalDateTime : DateTime.MaxValue;
}

public class SubCourse
{
    public string Name { get; set; } = string.Empty;

    public int CourseId { get; set; }

    public string Weight { get; set; } = string.Empty;

    public string Score { get; set; } = string.Empty;

    public string Range { get; set; } = string.Empty;

    public string Percentage { get; set; } = string.Empty;

    public string ContributionPercentage { get; set; } = string.Empty;
}

//{
//    "id": 213,
//    "fullname": "中国近现代史纲要",
//    "shortname": "中国近现代史纲要",
//    "idnumber": "",
//    "summary": "",
//    "summaryformat": 0,
//    "startdate": 1573574400,
//    "enddate": 0,
//    "visible": true,
//    "showactivitydates": false,
//    "showcompletionconditions": true,
//    "pdfexportfont": "",
//    "fullnamedisplay": "中国近现代史纲要",
//    "viewurl": "https://courses.gdut.edu.cn/course/view.php?id=213",
//    "courseimage": "https://courses.gdut.edu.cn/pluginfile.php/39448/course/overviewfiles/%E5%BE%AE%E4%BF%A1%E5%9B%BE%E7%89%87_20230422170658.jpg",
//    "progress": 2,
//    "hasprogress": true,
//    "isfavourite": false,
//    "hidden": false,
//    "showshortname": false,
//    "coursecategory": "马克思主义学院"
//}