using System;
using System.Collections.Generic;
using FreshMvvm;
using PropertyChanged;
using Xamarin.Forms;
using System.Linq;
using System.Threading.Tasks;
using Xamarin.Essentials;
using Microsoft.AppCenter.Crashes;

namespace ManageGo
{
    [AddINotifyPropertyChangedInterface]
    internal class SettingsPageModel : BaseDetailPage
    {
        private bool _applicationsNotificationsIsOn;
        private bool _tenantsNotificationsIsOn;
        private bool _maintenanceNotificationsIsOn;
        private bool _paymentNotificationsIsOn;
        private bool _biometricLoginIsOn;
        Action SwitchToggled;
        private bool _pushNotificationsIsOn;

        public string UserName { get; set; }
        public string UserEmail { get; set; }
        public string UserAlias { get; set; }
        public string UserPassword { get; set; } = "**************";
        public bool PushNotificationsIsOn
        {
            get => _pushNotificationsIsOn;
            set
            {
                _pushNotificationsIsOn = value;
                Preferences.Set("PushNotificationsIsOn", value);
                SwitchToggled?.Invoke();
            }
        }

        public bool BiometricLoginIsOn
        {
            get
            {
                return _biometricLoginIsOn;
            }
            set
            {
                _biometricLoginIsOn = value;
                Preferences.Set("IsBiometricAuthEnabled", value);
            }
        }

        protected override async void ViewIsAppearing(object sender, EventArgs e)
        {
            base.ViewIsAppearing(sender, e);
            try
            {
                var result = await MGDataAccessLibrary.BussinessLogic.UserProcessor.LoginWithExistingCredentials();
                App.UserInfo = new Models.SignedInUserInfo
                {
                    AccessToken = result.UserInfo.AccessToken,
                    UserEmailAddress = result.UserInfo.UserEmailAddress,
                    UserFirstName = result.UserInfo.UserFirstName,
                    UserLastName = result.UserInfo.UserLastName,
                    TenantPushNotification = result.UserInfo.TenantPushNotification,
                    MaintenancePushNotification = result.UserInfo.MaintenancePushNotification,
                    PaymentPushNotification = result.UserInfo.PaymentPushNotification,
                    PushNotification = result.UserInfo.PushNotification,
                    UserID = result.UserInfo.UserID
                };
                UserName = App.UserInfo.UserFirstName + " " + App.UserInfo.UserLastName;
                UserEmail = App.UserInfo.UserEmailAddress;
                PaymentNotificationsIsOn = App.UserInfo.PaymentPushNotification;
                TenantsNotificationsIsOn = App.UserInfo.TenantPushNotification;
                MaintenanceNotificationsIsOn = App.UserInfo.MaintenancePushNotification;
                PushNotificationsIsOn = App.UserInfo.PushNotification;
                BiometricLoginIsOn = Preferences.Get("IsBiometricAuthEnabled", false);
            }
            catch (Exception ex)
            {
                Crashes.TrackError(ex);
            }
        }

        public bool PaymentNotificationsIsOn
        {
            get => _paymentNotificationsIsOn;
            set
            {
                _paymentNotificationsIsOn = value;
                SwitchToggled?.Invoke();
            }
        }

        public bool MaintenanceNotificationsIsOn
        {
            get => _maintenanceNotificationsIsOn;
            set
            {
                _maintenanceNotificationsIsOn = value;
                SwitchToggled?.Invoke();
            }
        }

        public bool TenantsNotificationsIsOn
        {
            get => _tenantsNotificationsIsOn;
            set
            {
                _tenantsNotificationsIsOn = value;
                SwitchToggled?.Invoke();
            }
        }

        public bool ApplicationsNotificationsIsOn
        {
            get => _applicationsNotificationsIsOn;
            set
            {
                _applicationsNotificationsIsOn = value;
                SwitchToggled?.Invoke();
            }
        }



        [AlsoNotifyFor("NameFieldBackgroundColor", "NameFieldBorderColor")]
        public bool NameFieldIsEnabled { get; set; }
        [AlsoNotifyFor("EmailFieldBackgroundColor", "EmailFieldBorderColor")]
        public bool EmailFieldIsEnabled { get; set; }
        [AlsoNotifyFor("DisplayNameFieldBackgroundColor", "DisplayNameFieldBorderColor")]
        public bool DisplayNameFieldIsEnabled { get; set; }
        [AlsoNotifyFor("PasswordFieldBackgroundColor", "PasswordFieldBorderColor")]
        public bool PasswordFieldIsEnabled { get; set; }

        public Color NameFieldBackgroundColor
        {
            get
            {
                return NameFieldIsEnabled ? Color.White : Color.Transparent;
            }
        }
        public Color NameFieldBorderColor
        {
            get
            {
                return NameFieldIsEnabled ? Color.Silver : Color.Transparent;
            }
        }

        public Color PasswordFieldBackgroundColor
        {
            get
            {
                return PasswordFieldIsEnabled ? Color.White : Color.Transparent;
            }
        }
        public Color PasswordFieldBorderColor
        {
            get
            {
                return PasswordFieldIsEnabled ? Color.Silver : Color.Transparent;
            }
        }



        public Color DisplayNameFieldBackgroundColor
        {
            get
            {
                return DisplayNameFieldIsEnabled ? Color.White : Color.Transparent;
            }
        }
        public Color DisplayNameFieldBorderColor
        {
            get
            {
                return DisplayNameFieldIsEnabled ? Color.Silver : Color.Transparent;
            }
        }

        public Color EmailFieldBackgroundColor
        {
            get
            {
                return EmailFieldIsEnabled ? Color.White : Color.Transparent;
            }
        }
        public Color EmailFieldBorderColor
        {
            get
            {
                return EmailFieldIsEnabled ? Color.Silver : Color.Transparent;
            }
        }

        public override void Init(object initData)
        {
            base.Init(initData);
            HamburgerIsVisible = true;
            if (App.UserInfo != null)
            {
                UserName = App.UserInfo.UserFirstName + " " + App.UserInfo.UserLastName;
                UserEmail = App.UserInfo.UserEmailAddress;
                PaymentNotificationsIsOn = App.UserInfo.PaymentPushNotification;
                TenantsNotificationsIsOn = App.UserInfo.TenantPushNotification;
                MaintenanceNotificationsIsOn = App.UserInfo.MaintenancePushNotification;
                PushNotificationsIsOn = App.UserInfo.PushNotification;
                BiometricLoginIsOn = Preferences.Get("IsBiometricAuthEnabled", false);
            }
            async void p() => await UpdateUserDetails();
            SwitchToggled += p;
        }

        public FreshAwaitCommand OnEditButtonTapped
        {
            get
            {
                return new FreshAwaitCommand((par, tcs) =>
                {
                    var name = (string)par;
                    switch (name)
                    {
                        case "UserName":
                            NameFieldIsEnabled = true;
                            break;
                        case "Email":
                            EmailFieldIsEnabled = true;
                            break;
                        case "DisplayName":
                            DisplayNameFieldIsEnabled = true;
                            break;
                        case "Password":
                            PasswordFieldIsEnabled = true;
                            break;
                        default:
                            break;
                    }
                    tcs?.SetResult(true);
                });
            }
        }

        private async Task UpdateUserDetails()
        {
            if (string.IsNullOrWhiteSpace(UserName))
            {
                await CoreMethods.DisplayAlert("ManageGo", "Please enter your name", "OK");
                return;
            }
            else if (string.IsNullOrWhiteSpace(UserEmail) || !UserEmail.Contains("@"))
            {
                await CoreMethods.DisplayAlert("ManageGo", "Email not valid", "OK");
                return;
            }
            var userDetails = new MGDataAccessLibrary.Models.SignedInUserInfo
            {
                UserFirstName = UserName.Split(' ').FirstOrDefault(),
                UserLastName = UserName.Split(' ').LastOrDefault(),
                UserEmailAddress = UserEmail,
                PaymentPushNotification = PaymentNotificationsIsOn,
                MaintenancePushNotification = MaintenanceNotificationsIsOn,
                TenantPushNotification = TenantsNotificationsIsOn,
                PushNotification = PushNotificationsIsOn
            };
            try
            {
                await MGDataAccessLibrary.BussinessLogic.UserProcessor.UpdateUser(userDetails);
            }
            catch (Exception ex)
            {
                await CoreMethods.DisplayAlert("Something went wrong", ex.Message, "DISMISS");
            }
        }

        internal override Task LoadData(bool refreshData = false, bool FetchNextPage = false)
        {
            return new Task(() => { });
        }

        public FreshAwaitCommand OnFieldLostFocus
        {
            get
            {
                async void execute(object par, TaskCompletionSource<bool> tcs)
                {
                    var name = (string)par;
                    switch (name)
                    {
                        case "UserName":
                            NameFieldIsEnabled = false;
                            break;
                        case "Email":
                            EmailFieldIsEnabled = false;
                            break;
                        case "DisplayName":
                            DisplayNameFieldIsEnabled = false;
                            break;
                        case "Password":
                            PasswordFieldIsEnabled = false;
                            break;
                        default:
                            break;
                    }
                    tcs?.SetResult(true);
                    await UpdateUserDetails();
                }
                return new FreshAwaitCommand(execute);
            }
        }
    }
}

