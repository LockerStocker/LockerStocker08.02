using essentialUIKitTry.Views;
using essentialUIKitTry;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CounterFunctions;

using Xamarin.Forms;
using Xamarin.Forms.Xaml;
using Microsoft.Identity.Client;
using System.IdentityModel.Tokens.Jwt;

namespace essentialUIKitTry
{

    [XamlCompilation(XamlCompilationOptions.Compile)]
    public partial class ChooseALocker : ContentPage
    {

        public List<Locker>[] lockerRows = {new List<Locker>(), new List<Locker>(), new List<Locker>(), new List<Locker>() };

        private AuthenticationResult authenticationResult;
        public ChooseALocker(AuthenticationResult authResult)
        {
            //just in case so you can call this code several times np..
            authenticationResult = authResult;
            //var name = authenticationResult.Account.Username;
            InitializeComponent();
            SetLockerList();
        }


        protected override void OnAppearing()
        {
            GetClaims();
            base.OnAppearing();
        }

        private void GetClaims()
        {
            var token = authenticationResult.IdToken;
            if (token != null)
            {
                var handler = new JwtSecurityTokenHandler();
                var data = handler.ReadJwtToken(authenticationResult.IdToken);
                var claims = data.Claims.ToList();
                App.m_myUserKey = data.Claims.FirstOrDefault(x => x.Type.Equals("email")).Value;
                App.m_adminMode = data.Claims.FirstOrDefault(x => x.Type.Equals("surname")).Value == "ADMIN";

                if (data != null)
                {
                    if (!App.m_adminMode)
                    {
                        this.name.Text = $"Hi {data.Claims.FirstOrDefault(x => x.Type.Equals("name")).Value}!";
                        this.mid_title.Text = "Please Choose Your Locker:";
                        this.balance.Text = $"You have { data.Claims.FirstOrDefault(x => x.Type.Equals("givenName")).Value} Shekels in your account.";
                        //this.email.Text = $"email: {data.Claims.FirstOrDefault(x => x.Type.Equals("email")).Value}";
                    }
                    else
                    {
                        this.name.Text = $"Hi {data.Claims.FirstOrDefault(x => x.Type.Equals("name")).Value}!";
                        this.mid_title.Text = "Here are all your lockers: ";
                        
                        //this.email.Text = $"email: {data.Claims.FirstOrDefault(x => x.Type.Equals("email")).Value}";
                    }
                    //this.name.Text = $"Welcome {data.Claims.FirstOrDefault(x => x.Type.Equals("displayName")).Value}";

                }
            }
        }


        async void SignOutBtn_Clicked(System.Object sender, System.EventArgs e)
        {
            AuthenticationResult result;
            try
            {
                result = await App.AuthenticationClient
                    .AcquireTokenInteractive(Constants.Scopes)
                    .WithPrompt(Prompt.ForceLogin)
                    .WithParentActivityOrWindow(App.UIParent)
                    .ExecuteAsync();

                await Navigation.PushAsync(new ChooseALocker(result));
            }
            catch (MsalClientException)
            {

            }
        }
        Button getBtnForLocker(Locker locker)
        {
            int btnTimingFontSize = 8;
            int btnAvailableFontSize = 12;
            int btn_width = 60;
            int btn_height = 80;
            Button tmp_btn = new Button()
            {
                Text = "L" + locker.Id,
                StyleId = "" + locker.Id,
                WidthRequest = btn_width,
                HeightRequest = btn_height,
                FontSize = btnAvailableFontSize
            };
            if ((!locker.available) && (locker.user_key == App.m_myUserKey))
            {
                tmp_btn.BackgroundColor = Color.LightSteelBlue;
                tmp_btn.Padding = new Xamarin.Forms.Thickness(5, 2);
                tmp_btn.Text = "Time Remaining\n" + AzureApi.GetRemainingTime(locker);
                tmp_btn.FontSize = btnTimingFontSize;
            }
            else if (locker.available)
            {
                tmp_btn.BackgroundColor = Color.Green;
                tmp_btn.Text += "\n" + locker.price_per_hour + "NIS";
            }
            else
            {
                tmp_btn.BackgroundColor = Color.Red;
                if (App.m_adminMode)
                {
                    tmp_btn.Padding = new Xamarin.Forms.Thickness(5, 2);
                    tmp_btn.Text = locker.user_key + "\n" + AzureApi.GetRemainingTime(locker);
                    tmp_btn.FontSize = btnTimingFontSize;
                }
            }
            return tmp_btn;
        }

        async void SetLockerList()
        {
            int numOfRows = 4;
            int lockersInRow = 5;
            ButtonsRow1.Children.Clear();
            ButtonsRow2.Children.Clear();
            ButtonsRow3.Children.Clear();
            ButtonsRow4.Children.Clear();
            //ChooseALockerMainStack.Children.Clear();


            for (int rowIdx = 0; rowIdx < numOfRows; rowIdx++)
            {
                for (int lockerInRowIdx = 0; lockerInRowIdx < lockersInRow; lockerInRowIdx++)
                {
                    Locker tmpLocker = await AzureApi.GetLocker(rowIdx * lockersInRow + lockerInRowIdx + 1);
                    lockerRows[rowIdx].Add(tmpLocker);
                }
            }
            for (int idxInRow = 0; idxInRow < lockersInRow; idxInRow++)
            {
                Button btn1 = getBtnForLocker(lockerRows[0][idxInRow]);
                Button btn2 = getBtnForLocker(lockerRows[1][idxInRow]);
                Button btn3 = getBtnForLocker(lockerRows[2][idxInRow]);
                Button btn4 = getBtnForLocker(lockerRows[3][idxInRow]);

                btn1.Clicked += Locker_ClickedAsync;
                btn2.Clicked += Locker_ClickedAsync;
                btn3.Clicked += Locker_ClickedAsync;
                btn4.Clicked += Locker_ClickedAsync;
                ButtonsRow1.Children.Add(btn1);
                ButtonsRow2.Children.Add(btn2);
                ButtonsRow3.Children.Add(btn3);
                ButtonsRow4.Children.Add(btn4);
            }
            if (App.m_adminMode)
            {
                ModeInfoLbl.Text = "You are logged-in as Admin";
                ModeInfoLbl.TextColor = Color.DarkGreen;
                ModeInfoLbl.FontAttributes = FontAttributes.Bold;

                int btnFontSize = 9;
                int btn_width = 55;
                int btn_height = 45;
                Button SetCostBtn = new Button()
                {
                    BackgroundColor = Color.Black,
                    TextColor = Color.White,
                    Text = "set costs",
                    HorizontalOptions = LayoutOptions.Start,
                    FontSize = btnFontSize,
                    WidthRequest = btn_width,
                    HeightRequest = btn_height,
                    Padding = new Xamarin.Forms.Thickness(5, 2)
                };
                SetCostBtn.Clicked += NavigateToCostSelectionPage;
                ChooseALockerMainStack.Children.Add(SetCostBtn);
            }
        }

        async void NavigateToCostSelectionPage(object sender, System.EventArgs e)
        {
            await Navigation.PushAsync(new SetCostsMainPage());
        }

        async void Locker_ClickedAsync(object sender, System.EventArgs e)
        {
            int locker_id = int.Parse((sender as Button).StyleId);
            var locker = await AzureApi.GetLocker(locker_id);

            if (locker.available)
            {
                AzureApi.SetOccupy(locker_id, "userKey");
                await Navigation.PushAsync(new Locker1OrderedSuccess(locker_id));
            }
            else if (locker.user_key == App.m_myUserKey)
            {
                await Navigation.PushAsync(new LockerProfilePage(locker_id));
            }

            else if (!locker.available && App.m_adminMode)
            {
                await Navigation.PushAsync(new AdminClickedRedLockerPage(locker_id));
            }
            else
            {
                await Navigation.PushAsync(new Locker2OrderFailed("" + locker_id));
            }
            SetLockerList();
        }
    }
}