using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using yunyuketanga.Models;
using yunyuketanga.Services;
using yunyuketanga.ViewModels;

namespace yunyuketanga;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }

    //private async void LoginButton_Click(object sender, RoutedEventArgs e)
    //{
    //    LoginButton.IsEnabled = false;

    //    if (string.IsNullOrWhiteSpace(UsernameInput.Text) || string.IsNullOrWhiteSpace(PasswordInput.Text))
    //    {
    //        StatusLabel.Content = "请输入合法用户名或密码";
    //        return;
    //    }

    //    StatusLabel.Content = "正在发送登录请求";
    //    var loginResult = await crawlerService.PerformLoginAsync(UsernameInput.Text, PasswordInput.Text);

    //    if (loginResult.Success)
    //    {
    //        StatusLabel.Content = "正在获取所有课程列表";
    //        var courseList = await crawlerService.GetAllCoursesAsync();
    //        if (courseList.Count == 0)
    //        {
    //            StatusLabel.Content = "获取课程列表失败！";
    //            return;
    //        }
    //        CourseList.Visibility = Visibility.Visible;
    //        CourseList.ItemsSource = courseList;
    //    }
    //    else
    //    {
    //        StatusLabel.Content = $"登录失败！原因：{loginResult.Message}";
    //        LoginButton.IsEnabled = false;
    //        Debug.WriteLine(loginResult.Message);
    //    }
    //}

    //private async void ShowSubCoursesButton_Click(object sender, RoutedEventArgs e)
    //{
    //    var button = sender as Button;

    //    if (button?.Tag is not Course course) return;

    //    var subCourseList = await InsertSubViewBelowRow(course);
    //    SubCoursesPanel.Visibility = Visibility.Visible;
    //    SubCoursesList.ItemsSource = subCourseList;
    //}

    //private async Task<List<SubCourse>> InsertSubViewBelowRow(Course course)
    //{
    //    return await crawlerService.GetSubCourseIdsAsync(course.Id);
    //}

    //private async void WatchSubCoursesButton_Click(object sender, RoutedEventArgs e)
    //{
    //    var button = sender as Button;
    //    if (button?.Tag is not SubCourse subCourse) return;

    //    await crawlerService.WatchVideoAsync(subCourse.CourseId);
    //}

    //private void CloseSubCoursesButton_Click(object sender, RoutedEventArgs e)
    //{
    //    SubCoursesPanel.Visibility = Visibility.Hidden;
    //}
}