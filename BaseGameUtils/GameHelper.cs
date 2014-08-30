using System;
using System.Collections.Generic;
using Android.App;
using Android.Content;
using Android.Gms.AppStates;
using Android.Gms.Common;
using Android.Gms.Common.Apis;
using Android.Gms.Drive;
using Android.Gms.Games;
using Android.Gms.Games.MultiPlayer;
using Android.Gms.Games.MultiPlayer.TurnBased;
using Android.Gms.Games.Request;
using Android.Gms.Plus;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Java.Lang;
using Exception = System.Exception;
using Object = Java.Lang.Object;
using String = System.String;

namespace BaseGameUtils
{
    public class GameHelper : Object, IGoogleApiClientConnectionCallbacks, IGoogleApiClientOnConnectionFailedListener
    {
        public static String TAG = "GameHelper";

        // configuration done?

        // Request code we use when invoking other Activities to complete the
        // sign-in flow.
        private static int RC_RESOLVE = 9001;

        // Request code when invoking Activities whose result we don't care about.
        private static int RC_UNUSED = 9002;

        // the Google API client builder we will use to create GoogleApiClient

        // Client request flags
        public static int CLIENT_NONE = 0x00;
        public static int CLIENT_GAMES = 0x01;
        public static int CLIENT_PLUS = 0x02;
        public static int CLIENT_APPSTATE = 0x04;
        public static int CLIENT_SNAPSHOT = 0x08;

        public static int CLIENT_ALL = CLIENT_GAMES | CLIENT_PLUS
                                       | CLIENT_APPSTATE | CLIENT_SNAPSHOT;

        private static int DEFAULT_MAX_SIGN_IN_ATTEMPTS = 3;
        private readonly Handler mHandler;

        // What clients were requested? (bit flags)
        private readonly int mRequestedClients = CLIENT_NONE;
        private String GAMEHELPER_SHARED_PREFS = "GAMEHELPER_SHARED_PREFS";
        private String KEY_SIGN_IN_CANCELLATIONS = "KEY_SIGN_IN_CANCELLATIONS";
        private Activity mActivity;

        // app context
        private Context mAppContext;
        private Api.ApiOptionsNoOptions mAppStateApiOptions;

        // Whether to automatically try to sign in on onStart(). We only set this
        // to true when the sign-in process fails or the user explicitly signs out.
        // We set it back to false when the user initiates the sign in process.
        private bool mConnectOnStart = true;
        private bool mConnecting;

        /*
         * Whether user has specifically requested that the sign-in process begin.
         * If mUserInitiatedSignIn is false, we're in the automatic sign-in attempt
         * that we try once the Activity is started -- if true, then the user has
         * already clicked a "Sign-In" button or something similar
         */

        // The connection result we got from our last attempt to sign-in.
        private ConnectionResult mConnectionResult;

        // The error that happened during sign-in.

        // Print debug logs?
        private bool mDebugLog;
        private bool mExpectingResolution;
        private GamesClass.GamesOptions mGamesApiOptions = GamesClass.GamesOptions.InvokeBuilder().Build();
        private IGoogleApiClient mGoogleApiClient;
        private GoogleApiClientBuilder mGoogleApiClientBuilder;

        /*
         * If we got an invitation when we connected to the games client, it's here.
         * Otherwise, it's null.
         */
        private IInvitation mInvitation;

        /*
         * If we got turn-based match when we connected to the games client, it's
         * here. Otherwise, it's null.
         */

        // Listener
        private IGameHelperListener mListener;

        // Should we start the flow to sign the user in automatically on startup? If
        // so, up to
        // how many times in the life of the application?
        private int mMaxAutoSignInAttempts = DEFAULT_MAX_SIGN_IN_ATTEMPTS;
        private PlusClass.PlusOptions mPlusApiOptions;
        private IList<IGameRequest> mRequests;
        private bool mSetupDone;
        private bool mShowErrorDialogs = true;
        private bool mSignInCancelled;
        private SignInFailureReason mSignInFailureReason;
        private ITurnBasedMatch mTurnBasedMatch;
        private bool mUserInitiatedSignIn;

        /**
         * Construct a GameHelper object, initially tied to the given Activity.
         * After constructing this object, call @link{setup} from the onCreate()
         * method of your Activity.
         *
         * @param clientsToUse
         *            the API clients to use (a combination of the CLIENT_* flags,
         *            or CLIENT_ALL to mean all clients).
         */

        public GameHelper(Activity activity, int clientsToUse)
        {
            mActivity = activity;
            mAppContext = activity.ApplicationContext;
            mRequestedClients = clientsToUse;
            mHandler = new Handler();
        }

        public void OnConnected(Bundle connectionHint)
        {
            debugLog("onConnected: connected!");

            if (connectionHint != null)
            {
                debugLog("onConnected: connection hint provided. Checking for invite.");
                var inv = connectionHint.GetParcelable(Multiplayer.ExtraInvitation).JavaCast<IInvitation>();
                if (inv != null && inv.InvitationId != null)
                {
                    // retrieve and cache the invitation ID
                    debugLog("onConnected: connection hint has a room invite!");
                    mInvitation = inv;
                    debugLog("Invitation ID: " + mInvitation.InvitationId);
                }

                // Do we have any requests pending?
                mRequests = GamesClass.Requests.GetGameRequestsFromBundle(connectionHint);
                if (mRequests.Count != 0)
                {
                    // We have requests in onConnected's connectionHint.
                    debugLog("onConnected: connection hint has " + mRequests.Count + " request(s)");
                }

                debugLog("onConnected: connection hint provided. Checking for TBMP game.");
                mTurnBasedMatch =
                    connectionHint.GetParcelable(Multiplayer.ExtraTurnBasedMatch).JavaCast<ITurnBasedMatch>();
            }

            // we're good to go
            succeedSignIn();
        }

        public void OnConnectionSuspended(int cause)
        {
            debugLog("onConnectionSuspended, cause=" + cause);
            disconnect();
            mSignInFailureReason = null;
            debugLog("Making extraordinary call to OnSignInFailed callback");
            mConnecting = false;
            notifyListener(false);
        }

        public void OnConnectionFailed(ConnectionResult result)
        {
            // save connection result for later reference
            debugLog("onConnectionFailed");

            mConnectionResult = result;
            debugLog("Connection failure:");
            debugLog("   - code: " + GameHelperUtils.errorCodeToString(mConnectionResult.ErrorCode));
            debugLog("   - resolvable: " + mConnectionResult.HasResolution);
            debugLog("   - details: " + mConnectionResult);

            int cancellations = getSignInCancellations();
            bool shouldResolve = false;

            if (mUserInitiatedSignIn)
            {
                debugLog("onConnectionFailed: WILL resolve because user initiated sign-in.");
                shouldResolve = true;
            }
            else if (mSignInCancelled)
            {
                debugLog("onConnectionFailed WILL NOT resolve (user already cancelled once).");
                shouldResolve = false;
            }
            else if (cancellations < mMaxAutoSignInAttempts)
            {
                debugLog("onConnectionFailed: WILL resolve because we have below the max# of "
                         + "attempts, "
                         + cancellations
                         + " < "
                         + mMaxAutoSignInAttempts);
                shouldResolve = true;
            }
            else
            {
                shouldResolve = false;
                debugLog("onConnectionFailed: Will NOT resolve; not user-initiated and max attempts "
                         + "reached: "
                         + cancellations
                         + " >= "
                         + mMaxAutoSignInAttempts);
            }

            if (!shouldResolve)
            {
                // Fail and wait for the user to want to sign in.
                debugLog("onConnectionFailed: since we won't resolve, failing now.");
                mConnectionResult = result;
                mConnecting = false;
                notifyListener(false);
                return;
            }

            debugLog("onConnectionFailed: resolving problem...");

            // Resolve the connection result. This usually means showing a dialog or
            // starting an Activity that will allow the user to give the appropriate
            // consents so that sign-in can be successful.
            resolveConnectionResult();
        }

        /**
         * Sets the maximum number of automatic sign-in attempts to be made on
         * application startup. This maximum is over the lifetime of the application
         * (it is stored in a SharedPreferences file). So, for example, if you
         * specify 2, then it means that the user will be prompted to sign in on app
         * startup the first time and, if they cancel, a second time the next time
         * the app starts, and, if they cancel that one, never again. Set to 0 if
         * you do not want the user to be prompted to sign in on application
         * startup.
         */

        public void setMaxAutoSignInAttempts(int max)
        {
            mMaxAutoSignInAttempts = max;
        }

        private void assertConfigured(String operation)
        {
            if (!mSetupDone)
            {
                String error = "GameHelper error: Operation attempted without setup: "
                               + operation
                               + ". The setup() method must be called before attempting any other operation.";
                logError(error);
                throw new IllegalStateException(error);
            }
        }

        private void doApiOptionsPreCheck()
        {
            if (mGoogleApiClientBuilder != null)
            {
                String error = "GameHelper: you cannot call set*ApiOptions after the client "
                               + "builder has been created. Call it before calling CreateApiClientBuilder() "
                               + "or setup().";
                logError(error);
                throw new IllegalStateException(error);
            }
        }

        /**
         * Sets the options to pass when setting up the Games API. Call before
         * setup().
         */

        public void setGamesApiOptions(GamesClass.GamesOptions options)
        {
            doApiOptionsPreCheck();
            mGamesApiOptions = options;
        }

        /**
         * Sets the options to pass when setting up the AppState API. Call before
         * setup().
         */

        public void setAppStateApiOptions(Api.ApiOptionsNoOptions options)
        {
            doApiOptionsPreCheck();
            mAppStateApiOptions = options;
        }

        /**
         * Sets the options to pass when setting up the Plus API. Call before
         * setup().
         */

        public void setPlusApiOptions(PlusClass.PlusOptions options)
        {
            doApiOptionsPreCheck();
            mPlusApiOptions = options;
        }

        /**
         * Creates a GoogleApiClient.Builder for use with @link{#setup}. Normally,
         * you do not have to do this; use this method only if you need to make
         * nonstandard setup (e.g. adding extra scopes for other APIs) on the
         * GoogleApiClient.Builder before calling @link{#setup}.
         */

        public GoogleApiClientBuilder CreateApiClientBuilder()
        {
            try
            {
                if (mSetupDone)
                {
                    String error = "GameHelper: you called GameHelper.CreateApiClientBuilder() after " +
                                   "calling setup. You can only get a client builder BEFORE performing setup.";
                    logError(error);
                    throw new IllegalStateException(error);
                }

                var builder = new GoogleApiClientBuilder(mActivity, this, this);

                if (0 != (mRequestedClients & CLIENT_GAMES))
                {
                    try
                    {
                        //TODO: FIX
                        builder.AddApi(GamesClass.Api, (Object)mGamesApiOptions);
                    }
                    catch (Exception ex)
                    {
                        ex.ToString();
                        builder.AddApi(GamesClass.Api);
                    }
                    builder.AddScope(GamesClass.ScopeGames);

                }

                if (0 != (mRequestedClients & CLIENT_PLUS))
                {
                    try
                    {
                        builder.AddApi(PlusClass.Api);
                        builder.AddScope(PlusClass.ScopePlusLogin);
                    }
                    catch (Exception ex)
                    {
                        ex.ToString();
                    }
                }

                if (0 != (mRequestedClients & CLIENT_APPSTATE))
                {
                    try
                    {
                        builder.AddApi(AppStateManager.Api);
                        builder.AddScope(AppStateManager.ScopeAppState);
                    }
                    catch (Exception ex)
                    {
                        ex.ToString();
                    }
                }

                if (0 != (mRequestedClients & CLIENT_SNAPSHOT))
                {
                    try
                    {
                        builder.AddScope(DriveClass.ScopeAppfolder);
                        builder.AddApi(DriveClass.Api);
                    }
                    catch (Exception ex)
                    {
                        ex.ToString();
                    }
                }

                mGoogleApiClientBuilder = builder;
                return builder;
            }
            catch (Exception ex)
            {
                ex.ToString();
            }
            return null;
        }

        /**
         * Performs setup on this GameHelper object. Call this from the onCreate()
         * method of your Activity. This will create the clients and do a few other
         * initialization tasks. Next, call @link{#onStart} from the onStart()
         * method of your Activity.
         *
         * @param listener
         *            The listener to be notified of sign-in events.
         */

        public void setup(IGameHelperListener listener)
        {
            try
            {
                if (listener == null)
                {
                    throw new Exception("The listener is not of type IGameHelperListener");
                }
                if (mSetupDone)
                {
                    String error = "GameHelper: you cannot call GameHelper.setup() more than once!";
                    logError(error);
                    throw new IllegalStateException(error);
                }
                mListener = listener;
                debugLog("Setup: requested clients: " + mRequestedClients);

                if (mGoogleApiClientBuilder == null)
                {
                    // we don't have a builder yet, so create one
                    mGoogleApiClientBuilder = CreateApiClientBuilder();
                }

                mGoogleApiClient = mGoogleApiClientBuilder.Build();
                mGoogleApiClientBuilder = null;
                mSetupDone = true;
            }
            catch (Exception ex)
            {
                ex.ToString();
            }
        }

        /**
         * Returns the GoogleApiClient object. In order to call this method, you
         * must have called @link{setup}.
         */

        public IGoogleApiClient getApiClient()
        {
            if (mGoogleApiClient == null)
            {
                throw new IllegalStateException("No GoogleApiClient. Did you call setup()?");
            }
            return mGoogleApiClient;
        }

        /** Returns whether or not the user is signed in. */

        public bool isSignedIn()
        {
            return mGoogleApiClient != null && mGoogleApiClient.IsConnected;
        }

        /** Returns whether or not we are currently connecting */

        public bool isConnecting()
        {
            return mConnecting;
        }

        /**
         * Returns whether or not there was a (non-recoverable) error during the
         * sign-in process.
         */

        public bool hasSignInError()
        {
            return mSignInFailureReason != null;
        }

        /**
         * Returns the error that happened during the sign-in process, null if no
         * error occurred.
         */

        public SignInFailureReason getSignInError()
        {
            return mSignInFailureReason;
        }

        // Set whether to show error dialogs or not.
        public void setShowErrorDialogs(bool show)
        {
            mShowErrorDialogs = show;
        }

        /** Call this method from your Activity's onStart(). */

        public void onStart(Activity act)
        {
            try
            {
                mActivity = act;
                mAppContext = act.ApplicationContext;

                debugLog("onStart");
                assertConfigured("onStart");

                if (mConnectOnStart)
                {
                    if (mGoogleApiClient.IsConnected)
                    {
                        Log.Warn(TAG,
                            "GameHelper: client was already connected on onStart()");
                    }
                    else
                    {
                        debugLog("Connecting client.");
                        mConnecting = true;
                        mGoogleApiClient.Connect();
                    }
                }
                else
                {
                    debugLog("Not attempting to connect becase mConnectOnStart=false");
                    debugLog("Instead, reporting a sign-in failure.");
                    var runnable = new Runnable(() => { notifyListener(false); });
                    mHandler.PostDelayed(runnable, 1000);
                }
            }
            catch (Exception ex)
            {
                ex.ToString();
            }
        }

        /** Call this method from your Activity's onStop(). */

        public void onStop()
        {
            debugLog("onStop");
            assertConfigured("onStop");
            if (mGoogleApiClient.IsConnected)
            {
                debugLog("Disconnecting client due to onStop");
                mGoogleApiClient.Disconnect();
            }
            else
            {
                debugLog("Client already disconnected when we got onStop.");
            }
            mConnecting = false;
            mExpectingResolution = false;

            // let go of the Activity reference
            mActivity = null;
        }

        /**
         * Returns the invitation ID received through an invitation notification.
         * This should be called from your GameHelperListener's
         *
         * @link{GameHelperListener#OnSignInSucceeded method, to check if there's an
         *                                            invitation available. In that
         *                                            case, accept the invitation.
         * @return The id of the invitation, or null if none was received.
         */

        public String getInvitationId()
        {
            if (!mGoogleApiClient.IsConnected)
            {
                Log.Warn(TAG,
                    "Warning: getInvitationId() should only be called when signed in, " +
                    "that is, after getting onSignInSuceeded()");
            }
            return mInvitation == null ? null : mInvitation.InvitationId;
        }

        /**
         * Returns the invitation received through an invitation notification. This
         * should be called from your GameHelperListener's
         *
         * @link{GameHelperListener#OnSignInSucceeded method, to check if there's an
         *                                            invitation available. In that
         *                                            case, accept the invitation.
         * @return The invitation, or null if none was received.
         */

        public IInvitation getInvitation()
        {
            if (!mGoogleApiClient.IsConnected)
            {
                Log.Warn(TAG,
                    "Warning: getInvitation() should only be called when signed in, " +
                    "that is, after getting onSignInSuceeded()");
            }
            return mInvitation;
        }

        public bool hasInvitation()
        {
            return mInvitation != null;
        }

        public bool hasTurnBasedMatch()
        {
            return mTurnBasedMatch != null;
        }

        public bool hasRequests()
        {
            return mRequests != null;
        }

        public void clearInvitation()
        {
            mInvitation = null;
        }

        public void clearTurnBasedMatch()
        {
            mTurnBasedMatch = null;
        }

        public void clearRequests()
        {
            mRequests = null;
        }

        /**
         * Returns the tbmp match received through an invitation notification. This
         * should be called from your GameHelperListener's
         *
         * @link{GameHelperListener#OnSignInSucceeded method, to check if there's a
         *                                            match available.
         * @return The match, or null if none was received.
         */

        public ITurnBasedMatch getTurnBasedMatch()
        {
            if (!mGoogleApiClient.IsConnected)
            {
                Log.Warn(TAG,
                    "Warning: getTurnBasedMatch() should only be called when signed in, "
                    + "that is, after getting onSignInSuceeded()");
            }
            return mTurnBasedMatch;
        }

        /**
         * Returns the requests received through the onConnected bundle. This should
         * be called from your GameHelperListener's
         *
         * @link{GameHelperListener#OnSignInSucceeded method, to check if there are
         *                                            incoming requests that must be
         *                                            handled.
         * @return The requests, or null if none were received.
         */

        public IList<IGameRequest> getRequests()
        {
            if (!mGoogleApiClient.IsConnected)
            {
                Log.Warn(TAG, "Warning: getRequests() should only be called "
                              + "when signed in, "
                              + "that is, after getting onSignInSuceeded()");
            }
            return mRequests;
        }

        /** Enables debug logging */

        public void enableDebugLog(bool enabled)
        {
            mDebugLog = enabled;
            if (enabled)
            {
                debugLog("Debug log enabled.");
            }
        }

        [Deprecated]
        public void enableDebugLog(bool enabled, String tag)
        {
            Log.Warn(TAG, "GameHelper.enableDebugLog(bool,String) is deprecated. "
                          + "Use GameHelper.enableDebugLog(bool)");
            enableDebugLog(enabled);
        }

        /** Sign out and disconnect from the APIs. */

        public void signOut()
        {
            if (!mGoogleApiClient.IsConnected)
            {
                // nothing to do
                debugLog("signOut: was already disconnected, ignoring.");
                return;
            }

            // for Plus, "signing out" means clearing the default account and
            // then disconnecting
            if (0 != (mRequestedClients & CLIENT_PLUS))
            {
                debugLog("Clearing default account on PlusClient.");
                PlusClass.AccountApi.ClearDefaultAccount(mGoogleApiClient);
            }

            // For the games client, signing out means calling signOut and
            // disconnecting
            if (0 != (mRequestedClients & CLIENT_GAMES))
            {
                debugLog("Signing out from the Google API Client.");
                GamesClass.SignOut(mGoogleApiClient);
            }

            // Ready to disconnect
            debugLog("Disconnecting client.");
            mConnectOnStart = false;
            mConnecting = false;
            mGoogleApiClient.Disconnect();
        }

        /**
         * Handle activity result. Call this method from your Activity's
         * onActivityResult callback. If the activity result pertains to the sign-in
         * process, processes it appropriately.
         */

        public void onActivityResult(int requestCode, int responseCode, Intent intent)
        {
            try
            {
                debugLog("onActivityResult: req="
                         + (requestCode == RC_RESOLVE ? "RC_RESOLVE" : Java.Lang.String.ValueOf(requestCode)) +
                         ", resp="
                         + GameHelperUtils.activityResponseCodeToString(responseCode));
                if (requestCode != RC_RESOLVE)
                {
                    debugLog("onActivityResult: request code not meant for us. Ignoring.");
                    return;
                }

                // no longer expecting a resolution
                mExpectingResolution = false;

                if (!mConnecting)
                {
                    debugLog("onActivityResult: ignoring because we are not connecting.");
                    return;
                }

                // We're coming back from an activity that was launched to resolve a
                // connection problem. For example, the sign-in UI.
                if (responseCode == (int)Result.Ok)
                {
                    // Ready to try to connect again.
                    debugLog("onAR: Resolution was RESULT_OK, so connecting current client again.");
                    connect();
                }
                else if (responseCode == GamesActivityResultCodes.ResultReconnectRequired)
                {
                    debugLog("onAR: Resolution was RECONNECT_REQUIRED, so reconnecting.");
                    connect();
                }
                else if (responseCode == (int)Result.Canceled)
                {
                    // User cancelled.
                    debugLog("onAR: Got a cancellation result, so disconnecting.");
                    mSignInCancelled = true;
                    mConnectOnStart = false;
                    mUserInitiatedSignIn = false;
                    mSignInFailureReason = null; // cancelling is not a failure!
                    mConnecting = false;
                    mGoogleApiClient.Disconnect();

                    // increment # of cancellations
                    int prevCancellations = getSignInCancellations();
                    int newCancellations = incrementSignInCancellations();
                    debugLog("onAR: # of cancellations " + prevCancellations + " --> " + newCancellations + ", max " + mMaxAutoSignInAttempts);

                    notifyListener(false);
                }
                else
                {
                    // Whatever the problem we were trying to solve, it was not
                    // solved. So give up and show an error message.
                    debugLog("onAR: responseCode=" + GameHelperUtils.activityResponseCodeToString(responseCode) + ", so giving up.");
                    giveUp(new SignInFailureReason(mConnectionResult.ErrorCode, responseCode));
                }
            }
            catch (Exception ex)
            {
                ex.ToString();
            }
        }

        private void notifyListener(bool success)
        {
            try
            {
                debugLog("Notifying LISTENER of sign-in "
                         + (success  ? "SUCCESS" : mSignInFailureReason != null  ? "FAILURE (error)" : "FAILURE (no error)"));
                if (mListener != null)
                {
                    if (success)
                    {
                        mListener.OnSignInSucceeded();
                    }
                    else
                    {
                        mListener.OnSignInFailed();
                    }
                }
            }
            catch (Exception ex)
            {
                ex.ToString();
            }
        }

        /**
         * Starts a user-initiated sign-in flow. This should be called when the user
         * clicks on a "Sign In" button. As a result, authentication/consent dialogs
         * may show up. At the end of the process, the GameHelperListener's
         * OnSignInSucceeded() or OnSignInFailed() methods will be called.
         */

        public void beginUserInitiatedSignIn()
        {
            debugLog("beginUserInitiatedSignIn: resetting attempt count.");
            resetSignInCancellations();
            mSignInCancelled = false;
            mConnectOnStart = true;

            if (mGoogleApiClient.IsConnected)
            {
                // nothing to do
                logWarn("beginUserInitiatedSignIn() called when already connected. "
                        + "Calling listener directly to notify of success.");
                notifyListener(true);
                return;
            }
            if (mConnecting)
            {
                logWarn("beginUserInitiatedSignIn() called when already connecting. "
                        + "Be patient! You can only call this method after you get an "
                        + "OnSignInSucceeded() or OnSignInFailed() callback. Suggestion: disable "
                        + "the sign-in button on startup and also when it's clicked, and re-enable "
                        + "when you get the callback.");
                // ignore call (listener will get a callback when the connection
                // process finishes)
                return;
            }

            debugLog("Starting USER-INITIATED sign-in flow.");

            // indicate that user is actively trying to sign in (so we know to
            // resolve
            // connection problems by showing dialogs)
            mUserInitiatedSignIn = true;

            if (mConnectionResult != null)
            {
                // We have a pending connection result from a previous failure, so
                // start with that.
                debugLog("beginUserInitiatedSignIn: continuing pending sign-in flow.");
                mConnecting = true;
                resolveConnectionResult();
            }
            else
            {
                // We don't have a pending connection result, so start anew.
                debugLog("beginUserInitiatedSignIn: starting new sign-in flow.");
                mConnecting = true;
                connect();
            }
        }

        private void connect()
        {
            if (mGoogleApiClient.IsConnected)
            {
                debugLog("Already connected.");
                return;
            }
            debugLog("Starting connection.");
            mConnecting = true;
            mInvitation = null;
            mTurnBasedMatch = null;
            mGoogleApiClient.Connect();
        }

        /**
         * Disconnects the API client, then connects again.
         */

        public void reconnectClient()
        {
            if (!mGoogleApiClient.IsConnected)
            {
                Log.Warn(TAG, "reconnectClient() called when client is not connected.");
                // interpret it as a request to connect
                connect();
            }
            else
            {
                debugLog("Reconnecting client.");
                mGoogleApiClient.Reconnect();
            }
        }

        /** Called when we successfully obtain a connection to a client. */

        private void succeedSignIn()
        {
            debugLog("succeedSignIn");
            mSignInFailureReason = null;
            mConnectOnStart = true;
            mUserInitiatedSignIn = false;
            mConnecting = false;
            notifyListener(true);
        }

        // Return the number of times the user has cancelled the sign-in flow in the
        // life of the app
        private int getSignInCancellations()
        {
            ISharedPreferences sp = mAppContext.GetSharedPreferences(GAMEHELPER_SHARED_PREFS, FileCreationMode.Private);
            return sp.GetInt(KEY_SIGN_IN_CANCELLATIONS, 0);
        }

        // Increments the counter that indicates how many times the user has
        // cancelled the sign in
        // flow in the life of the application
        private int incrementSignInCancellations()
        {
            int cancellations = getSignInCancellations();
            ISharedPreferencesEditor editor =
                mAppContext.GetSharedPreferences(GAMEHELPER_SHARED_PREFS, FileCreationMode.Private).Edit();
            editor.PutInt(KEY_SIGN_IN_CANCELLATIONS, cancellations + 1);
            editor.Commit();
            return cancellations + 1;
        }

        // Reset the counter of how many times the user has cancelled the sign-in
        // flow.
        private void resetSignInCancellations()
        {
            ISharedPreferencesEditor editor =
                mAppContext.GetSharedPreferences(GAMEHELPER_SHARED_PREFS, FileCreationMode.Private).Edit();
            editor.PutInt(KEY_SIGN_IN_CANCELLATIONS, 0);
            editor.Commit();
        }

        /** Handles a connection failure. */

        /**
         * Attempts to resolve a connection failure. This will usually involve
         * starting a UI flow that lets the user give the appropriate consents
         * necessary for sign-in to work.
         */

        private void resolveConnectionResult()
        {
            try
            {
                // Try to resolve the problem
                if (mExpectingResolution)
                {
                    debugLog("We're already expecting the result of a previous resolution.");
                    return;
                }

                debugLog("resolveConnectionResult: trying to resolve result: "
                         + mConnectionResult);
                if (mConnectionResult.HasResolution)
                {
                    // This problem can be fixed. So let's try to fix it.
                    debugLog("Result has resolution. Starting it.");
                    try
                    {
                        // launch appropriate UI flow (which might, for example, be the
                        // sign-in flow)
                        mExpectingResolution = true;
                        mConnectionResult.StartResolutionForResult(mActivity, RC_RESOLVE);
                    }
                    catch (IntentSender.SendIntentException e)
                    {
                        e.ToString();
                        // Try connecting again
                        debugLog("SendIntentException, so connecting again.");
                        connect();
                    }
                }
                else
                {
                    // It's not a problem what we can solve, so give up and show an
                    // error.
                    debugLog("resolveConnectionResult: result has no resolution. Giving up.");
                    giveUp(new SignInFailureReason(mConnectionResult.ErrorCode));
                }
            }
            catch (Exception ex)
            {
                ex.ToString();
            }
        }

        public void disconnect()
        {
            if (mGoogleApiClient.IsConnected)
            {
                debugLog("Disconnecting client.");
                mGoogleApiClient.Disconnect();
            }
            else
            {
                Log.Warn(TAG, "disconnect() called when client was already disconnected.");
            }
        }

        /**
         * Give up on signing in due to an error. Shows the appropriate error
         * message to the user, using a standard error dialog as appropriate to the
         * cause of the error. That dialog will indicate to the user how the problem
         * can be solved (for example, re-enable Google Play Services, upgrade to a
         * new version, etc).
         */

        private void giveUp(SignInFailureReason reason)
        {
            try
            {
                mConnectOnStart = false;
                disconnect();
                mSignInFailureReason = reason;

                if (reason.mActivityResultCode == GamesActivityResultCodes.ResultAppMisconfigured)
                {
                    // print debug info for the developer
                    GameHelperUtils.printMisconfiguredDebugInfo(mAppContext);
                }

                showFailureDialog();
                mConnecting = false;
                notifyListener(false);
            }
            catch (Exception ex)
            {
                ex.ToString();
            }
        }

        /** Called when we are disconnected from the Google API client. */

        public void showFailureDialog()
        {
            if (mSignInFailureReason != null)
            {
                int errorCode = mSignInFailureReason.getServiceErrorCode();
                int actResp = mSignInFailureReason.getActivityResultCode();

                if (mShowErrorDialogs)
                {
                    showFailureDialog(mActivity, actResp, errorCode);
                }
                else
                {
                    debugLog("Not showing error dialog because mShowErrorDialogs==false. "
                             + "" + "Error was: " + mSignInFailureReason);
                }
            }
        }

        /** Shows an error dialog that's appropriate for the failure reason. */

        public static void showFailureDialog(Activity activity, int actResp, int errorCode)
        {
            if (activity == null)
            {
                Log.Error("GameHelper", "*** No Activity. Can't show failure dialog!");
                return;
            }
            Dialog errorDialog = null;

            switch (actResp)
            {
                case GamesActivityResultCodes.ResultAppMisconfigured:
                    errorDialog = makeSimpleDialog(activity, GameHelperUtils.getString(
                        activity, GameHelperUtils.R_APP_MISCONFIGURED));
                    break;
                case GamesActivityResultCodes.ResultSignInFailed:
                    errorDialog = makeSimpleDialog(activity, GameHelperUtils.getString(
                        activity, GameHelperUtils.R_SIGN_IN_FAILED));
                    break;
                case GamesActivityResultCodes.ResultLicenseFailed:
                    errorDialog = makeSimpleDialog(activity, GameHelperUtils.getString(
                        activity, GameHelperUtils.R_LICENSE_FAILED));
                    break;
                default:
                    // No meaningful Activity response code, so generate default Google
                    // Play services dialog
                    errorDialog = GooglePlayServicesUtil.GetErrorDialog(errorCode, activity, RC_UNUSED, null);
                    if (errorDialog == null)
                    {
                        // get fallback dialog
                        Log.Error("GameHelper", "No standard error dialog available. Making fallback dialog.");
                        errorDialog = makeSimpleDialog(activity,
                            GameHelperUtils.getString(activity, GameHelperUtils.R_UNKNOWN_ERROR) + " " +
                            GameHelperUtils.errorCodeToString(errorCode));
                    }
                    break;
            }

            errorDialog.Show();
        }

        public static Dialog makeSimpleDialog(Activity activity, String text)
        {
            return
                (new AlertDialog.Builder(activity)).SetMessage(text)
                    .SetNeutralButton(Android.Resource.String.Ok, (null as EventHandler<DialogClickEventArgs>))
                    .Create();
        }

        public static Dialog makeSimpleDialog(Activity activity, String title, String text)
        {
            return
                (new AlertDialog.Builder(activity)).SetMessage(text)
                    .SetTitle(title)
                    .SetNeutralButton(Android.Resource.String.Ok, (null as EventHandler<DialogClickEventArgs>))
                    .Create();
        }

        public Dialog makeSimpleDialog(String text)
        {
            if (mActivity == null)
            {
                logError("*** makeSimpleDialog failed: no current Activity!");
                return null;
            }
            return makeSimpleDialog(mActivity, text);
        }

        public Dialog makeSimpleDialog(String title, String text)
        {
            if (mActivity == null)
            {
                logError("*** makeSimpleDialog failed: no current Activity!");
                return null;
            }
            return makeSimpleDialog(mActivity, title, text);
        }

        private void debugLog(String message)
        {
            if (mDebugLog)
            {
                Log.Debug(TAG, "GameHelper: " + message);
            }
        }

        private void logWarn(String message)
        {
            Log.Warn(TAG, "!!! GameHelper WARNING: " + message);
        }

        private void logError(String message)
        {
            Log.Error(TAG, "*** GameHelper ERROR: " + message);
        }

        // Not recommended for general use. This method forces the
        // "connect on start" flag
        // to a given state. This may be useful when using GameHelper in a
        // non-standard
        // sign-in flow.
        public void setConnectOnStart(bool connectOnStart)
        {
            debugLog("Forcing mConnectOnStart=" + connectOnStart);
            mConnectOnStart = connectOnStart;
        }
    }
}