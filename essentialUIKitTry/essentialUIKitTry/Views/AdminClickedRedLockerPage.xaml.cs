using CounterFunctions;
using System.Drawing;
using Xamarin.Forms.Internals;
using Xamarin.Forms.Xaml;
using Xamarin.Forms;

namespace essentialUIKitTry.Views
{
    /// <summary>
    /// Page to show chat profile page
    /// </summary>
    [Preserve(AllMembers = true)]
    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class AdminClickedRedLockerPage
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="AdminClickedRedLockerPage" /> class.
        /// </summary>
        Locker locker;
        bool photoTaken = false;
        public AdminClickedRedLockerPage(int lockerId)
        {
            this.InitializeComponent();
            SetLocker(lockerId);
        }

        async void SetLocker(int lockerId)
        {

            string status = "not set";
            this.locker = await AzureApi.GetLocker(lockerId);
            LockerIdLbl.Text = "Locker Id: " + lockerId;

            this.mailOfUser.Text = $"Mail of Customer: {locker.user_key}";

            string timeRemainingStr = AzureApi.GetRemainingTime(locker);
            TimeRemainingLbl.Text = "Remaing time: " + timeRemainingStr;


            if (this.locker.locked) status = "Locked";
            else status = "Unlocked";
            StatusLbl.Text = "Status: " + status;

            int btnTakephotoFontSize = 12;
            int btn_width = 160;
            int btn_height = 45;
            LockUnlockBtn.Text = this.locker.locked ? "Unlock" : "Lock";
            LockUnlockBtn.WidthRequest = btn_width;
            LockUnlockBtn.HeightRequest = btn_height;
            LockUnlockBtn.BackgroundColor = System.Drawing.Color.LightSteelBlue;
            LockUnlockBtn.Padding = new Xamarin.Forms.Thickness(5, 2);
            LockUnlockBtn.FontSize = btnTakephotoFontSize;
            LockUnlockBtn.Clicked += HandleLockUnlockBtn;

            ReleaseBtn.Text = "Release Locker";
            ReleaseBtn.WidthRequest = btn_width;
            ReleaseBtn.HeightRequest = btn_height;
            ReleaseBtn.BackgroundColor = System.Drawing.Color.LightSteelBlue;
            ReleaseBtn.Padding = new Xamarin.Forms.Thickness(5, 2);
            ReleaseBtn.FontSize = btnTakephotoFontSize;
            ReleaseBtn.Clicked += HandleReleaseBtn;

        }
        async void SetLocker_notUsed(int lockerId)
        {
            string status = "not set";
            this.locker = await AzureApi.GetLocker(lockerId);
            LockerIdLbl.Text = "Locker Id: " + lockerId;


            string timeRemainingStr = AzureApi.GetRemainingTime(locker);
            TimeRemainingLbl.Text = "Remaing time: " + timeRemainingStr;


            if (this.locker.locked) status = "Locked";
            else status = "Unlocked";
            StatusLbl.Text = "Status: " + status;

        }

        void HandleTakeAPhotoBtn(object sender, System.EventArgs e)
        {
            photoTaken = true;
            SetLocker(this.locker.Id);
            AzureApi.TakeLockerPhoto(this.locker.Id + "");
        }
        void HandleLockUnlockBtn(object sender, System.EventArgs e)
        {
            if (this.locker.locked) AzureApi.SetLock(this.locker);
            else AzureApi.SetUnlock(this.locker);
            SetLocker(this.locker.Id);
        }
        async void HandleReleaseBtn(object sender, System.EventArgs e)
        {
            AzureApi.SetAvailable(this.locker.Id);
            await Navigation.PopAsync();

        }

        void Navigate_To_Photo(object sender, System.EventArgs e)
        {
            if (photoTaken)
                Navigation.PushAsync(new InsideALockerImage(this.locker.Id + ""));
        }
    }
}