﻿using Data;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Display.Control
{
    public sealed partial class VideoCoverDisplay : UserControl
    {
        ApplicationDataContainer localSettings = ApplicationData.Current.LocalSettings;
        ObservableCollection<AccountContentInPage> AccountInPage = new ObservableCollection<AccountContentInPage>();
        ApplicationDataCompositeValue composite = (ApplicationDataCompositeValue)ApplicationData.Current.LocalSettings.Values["DisplaySettings"];
        //public ObservableCollection<VideoCoverDisplayClass> FileGrid { get; set; }
        private ObservableCollection<VideoCoverDisplayClass> _FileGrid;
        public ObservableCollection<VideoCoverDisplayClass> FileGrid
        {
            get
            {
                return _FileGrid;
            }
            set
            {
                _FileGrid = value;
                totalPageCount = (int)Math.Ceiling((double)FileGrid.Count / showCountInPage);
                tryDisplayInfo(0);
            }
        }

        //private ObservableCollection<VideoCoverDisplayClass> LastFileGrid;
        public ObservableCollection<VideoCoverDisplayClass> FileGrid_part = new();
        //item开始Index
        private int startValue = 0;
        //item开始Index
        private int nowPage = 1;
        //每页显示的最大数量
        private int _showCountInPage = 30;
        private int showCountInPage
        {
            get
            {
                int count = 0;
                if (composite == null)
                {
                    composite = new ApplicationDataCompositeValue();
                }
                else
                {
                    count = Convert.ToInt32(composite["CountPerPage"]);
                }

                if (count == 0)
                {
                    count = _showCountInPage;
                }

                return count;
            }
            set
            {
                composite["CountPerPage"] = value;
            }
        }
        //总页数
        private int totalPageCount = 1;

        public VideoCoverDisplay()
        {
            this.InitializeComponent();

            loadData();
            //loadSqlData();
            //Loaded += Control_loaded;
        }

        private void loadData()
        {

            if (AccountInPage.Count == 0)
            {
                for (var i = 10; i < 100; i += 20)
                {
                    AccountInPage.Add(new AccountContentInPage()
                    {
                        ContentAcount = i,
                    });
                }
            }

            //显示UI
            ContentAcountListView.ItemsSource = AccountInPage;
            BasicGridView.ItemsSource = FileGrid_part;

            //加载控件默认设置，每页最大显示数量，图标显示大小s
            LoadDefaultSettings();
        }



        ///// <summary>
        ///// 用户控件加载时
        ///// </summary>
        ///// <param name="sender"></param>
        ///// <param name="e"></param>
        //private void Control_loaded(object sender, RoutedEventArgs e)
        //{
        //    if (AccountInPage.Count == 0)
        //    {
        //        for (var i = 10; i < 100; i += 20)
        //        {
        //            AccountInPage.Add(new AccountContentInPage()
        //            {
        //                ContentAcount = i,
        //            });
        //        }
        //    }

        //    //显示UI
        //    ContentAcountListView.ItemsSource = AccountInPage;
        //    BasicGridView.ItemsSource = FileGrid_part;

        //    //加载控件默认设置
        //    LoadDefaultSettings();

        //    totalPageCount = (int)Math.Ceiling((double)FileGrid.Count / showCountInPage);
        //}

        /// <summary>
        /// 加载（更新）文件信息，根据startValue[0:FileGrid.Count]
        /// </summary>
        private void tryDisplayInfo(int newStartValue)
        {
            var newEndIndex = newStartValue + showCountInPage - 1;

            //举例：
            //    FileGrid.Count = 50
            //    Value           0-49

            if (newEndIndex >= FileGrid.Count)
            {
                newEndIndex = FileGrid.Count - 1;
            }
            else if (newEndIndex < FileGrid.Count && newStartValue >= 0)
            {
            }
            else
            {
                return;
            }
            //当前页数
            int newNowPage = newStartValue / showCountInPage + 1;

            double maxPageCount = Math.Ceiling((double)FileGrid.Count / showCountInPage);
            //当前页数必须小于或等于最大页数，且大于0
            if (newNowPage <= maxPageCount && newNowPage > 0)
            {
                nowPage = newNowPage;
            }
            else
            {
                return;
            }

            //是否需要更新页数显示
            if (Convert.ToInt32(nowPageTextBox.Text) != newNowPage)
            {
                nowPageTextBox.Text = nowPage.ToString();
            }

            //更新startValue
            startValue = newStartValue;

            // 删除原有显示
            if (FileGrid_part != null)
            {
                FileGrid_part.Clear();
            }

            for (int i = newStartValue; i <= newEndIndex; i++)
            {
                var item = FileGrid[i];
                FileGrid_part.Add(item);
            }

            InfoBar.Message = $"总数量：{FileGrid.Count} 总页数：{totalPageCount}，当前显示 {newStartValue + 1} 到 {newEndIndex + 1} 内容";
        }

        /// <summary>
        /// 加载应用设置
        /// </summary>
        private void LoadDefaultSettings()
        {
            // 图片大小，每页显示的最大数量
            int ImageSize = 350;
            int CountPerPage = 10;

            // 加载应用设置，有则使用，没有则添加
            if (composite != null)
            {
                ImageSize = composite.ContainsKey("ImageSize") ? (int)composite["ImageSize"] : ImageSize;
                CountPerPage = composite.ContainsKey("CountPerPage") ? (int)composite["CountPerPage"] : CountPerPage;
            }
            else
            {
                composite = new ApplicationDataCompositeValue();
                composite["ImageSize"] = ImageSize;
                composite["CountPerPage"] = CountPerPage;
                localSettings.Values["DisplaySettings"] = composite;
            }

            ImageSizeChangeSlider.Value = ImageSize;

            var item = AccountInPage.Where(i => i.ContentAcount == CountPerPage);

            // 无该最大显示数量
            if (!item.Any())
            {
                tryAddNewAcountInPage(CountPerPage);
                item = AccountInPage.Where(i => i.ContentAcount == CountPerPage);
            }

            ContentAcountListView.SelectedIndex = AccountInPage.IndexOf(item.First());

            ContentAcountListView.SelectionChanged += ContentAcountListView_SelectionChanged;
        }

        /// <summary>
        /// 添加一页显示数量
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void AddAccountPage_NumberBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            int newValue = (int)args.NewValue;
            tryAddNewAcountInPage(newValue);
        }

        private void tryAddNewAcountInPage(int newValue)
        {
            if (newValue != double.NaN && newValue > 0)
            {
                AccountContentInPage newAccount = new AccountContentInPage()
                {
                    ContentAcount = newValue
                };

                //排除重复项
                var item = AccountInPage.Where(i => i.ContentAcount == newValue);
                if (item.Count() == 0)
                {
                    AccountInPage.Add(newAccount);
                }

                // 排序后选择项可能会丢失，记录排序项，然后排序后重新添加
                var selectIndex = ContentAcountListView.SelectedIndex;

                // 排序
                var sortedItemsList = AccountInPage.OrderBy(i => i.ContentAcount).ToList();
                foreach (var sortedItem in sortedItemsList)
                {
                    var moveIndex = AccountInPage.IndexOf(sortedItem);
                    var movedIndex = sortedItemsList.IndexOf(sortedItem);

                    AccountInPage.Move(moveIndex, movedIndex);
                }

                if (ContentAcountListView.SelectedIndex == -1)
                {
                    // 重新选择，若影响，选择项+1（因为添加项为1）
                    ContentAcountListView.SelectedIndex = selectIndex + 1;
                }
            }
        }

        /// <summary>
        /// Slider值改变后，调整图片大小
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Slider_valueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            //ObservableCollection<VideoCoverDisplayClass> tmpGrid = new ObservableCollection<VideoCoverDisplayClass>();
            if (FileGrid == null)
            {
                return;
            }

            for (int i = 0; i < FileGrid.Count; i++)
            {
                //FileGrid.Add(FileGrid[i]);
                FileGrid[i].imagewidth = (int)e.NewValue;
                FileGrid[i].imageheight = (int)e.NewValue / 3 * 2;
            }

            //刷新应用设置
            //var composite = (ApplicationDataCompositeValue)localSettings.Values["DisplaySettings"];
            composite["ImageSize"] = (int)e.NewValue;
            localSettings.Values["DisplaySettings"] = composite;

        }

        ////点击选项
        //public event ItemClickEventHandler Click;
        private void BasicGridView_ItemClick(object sender, ItemClickEventArgs e)
        {
            //Click?.Invoke(sender, e);
        }

        public event RoutedEventHandler Click;
        private void MoreButton_Click(object sender, RoutedEventArgs e)
        {
            Click?.Invoke(sender, e);
        }


        /// <summary>
        /// 删除当前页的最大显示数量
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var item = (sender as Button).DataContext as AccountContentInPage;

            //删除项为选中项
            if (item == ContentAcountListView.SelectedItem)
            {
                //数量两个以上
                if (AccountInPage.Count() > 1)
                {
                    //存在下一项
                    if (ContentAcountListView.SelectedIndex + 2 <= AccountInPage.Count)
                    {
                        ContentAcountListView.SelectedIndex++;
                    }
                    else
                    {
                        ContentAcountListView.SelectedIndex--;
                    }
                }

            }
            AccountInPage.Remove(item);

            //数量为零，添加一项
            if (AccountInPage.Count() == 0)
            {
                AccountInPage.Add(new AccountContentInPage()
                {
                    ContentAcount = 50,
                });
                ContentAcountListView.SelectedIndex = 0;

            }
        }

        
        private void ContentAcountListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ContentAcountListView.SelectedItem == null) return;
            if (FileGrid == null || FileGrid.Count == 0) return;

            // 更新应用设置
            var pageCountItem = ContentAcountListView.SelectedItem as AccountContentInPage;

            //每页显示的数量
            showCountInPage = pageCountItem.ContentAcount;

            ////总页数
            //totalPageCount = (int)Math.Ceiling((double)FileGrid.Count / showCountInPage);

            //var composite = (ApplicationDataCompositeValue)localSettings.Values["DisplaySettings"];
            //composite["CountPerPage"] = showCountInPage;
            localSettings.Values["DisplaySettings"] = composite;

            // 重新显示（按照新的显示数量）
            tryDisplayInfo(startValue);

        }


        /// <summary>
        /// 显示上一页内容
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PreviousPageButton(object sender, RoutedEventArgs e)
        {
            if (startValue - showCountInPage < 0)
            {
                LightDismissTeachingTip.Content = "最小了，不能再减了";
                LightDismissTeachingTip.Target = sender as Button;
                LightDismissTeachingTip.IsOpen = true;
                return;
            }

            tryDisplayInfo(startValue - showCountInPage);
        }

        /// <summary>
        /// 显示下一页内容
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void NextPageButton(object sender, RoutedEventArgs e)
        {
            if(startValue + showCountInPage >= totalPageCount * showCountInPage)
            {
                LightDismissTeachingTip.Content = "最大了，不能再加了";
                LightDismissTeachingTip.Target = sender as Button;
                LightDismissTeachingTip.IsOpen = true;
                return;
            }

            tryDisplayInfo(startValue + showCountInPage);
        }

        /// <summary>
        /// 鼠标悬停在Grid，显示可操作按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Grid_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            var item = sender as Grid;
            item.Children[1].Visibility = Visibility.Visible;
        }


        private void Grid_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            var item = sender as Grid;
            var CollapsedGrid = item.Children[1] as Grid;
            var CommandBarControl = CollapsedGrid.Children.Where(x => x is CommandBar).FirstOrDefault() as CommandBar;
            if (CommandBarControl.IsOpen == false)
            {
                CollapsedGrid.Visibility = Visibility.Collapsed;
            }
        }

        private void button_OnPointerEntered(object sender, PointerRoutedEventArgs e)
        {
            ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Hand);
        }


        private void Button_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.Arrow);
        }


        /// <summary>
        /// 点击了喜欢按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LikeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            AppBarToggleButton isLikeButton = (AppBarToggleButton)sender;

            var videoInfo = (VideoCoverDisplayClass)isLikeButton.DataContext;

            videoInfo.is_like = (bool)isLikeButton.IsChecked ? 1 : 0;

            DataAccess.UpdateSingleDataFromVideoInfo(videoInfo.truename, "is_like", videoInfo.is_like.ToString());
        }

        /// <summary>
        /// 点击了稍后观看
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LookLaterToggleButton_Click(object sender, RoutedEventArgs e)
        {
            AppBarToggleButton isLikeButton = (AppBarToggleButton)sender;

            var videoInfo = (VideoCoverDisplayClass)isLikeButton.DataContext;

            videoInfo.look_later = (bool)isLikeButton.IsChecked ? DateTimeOffset.Now.ToUnixTimeSeconds() : 0;

            DataAccess.UpdateSingleDataFromVideoInfo(videoInfo.truename, "look_later", videoInfo.look_later.ToString());
        }

        /// <summary>
        /// 修改评分
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void RatingControl_ValueChanged(RatingControl sender, object args)
        {
            var videoInfo = sender.DataContext as VideoCoverDisplayClass;

            string score_str = videoInfo.score == 0 ? "-1" : sender.Value.ToString();

            DataAccess.UpdateSingleDataFromVideoInfo(videoInfo.truename, "score", score_str);

        }

        /// <summary>
        /// 点击播放键
        /// </summary>
        public event RoutedEventHandler VideoPlayClick;
        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            VideoPlayClick?.Invoke(sender, e);
        }

        /// <summary>
        /// 手动输入当前页数
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void nowPageTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            //如果不是回车
            if (e.Key != Windows.System.VirtualKey.Enter)
            {
                return;
            }

            //检查页数合法性
            string pageText = nowPageTextBox.Text;

            int n;
            bool is_num = Int32.TryParse(pageText, out n);

            //非数字
            if (!is_num)
            {
                HintTeachingTip.Title = "提示";
                HintTeachingTip.Subtitle = "输入有误，只允许数字，请重新输入";
                HintTeachingTip.Target = nowPageTextBox;
                HintTeachingTip.IconSource = new FontIconSource() { FontFamily = new FontFamily("Segoe Fluent Icons"), Glyph = "\xE946" };
                HintTeachingTip.IsOpen = true;

                //回复原值
                nowPageTextBox.Text = nowPage.ToString();
                e.Handled = true;
            }else if(n > totalPageCount)
            {
                nowPageTextBox.Text = totalPageCount.ToString();
                int newStartValue = (totalPageCount - 1) * showCountInPage;
                // 重新显示（按照新的显示数量）
                tryDisplayInfo(newStartValue);
            }else if(n <= 0)
            {
                nowPageTextBox.Text = "1";
                // 重新显示（按照新的显示数量）
                tryDisplayInfo(0);
            }
            else
            {
                int newStartValue = (n - 1) * showCountInPage;
                // 重新显示（按照新的显示数量）
                tryDisplayInfo(newStartValue);
            }

        }

        /// <summary>
        /// 选择当前页的最大显示数量
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void orderListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            ListView selectListView = (ListView)sender;
            var clickStackPanel = (e.ClickedItem as StackPanel);
            var selectTextBlock = clickStackPanel.Children.Where(x => x is TextBlock).First() as TextBlock;
            string selectOrderText = selectTextBlock.Text;

            FontIcon lastFontIcon = clickStackPanel.Children.Where(x => x is FontIcon).Last() as FontIcon;

            string upGlyph = "\xE014";
            string downGlyph = "\xE015";
            string newGlyph = "\xE174";

            //原图标
            bool isUpSort = lastFontIcon.Glyph == upGlyph;

            //更新降序或升序图标
            //注意：随机 无需升/降序
            if (selectListView.SelectedItem == e.ClickedItem && selectOrderText != "随机")
            {
                lastFontIcon.Glyph = isUpSort ? downGlyph : upGlyph;
            }

            //现图标
            isUpSort = lastFontIcon.Glyph == upGlyph;

            switch (selectOrderText)
            {
                case "名称":
                    newGlyph = "\xE185";
                    FileGrid = isUpSort ? new ObservableCollection<VideoCoverDisplayClass>(FileGrid.OrderBy(item => item.truename)) : new ObservableCollection<VideoCoverDisplayClass>(FileGrid.OrderByDescending(item => item.truename));
                    tryDisplayInfo(0);
                    break;
                case "演员":
                    newGlyph = "\xEC92";
                    FileGrid = isUpSort ? new ObservableCollection<VideoCoverDisplayClass>(FileGrid.OrderBy(item => item.actor)) : new ObservableCollection<VideoCoverDisplayClass>(FileGrid.OrderByDescending(item => item.actor));
                    tryDisplayInfo(0);
                    break;
                case "年份":
                    newGlyph = "\xEB05";
                    FileGrid = isUpSort ? new ObservableCollection<VideoCoverDisplayClass>(FileGrid.OrderBy(item => item.realeaseYear)) : new ObservableCollection<VideoCoverDisplayClass>(FileGrid.OrderByDescending(item => item.realeaseYear));
                    tryDisplayInfo(0);
                    break;
                case "随机":
                    newGlyph = "\xF463";
                    Random rnd = new Random();
                    FileGrid = new ObservableCollection<VideoCoverDisplayClass>(FileGrid.OrderByDescending(item => rnd.Next()));
                    tryDisplayInfo(0);
                    break;
                    //default:

                    //    break;
            }

            //更新首图标
            FontIcon orderFontIcon = orderButton.Content as FontIcon;
            if (orderFontIcon.Glyph != newGlyph)
            {
                orderButton.Content = new FontIcon() { FontFamily = new FontFamily("Segoe Fluent Icons"), Glyph = newGlyph };
            }

        }

        /// <summary>
        /// 点击了删除按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void deleteAppBarButton_Click(object sender, RoutedEventArgs e)
        {

            ContentDialog dialog = new ContentDialog()
            {
                XamlRoot = this.XamlRoot,
                Title = "确认",
                IsPrimaryButtonEnabled = false,
                PrimaryButtonText = "删除",
                CloseButtonText = "返回",
                DefaultButton = ContentDialogButton.Close,
                Content = "该操作只删除本地数据库数据，不对115服务器进行操作，确认删除？"
            };

            //TODO
            var result = await dialog.ShowAsync();

        }

        //开始动画
        public async void StartAnimation(ConnectedAnimation animation,VideoCoverDisplayClass item)
        {
            //开始动画
            await BasicGridView.TryStartConnectedAnimationAsync(animation, item, "showImage");

        }

        public void PrepareAnimation(VideoCoverDisplayClass item)
        {
            BasicGridView.PrepareConnectedAnimation("ForwardConnectedAnimation", item, "showImage");
        }


    }
}
