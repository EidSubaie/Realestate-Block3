using System;
using System.Collections.Generic;

using Xamarin.Forms;

namespace ManageGo
{
    public partial class MGWebViewPage : ContentPage
    {
        public MGWebViewPage()
        {
            InitializeComponent();
            NavigationPage.SetHasNavigationBar(this, false);
        }

        void Handle_Navigating(object sender, WebNavigatingEventArgs e)
        {
            MyLoader.IsVisible = true;
            MyLoader.IsRunning = true;
        }

        void Handle_Navigated(object sender, WebNavigatedEventArgs e)
        {
            MyLoader.IsVisible = false;
            MyLoader.IsRunning = false;
        }
    }
}
