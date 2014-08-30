using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

namespace TypeANumber
{
    public class AccomplishmentsOutbox
    {
        public bool mPrimeAchievement = false;
        public bool mHumbleAchievement = false;
        public bool mLeetAchievement = false;
        public bool mArrogantAchievement = false;
        public int mBoredSteps = 0;
        public int mEasyModeScore = -1;
        public int mHardModeScore = -1;

        public bool isEmpty()
        {
            return !mPrimeAchievement && !mHumbleAchievement && !mLeetAchievement &&
                    !mArrogantAchievement && mBoredSteps == 0 && mEasyModeScore < 0 &&
                    mHardModeScore < 0;
        }

        public void saveLocal(Context ctx)
        {
            /* TODO: This is left as an exercise. To make it more difficult to cheat,
             * this data should be stored in an encrypted file! And remember not to
             * expose your encryption key (obfuscate it by building it from bits and
             * pieces and/or XORing with another string, for instance). */
        }

        public void loadLocal(Context ctx)
        {
            /* TODO: This is left as an exercise. Write code here that loads data
             * from the file you wrote in saveLocal(). */
        }
    }
}