using System;
using Android.OS;
using Android.Support.V4.App;
using Android.Views;
using Android.Widget;

namespace TypeANumber
{
    public class WinFragment : Fragment, View.IOnClickListener
    {
        private String mExplanation = "";
        private IWinFragmentListener mListener;
        private int mScore;
        private bool mShowSignIn;

        public void OnClick(View view)
        {
            if (view.Id == Resource.Id.win_screen_sign_in_button)
            {
                mListener.OnWinScreenSignInClicked();
            }
            mListener.OnWinScreenDismissed();
        }

        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            View v = inflater.Inflate(Resource.Layout.fragment_win, container, false);
            v.FindViewById(Resource.Id.win_ok_button).SetOnClickListener(this);
            v.FindViewById(Resource.Id.win_screen_sign_in_button).SetOnClickListener(this);
            return v;
        }

        public void setFinalScore(int i)
        {
            mScore = i;
        }

        public void setExplanation(String s)
        {
            mExplanation = s;
        }

        public void setListener(IWinFragmentListener l)
        {
            mListener = l;
        }

        public override void OnStart()
        {
            base.OnStart();
            updateUi();
        }

        private void updateUi()
        {
            if (Activity == null)
            {
                return;
            }
            var scoreTv = Activity.FindViewById<TextView>(Resource.Id.score_display);
            var explainTv = Activity.FindViewById<TextView>(Resource.Id.scoreblurb);

            if (scoreTv != null) scoreTv.Text = Java.Lang.String.ValueOf(mScore);
            if (explainTv != null) explainTv.Text = mExplanation;

            Activity.FindViewById(Resource.Id.win_screen_sign_in_bar).Visibility = mShowSignIn
                ? ViewStates.Visible
                : ViewStates.Gone;
            Activity.FindViewById(Resource.Id.win_screen_signed_in_bar).Visibility = mShowSignIn
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