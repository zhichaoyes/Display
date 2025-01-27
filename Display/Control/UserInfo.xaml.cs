﻿using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Display.Control
{

    public sealed partial class UserInfo : UserControl
    {

        //status需要实时更新
        private static readonly DependencyProperty statusProperty =
            DependencyProperty.Register("status", typeof(string), typeof(UserInfo), null);

        public string status
        {
            get
            {
                string statusValue = (string)GetValue(statusProperty);

                if (string.IsNullOrEmpty(statusValue))
                {
                    statusValue = "Update";
                }
                return statusValue;
            }
            set {

                SetValue(statusProperty, value);
            }
        }

        //userinfo需要实时更新
        private static readonly DependencyProperty userinfoProperty =
            DependencyProperty.Register("userinfo", typeof(UserData), typeof(UserInfo), null);

        public UserData userinfo
        {
            get { return (UserData)GetValue(userinfoProperty); }
            set { SetValue(userinfoProperty, value); }
        }


        public UserInfo()
        {
            this.InitializeComponent();
        }

        private Visibility isShowVip(string status)
        {
            return status == "Login" && UserName_TextBlock.Text == "" ? Visibility.Collapsed: Visibility.Visible;
        }

        private Visibility isShowUserInfo(string status)
        {
            return status != "Login" ? Visibility.Collapsed : Visibility.Visible;
        }

        private Visibility isShowOtherInfo(string status)
        {
            //return Visibility.Visible;
            return status != "Login" ? Visibility.Visible : Visibility.Collapsed;
        }

        private Visibility isProcess(string status)
        {
            return status == "NoLogin" ? Visibility.Visible : Visibility.Collapsed;
        }

        private Visibility isUpdate(string status)
        {
            return status == "Update" ? Visibility.Visible : Visibility.Collapsed;
        }

        public event RoutedEventHandler UpdateClick;
        private void UpdataButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateClick?.Invoke(sender, e);
        }


        public event RoutedEventHandler Loginlick;
        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            Loginlick?.Invoke(sender, e);
        }


        public event RoutedEventHandler LogoutClick;
        private void Logout_Click(object sender, RoutedEventArgs e)
        {
            LogoutClick?.Invoke(sender, e);
        }
    }
}
