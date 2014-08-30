using System;

using Android.Content;
using Android.Gms.Games;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using Android.OS;
using BaseGameUtils;
using Java.Lang;
using Java.Util.Jar;
using Exception = System.Exception;
using Fragment = Android.Support.V4.App.Fragment;
using String = System.String;

namespace TypeANumber
{
    [Android.App.Activity(Name = "com.google.example.games.tanc.MainActivity", Label = "Type-a-Number Challenge", MainLauncher = true)]
    public class MainActivity : BaseGameActivity, IGameHelperListener, IGameplayFragmentListener, IMainMenuFragmentListener, IWinFragmentListener
    {
        // Fragments
        MainMenuFragment mMainMenuFragment;
        GameplayFragment mGameplayFragment;
        WinFragment mWinFragment;

        // request codes we use when invoking an external activity
        //int RC_RESOLVE = 5000,
        int RC_UNUSED = 5001;

        // tag for debug logging
        bool ENABLE_DEBUG = true;
        String TAG = "TanC";

        // playing on hard mode?
        bool mHardMode = false;

        // achievements and scores we're pending to push to the cloud
        // (waiting for the user to sign in, for instance)
        AccomplishmentsOutbox mOutbox = new AccomplishmentsOutbox();

        protected override void OnCreate(Bundle bundle)
        {
            try
            {
                enableDebugLog(ENABLE_DEBUG, TAG);
                base.OnCreate(bundle);
                SetContentView(Resource.Layout.activity_main);

                // create fragments
                mMainMenuFragment = new MainMenuFragment();
                mGameplayFragment = new GameplayFragment();
                mWinFragment = new WinFragment();

                // listen to fragment events
                mMainMenuFragment.setListener(this);
                mGameplayFragment.setListener(this);
                mWinFragment.setListener(this);

                // add initial fragment (welcome fragment)
                SupportFragmentManager.BeginTransaction().Add(Resource.Id.fragment_container, mMainMenuFragment).Commit();

                // IMPORTANT: if this Activity supported rotation, we'd have to be
                // more careful about adding the fragment, since the fragment would
                // already be there after rotation and trying to add it again would
                // result in overlapping fragments. But since we don't support rotation,
                // we don't deal with that for code simplicity.

                // load outbox from file
                mOutbox.loadLocal(this);

            }
            catch (Exception ex)
            {
                ex.ToString();
            }
        }

        // Switch UI to the given fragment
        void switchToFragment(Fragment newFrag)
        {
            SupportFragmentManager.BeginTransaction().Replace(Resource.Id.fragment_container, newFrag).Commit();
        }

        public void OnStartGameRequested(bool hardMode)
        {
            startGame(hardMode);
        }

        public void OnShowAchievementsRequested()
        {
            if (isSignedIn())
            {
                StartActivityForResult(GamesClass.Achievements.GetAchievementsIntent(getApiClient()), RC_UNUSED);
            }
            else
            {
                showAlert(GetString(Resource.String.achievements_not_available));
            }
        }

        public void OnShowLeaderboardsRequested()
        {
            if (isSignedIn())
            {
                StartActivityForResult(GamesClass.Leaderboards.GetAllLeaderboardsIntent(getApiClient()), RC_UNUSED);
            }
            else
            {
                showAlert(GetString(Resource.String.leaderboards_not_available));
            }
        }

        /**
         * Start gameplay. This means updating some status variables and switching
         * to the "gameplay" screen (the screen where the user types the score they want).
         *
         * @param hardMode whether to start gameplay in "hard mode".
         */
        void startGame(bool hardMode)
        {
            mHardMode = hardMode;
            switchToFragment(mGameplayFragment);
        }

        /**
         * Checks that the developer (that's you!) read the instructions.
         *
         * IMPORTANT: a method like this SHOULD NOT EXIST in your production app!
         * It merely exists here to check that anyone running THIS PARTICULAR SAMPLE
         * did what they were supposed to in order for the sample to work.
         */
        bool verifyPlaceholderIdsReplaced()
        {
            bool CHECK_PKGNAME = false; // set to false to disable check
            // (not recommended!)

            // Did the developer forget to change the package name?
            if (CHECK_PKGNAME && PackageName.StartsWith("com.google.example."))
            {
                Log.Error(TAG, "*** Sample setup problem: " +
                    "package name cannot be com.google.example.*. Use your own " +
                    "package name.");
                return false;
            }

            // Did the developer forget to replace a placeholder ID?
            int[] res_ids = new int[] {
                Resource.String.app_id, Resource.String.achievement_arrogant,
                Resource.String.achievement_bored, Resource.String.achievement_humble,
                Resource.String.achievement_leet, Resource.String.achievement_prime,
                Resource.String.leaderboard_easy, Resource.String.leaderboard_hard
            };
            foreach (int i in res_ids)
            {
                if (GetString(i).Equals("ReplaceMe", StringComparison.CurrentCultureIgnoreCase))
                {
                    Log.Error(TAG, "*** Sample setup problem: You must replace all " + "placeholder IDs in the ids.xml file by your project's IDs.");
                    return false;
                }
            }
            return true;
        }

        public void OnEnteredScore(int requestedScore)
        {
            // Compute  score (in easy mode, it's the requested score; in hard mode, it's half)
            int Score = mHardMode ? requestedScore / 2 : requestedScore;

            mWinFragment.setFinalScore(Score);
            mWinFragment.setExplanation(mHardMode ? GetString(Resource.String.hard_mode_explanation) : GetString(Resource.String.easy_mode_explanation));

            // check for achievements
            checkForAchievements(requestedScore, Score);

            // update leaderboards
            updateLeaderboards(Score);

            // push those accomplishments to the cloud, if signed in
            pushAccomplishments();

            // switch to the exciting "you won" screen
            switchToFragment(mWinFragment);
        }

        // Checks if n is prime. We don't consider 0 and 1 to be prime.
        // This is not an implementation we are mathematically proud of, but it gets the job done.
        bool isPrime(int n)
        {
            int i;
            if (n == 0 || n == 1) return false;
            for (i = 2; i <= n / 2; i++)
            {
                if (n % i == 0)
                {
                    return false;
                }
            }
            return true;
        }

        /**
         * Check for achievements and unlock the appropriate ones.
         *
         * @param requestedScore the score the user requested.
         * @param Score the score the user got.
         */
        void checkForAchievements(int requestedScore, int Score)
        {
            // Check if each condition is met; if so, unlock the corresponding
            // achievement.
            if (isPrime(Score))
            {
                mOutbox.mPrimeAchievement = true;
                achievementToast(GetString(Resource.String.achievement_prime_toast_text));
            }
            if (requestedScore == 9999)
            {
                mOutbox.mArrogantAchievement = true;
                achievementToast(GetString(Resource.String.achievement_arrogant_toast_text));
            }
            if (requestedScore == 0)
            {
                mOutbox.mHumbleAchievement = true;
                achievementToast(GetString(Resource.String.achievement_humble_toast_text));
            }
            if (Score == 1337)
            {
                mOutbox.mLeetAchievement = true;
                achievementToast(GetString(Resource.String.achievement_leet_toast_text));
            }
            mOutbox.mBoredSteps++;
        }

        void unlockAchievement(int achievementId, String fallbackString)
        {
            if (isSignedIn())
            {
                GamesClass.Achievements.Unlock(getApiClient(), GetString(achievementId));
            }
            else
            {
                Toast.MakeText(this, GetString(Resource.String.achievement) + ": " + fallbackString, ToastLength.Long).Show();
            }
        }

        void achievementToast(String achievement)
        {
            // Only show toast if not signed in. If signed in, the standard Google Play
            // toasts will appear, so we don't need to show our own.
            if (!isSignedIn())
            {
                Toast.MakeText(this, GetString(Resource.String.achievement) + ": " + achievement, ToastLength.Long).Show();
            }
        }

        void pushAccomplishments()
        {
            if (!isSignedIn())
            {
                // can't push to the cloud, so save locally
                mOutbox.saveLocal(this);
                return;
            }
            if (mOutbox.mPrimeAchievement)
            {
                GamesClass.Achievements.Unlock(getApiClient(), GetString(Resource.String.achievement_prime));
                mOutbox.mPrimeAchievement = false;
            }
            if (mOutbox.mArrogantAchievement)
            {
                GamesClass.Achievements.Unlock(getApiClient(), GetString(Resource.String.achievement_arrogant));
                mOutbox.mArrogantAchievement = false;
            }
            if (mOutbox.mHumbleAchievement)
            {
                GamesClass.Achievements.Unlock(getApiClient(), GetString(Resource.String.achievement_humble));
                mOutbox.mHumbleAchievement = false;
            }
            if (mOutbox.mLeetAchievement)
            {
                GamesClass.Achievements.Unlock(getApiClient(), GetString(Resource.String.achievement_leet));
                mOutbox.mLeetAchievement = false;
            }
            if (mOutbox.mBoredSteps > 0)
            {
                GamesClass.Achievements.Increment(getApiClient(), GetString(Resource.String.achievement_really_bored), mOutbox.mBoredSteps);
                GamesClass.Achievements.Increment(getApiClient(), GetString(Resource.String.achievement_bored), mOutbox.mBoredSteps);
            }
            if (mOutbox.mEasyModeScore >= 0)
            {
                GamesClass.Leaderboards.SubmitScore(getApiClient(), GetString(Resource.String.leaderboard_easy), mOutbox.mEasyModeScore);
                mOutbox.mEasyModeScore = -1;
            }
            if (mOutbox.mHardModeScore >= 0)
            {
                GamesClass.Leaderboards.SubmitScore(getApiClient(), GetString(Resource.String.leaderboard_hard), mOutbox.mHardModeScore);
                mOutbox.mHardModeScore = -1;
            }
            mOutbox.saveLocal(this);
        }

        /**
         * Update leaderboards with the user's score.
         *
         * @param Score The score the user got.
         */
        void updateLeaderboards(int Score)
        {
            if (mHardMode && mOutbox.mHardModeScore < Score)
            {
                mOutbox.mHardModeScore = Score;
            }
            else if (!mHardMode && mOutbox.mEasyModeScore < Score)
            {
                mOutbox.mEasyModeScore = Score;
            }
        }

        public void OnWinScreenDismissed()
        {
            switchToFragment(mMainMenuFragment);
        }

        public void OnSignInFailed()
        {
            // Sign-in failed, so show sign-in button on main menu
            mMainMenuFragment.setGreeting(GetString(Resource.String.signed_out_greeting));
            mMainMenuFragment.setShowSignInButton(true);
            mWinFragment.setShowSignInButton(true);
        }

        public void OnSignInSucceeded()
        {
            // Show sign-out button on main menu
            mMainMenuFragment.setShowSignInButton(false);

            // Show "you are signed in" message on win screen, with no sign in button.
            mWinFragment.setShowSignInButton(false);

            // Set the greeting appropriately on main menu
            IPlayer p = GamesClass.Players.GetCurrentPlayer(getApiClient());
            String displayName;
            if (p == null)
            {
                Log.Warn(TAG, "mGamesClient.getCurrentPlayer() is NULL!");
                displayName = "???";
            }
            else
            {
                displayName = p.DisplayName;
            }
            mMainMenuFragment.setGreeting("Hello, " + displayName);


            // if we have accomplishments to push, push them
            if (!mOutbox.isEmpty())
            {
                pushAccomplishments();
                Toast.MakeText(this, GetString(Resource.String.your_progress_will_be_uploaded), ToastLength.Long).Show();
            }
        }

        public void OnSignInButtonClicked()
        {
            // check if developer read the documentation!
            // (Note: in a production application, this code should NOT exist)
            if (!verifyPlaceholderIdsReplaced())
            {
                showAlert("Sample not set up correctly. See README.");
                return;
            }

            // start the sign-in flow
            beginUserInitiatedSignIn();
        }

        public void OnSignOutButtonClicked()
        {
            signOut();
            mMainMenuFragment.setGreeting(GetString(Resource.String.signed_out_greeting));
            mMainMenuFragment.setShowSignInButton(true);
            mWinFragment.setShowSignInButton(true);
        }

        public void OnWinScreenSignInClicked()
        {
            beginUserInitiatedSignIn();
        }
    }
}

