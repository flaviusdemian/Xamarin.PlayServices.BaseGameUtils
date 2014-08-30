using System;
using Android.Gms.Games;
using Android.Support.V4.App;
using Android.OS;
using Android.Views;
using Android.Widget;
using BaseGameUtils;

namespace TypeANumber
{
    public class MainMenuFragment : Fragment, View.IOnClickListener
    {
        private String mGreeting = "Hello, anonymous user (not signed in)";

        private IMainMenuFragmentListener mListener;
        private bool mShowSignIn = true;

        public void OnClick(View view)
        {
            switch (view.Id)
            {
                case Resource.Id.easy_mode_button:
                    mListener.OnStartGameRequested(false);
                    break;
                case Resource.Id.hard_mode_button:
                    mListener.OnStartGameRequested(true);
                    break;
                case Resource.Id.show_achievements_button:
                    mListener.OnShowAchievementsRequested();
                    break;
                case Resource.Id.show_leaderboards_button:
                    mListener.OnShowLeaderboardsRequested();
                    break;
                case Resource.Id.sign_in_button:
                    mListener.OnSignInButtonClicked();
                    break;
                case Resource.Id.sign_out_button:
                    mListener.OnSignOutButtonClicked();
                    break;
                case Resource.Id.btn_unlock:
                    GamesClass.Achievements.Unlock(BaseGameActivity.mHelper.getApiClient(), "CgkIht6yrNoPEAIQAw");
                    break;
            }
        }

        public override View OnCreateView(LayoutInflater inflater, ViewGroup container,
            Bundle savedInstanceState)
        {
            View v = inflater.Inflate(Resource.Layout.fragment_mainmenu, container, false);
            int[] CLICKABLES =
            {
                Resource.Id.easy_mode_button, Resource.Id.hard_mode_button,
                Resource.Id.show_achievements_button, Resource.Id.show_leaderboards_button,
                Resource.Id.sign_in_button, Resource.Id.sign_out_button, Resource.Id.btn_unlock
            };
            foreach (int i in CLICKABLES)
            {
                v.FindViewById(i).SetOnClickListener(this);
            }
            return v;
        }

        public void setListener(IMainMenuFragmentListener l)
        {
            mListener = l;
        }

        public override void OnStart()
        {
            base.OnStart();
            updateUi();
        }

        public void setGreeting(String greeting)
        {
            mGreeting = greeting;
            updateUi();
        }

        private void updateUi()
        {
            if (Activity == null) return;
            var tv = (TextView) Activity.FindViewById(Resource.Id.hello);
            if (tv != null)
            {
                tv.Text = mGreeting;
            }

            Activity.FindViewById(Resource.Id.sign_in_bar).Visibility = mShowSignIn
                ? ViewStates.Visible
                : ViewStates.Gone;
            Activity.FindViewById(Resource.Id.sign_out_bar).Visibility = mShowSignIn
                ? ViewStates.Gone
                : ViewStates.Visible;
        }

        public void setShowSignInButton(bool showSignIn)
        {
            mShowSignIn = showSignIn;
            updateUi();
        }
    }
}