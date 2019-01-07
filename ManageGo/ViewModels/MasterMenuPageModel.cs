﻿using System;
using FreshMvvm;
using Xamarin.Forms;

namespace ManageGo
{
    public class MasterMenuPageModel : FreshBasePageModel
    {
        public bool HamburgerIsVisible { get; set; }
        internal event EventHandler<bool> OnLogout;
        public MasterMenuPageModel()
        {
            HamburgerIsVisible = true;
        }

        public FreshAwaitCommand OnSupportEmailTapped
        {
            get
            {
                return new FreshAwaitCommand((tcs) =>
                {
                    var address = "support@managego.com";
                    Device.OpenUri(new Uri($"mailto:{address}"));
                    tcs?.SetResult(true);
                });
            }
        }

        public FreshAwaitCommand OnSupportPhoneTapped
        {
            get
            {
                return new FreshAwaitCommand((tcs) =>
                {
                    var phone = "2123007950";
                    Device.OpenUri(new Uri($"tel:{phone}"));
                    tcs?.SetResult(true);
                });
            }
        }


        public FreshAwaitCommand OnMasterMenuTapped
        {
            get
            {
                return new FreshAwaitCommand((tcs) =>
                {
                    App.MenuIsPresented = false;
                    tcs?.SetResult(true);
                });
            }
        }

        public FreshAwaitCommand OnNotificationsTapped
        {
            get
            {
                return new FreshAwaitCommand((tcs) =>
                {
                    if (!App.MasterDetailNav.Pages.ContainsKey("Notifications"))
                        App.MasterDetailNav.AddPage<NotificationsPageModel>("Notifications");
                    App.MasterDetailNav.SwitchSelectedRootPageModel<NotificationsPageModel>();
                    tcs?.SetResult(true);
                });

            }
        }

        public FreshAwaitCommand OnMaintenanceTapped
        {
            get
            {
                async void execute(System.Threading.Tasks.TaskCompletionSource<bool> tcs)
                {
                    if (App.MasterDetailNav.Detail is NavigationPage
                    && ((NavigationPage)App.MasterDetailNav.Detail).CurrentPage.GetModel() is MaintenanceTicketsPageModel model)
                    {
                        App.MasterDetailNav.IsPresented = false;
                        model.NumberOfAppliedFilters = " ";
                        model.DateRange = null;
                        model.FiltersDictionary = null;
                        await model.LoadData(true, true);
                    }
                    else
                        await App.MasterDetailNav.SwitchSelectedRootPageModel<MaintenanceTicketsPageModel>();
                    tcs?.SetResult(true);
                }
                return new FreshAwaitCommand(execute);
            }
        }

        public FreshAwaitCommand OnHomeTapped
        {
            get
            {
                return new FreshAwaitCommand((tcs) =>
                {
                    App.MasterDetailNav.SwitchSelectedRootPageModel<WelcomePageModel>();
                    tcs?.SetResult(true);
                });
            }
        }



        public FreshAwaitCommand OnFeedbackTapped
        {
            get
            {
                return new FreshAwaitCommand((tcs) =>
                {
                    if (!App.MasterDetailNav.Pages.ContainsKey("Feedback"))
                        App.MasterDetailNav.AddPage<FeedbackPageModel>("Feedback");
                    App.MasterDetailNav.SwitchSelectedRootPageModel<FeedbackPageModel>();
                    tcs?.SetResult(true);
                });
            }
        }

        public FreshAwaitCommand OnSettingsTapped
        {
            get
            {
                return new FreshAwaitCommand((tcs) =>
                {
                    if (!App.MasterDetailNav.Pages.ContainsKey("Settings"))
                        App.MasterDetailNav.AddPage<SettingsPageModel>("Settings");
                    App.MasterDetailNav.SwitchSelectedRootPageModel<SettingsPageModel>();
                    tcs?.SetResult(true);
                });
            }
        }

        public FreshAwaitCommand OnCalendarPageTapped
        {
            get
            {
                return new FreshAwaitCommand((tcs) =>
                {
                    if (!App.MasterDetailNav.Pages.ContainsKey("OnCalendarPage"))
                        App.MasterDetailNav.AddPage<CalendarPageModel>("OnCalendarPage");
                    App.MasterDetailNav.SwitchSelectedRootPageModel<CalendarPageModel>();
                    tcs?.SetResult(true);
                });
            }
        }

        public FreshAwaitCommand OnTenantsTapped
        {
            get
            {
                return new FreshAwaitCommand((tcs) =>
                {
                    if (!App.MasterDetailNav.Pages.ContainsKey("Tenants"))
                        App.MasterDetailNav.AddPage<TenantsPageModel>("Tenants");
                    App.MasterDetailNav.SwitchSelectedRootPageModel<TenantsPageModel>();
                    tcs?.SetResult(true);
                });
            }
        }

        public FreshAwaitCommand PaymentsTapped
        {
            get
            {
                return new FreshAwaitCommand((tcs) =>
                {
                    if (!App.MasterDetailNav.Pages.ContainsKey("Payments"))
                        App.MasterDetailNav.AddPage<PaymentsPageModel>("Payments");
                    App.MasterDetailNav.SwitchSelectedRootPageModel<PaymentsPageModel>();
                    tcs?.SetResult(true);
                });
            }
        }

        public FreshAwaitCommand OnTransactionsTapped
        {
            get
            {
                return new FreshAwaitCommand((tcs) =>
                {
                    if (!App.MasterDetailNav.Pages.ContainsKey("Transactions"))
                        App.MasterDetailNav.AddPage<TransactionsPageModel>("Transactions");
                    App.MasterDetailNav.SwitchSelectedRootPageModel<TransactionsPageModel>();
                    tcs?.SetResult(true);
                });
            }
        }

        public FreshAwaitCommand OnBuildingsTapped
        {
            get
            {
                return new FreshAwaitCommand((tcs) =>
                {
                    if (!App.MasterDetailNav.Pages.ContainsKey("Buildings"))
                        App.MasterDetailNav.AddPage<BuildingsListPageModel>("Buildings");
                    App.MasterDetailNav.SwitchSelectedRootPageModel<BuildingsListPageModel>();
                    tcs?.SetResult(true);
                });
            }
        }

        public FreshAwaitCommand OnLogoutTapped
        {
            get
            {
                return new FreshAwaitCommand((tcs) =>
                {
                    OnLogout?.Invoke(this, true);
                });
            }
        }
    }
}
