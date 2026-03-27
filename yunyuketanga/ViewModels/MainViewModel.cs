using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Diagnostics;
using yunyuketanga.Models;
using yunyuketanga.Services;
using System.Collections.ObjectModel;

namespace yunyuketanga.ViewModels;

public partial class MainViewModel : ObservableObject, IDebug
{
    private readonly CrawlerService crawlerService;

    public void WriteLine(string message)
    {
        Debug.WriteLine(message);
        DebugInfo += "\n" + message;
    }

    public MainViewModel()
    {
        crawlerService = new CrawlerService(this);
    }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool LoginSuccess { get; set; }

    [ObservableProperty]
    public partial bool SubCoursesVisible { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PerformLoginCommand))]
    public partial string Username { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PerformLoginCommand))]
    public partial string Password { get; set; }

    bool LoginInfoValid() => !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password);


    [ObservableProperty]
    public partial string StatusInfo { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DebugInfo { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ObservableCollection<Course> CourseCollection { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<SubCourse> SubCourseCollection { get; set; }

    private int CachedSubCourseId = -1;

    [RelayCommand(CanExecute = nameof(LoginInfoValid))]
    async Task PerformLogin()
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            StatusInfo = "请输入合法用户名或密码";
            return;
        }

        StatusInfo = "正在发送登录请求";
        IsBusy = true;

        var loginResult = await crawlerService.PerformLoginAsync(Username, Password);
        
        if (loginResult.Success)
        {
            StatusInfo = "登录成功。正在获取所有课程列表";
            var courseList = await crawlerService.GetAllCoursesAsync();
            if (courseList.Count == 0)
            {
                StatusInfo = "获取课程列表失败！";
                IsBusy = false;
                return;
            }
            StatusInfo = "获取课程列表成功";
            LoginSuccess = true;

            var CourseCollectionCopy = new ObservableCollection<Course>();

            foreach (var course in courseList)
            {
                CourseCollectionCopy.Add(course);
            }

            CourseCollection = CourseCollectionCopy;
        }
        else
        {
            StatusInfo = $"登录失败！原因：{loginResult.Message}";
            Debug.WriteLine(loginResult.Message);
        }
    }

    [RelayCommand]
    async Task FetchAndShowSubCourses(int id)
    {
        SubCoursesVisible = true;

        if (CachedSubCourseId == id) return;

        CachedSubCourseId = id;

        var subCourses = await crawlerService.GetSubCourseIdsAsync(id);
        if (subCourses.Count == 0)
        {
            StatusInfo = "获取子课程列表失败！";
            return;
        }

        var subCoursesCollectionCopy = new ObservableCollection<SubCourse>();

        foreach (var subCourse in subCourses)
        {
            subCoursesCollectionCopy.Add(subCourse);
        }

        SubCourseCollection = subCoursesCollectionCopy;
    }

    [RelayCommand]
    void CloseSubCourses()
    {
        SubCoursesVisible = false;
    }

    [RelayCommand]
    async Task WatchVideo(int id)
    {
        await crawlerService.WatchVideoAsync(id);
    }
}
