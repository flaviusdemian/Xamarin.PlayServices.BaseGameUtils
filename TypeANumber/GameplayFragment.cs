using System;
using Android.OS;
using Android.Support.V4.App;
using Android.Views;
using Android.Widget;

namespace TypeANumber
{
    public class GameplayFragment : Fragment, View.IOnClickListener
    {
        private static readonly int[] MY_BUTTONS =
        {
            Resource.Id.digit_button_0, Resource.Id.digit_button_1, Resource.Id.digit_button_2,
            Resource.Id.digit_button_3, Resource.Id.digit_button_4, Resource.Id.digit_button_5,
            Resource.Id.digit_button_6, Resource.Id.digit_button_7, Resource.Id.digit_button_8,
            Resource.Id.digit_button_9, Resource.Id.digit_button_clear, Resource.Id.ok_score_button
        };

        private IGameplayFragmentListener mListener;
        private int mRequestedScore = 5000;

        public void OnClick(View view)
        {
            switch (view.Id)
            {
                case Resource.Id.digit_button_clear:
                    mRequestedScore = 0;
                    updateUi();
                    break;
                case Resource.Id.digit_button_0:
                case Resource.Id.digit_button_1:
                case Resource.Id.digit_button_2:
                case Resource.Id.digit_button_3:
                case Resource.Id.digit_button_4:
                case Resource.Id.digit_button_5:
                case Resource.Id.digit_button_6:
                case Resource.Id.digit_button_7:
                case Resource.Id.digit_button_8:
                case Resource.Id.digit_button_9:
                    int x = int.Parse(((Button) view).Text.Trim());
                    mRequestedScore = (mRequestedScore*10 + x)%10000;
                    updateUi();
                    break;
                case Resource.Id.ok_score_button:
                    mListener.OnEnteredScore(mRequestedScore);
                    break;
            }
        }


        public override View OnCreateView(LayoutInflater inflater, ViewGroup container,
            Bundle savedInstanceState)
        {
            View v = inflater.Inflate(Resource.Layout.fragment_gameplay, container, false);
            foreach (int i in MY_BUTTONS)
            {
                v.FindViewById<Button>(i).SetOnClickListener(this);
            }
            return v;
        }

        public void setListener(IGameplayFragmentListener l)
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
            var scoreInput = Activity.FindViewById<TextView>(Resource.Id.score_input);
            if (scoreInput != null)
            {
                scoreInput.Text = String.Format("%04d", mRequestedScore);
            }
        }
    }
}