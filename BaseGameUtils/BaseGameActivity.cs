using Android.App;
using Android.Content;
using Android.Gms.Common.Apis;
using Android.OS;
using Android.Support.V4.App;
using Android.Util;
using Java.Lang;
using Exception = System.Exception;
using String = System.String;

namespace BaseGameUtils
{
    [Activity(Label = "BaseGameActivity")]
    /**
     * Example base class for games. This implementation takes care of setting up
     * the API client object and managing its lifecycle. Subclasses only need to
     * override the @link{#OnSignInSucceeded} and @link{#OnSignInFailed} abstract
     * methods. To initiate the sign-in flow when the user clicks the sign-in
     * button, subclasses should call @link{#beginUserInitiatedSignIn}. By default,
     * this class only instantiates the GoogleApiClient object. If the PlusClient or
     * AppStateClient objects are also wanted, call the BaseGameActivity(int)
     * constructor and specify the requested clients. For example, to request
     * PlusClient and GamesClient, use BaseGameActivity(CLIENT_GAMES | CLIENT_PLUS).
     * To request all available clients, use BaseGameActivity(CLIENT_ALL).
     * Alternatively, you can also specify the requested clients via
     * @link{#setRequestedClients}, but you must do so before @link{#onCreate}
     * gets called, otherwise the call will have no effect.
     *
     * @author Bruno Oliveira (Google)
     */
    public abstract class BaseGameActivity : FragmentActivity
    {
        // The game helper object. This class is mainly a wrapper around this object.
        public static GameHelper mHelper;

        // We expose these constants here because we don't want users of this class
        // to have to know about GameHelper at all.
        public static int CLIENT_GAMES = GameHelper.CLIENT_GAMES;
        public static int CLIENT_APPSTATE = GameHelper.CLIENT_APPSTATE;
        public static int CLIENT_PLUS = GameHelper.CLIENT_PLUS;
        public static int CLIENT_ALL = GameHelper.CLIENT_ALL;

        // Requested clients. By default, that's just the games client.

        private static String TAG = "BaseGameActivity";
        protected bool mDebugLog = false;
        protected int mRequestedClients = CLIENT_GAMES;

        /** Constructs a BaseGameActivity with default client (GamesClient). */

        protected BaseGameActivity()
        {
        }

        /**
     * Constructs a BaseGameActivity with the requested clients.
     * @param requestedClients The requested clients (a combination of CLIENT_GAMES,
     *         CLIENT_PLUS and CLIENT_APPSTATE).
     */

        protected BaseGameActivity(int requestedClients)
        {
            setRequestedClients(requestedClients);
        }

        /**
     * Sets the requested clients. The preferred way to set the requested clients is
     * via the constructor, but this method is available if for some reason your code
     * cannot do this in the constructor. This must be called before onCreate or getGameHelper()
     * in order to have any effect. If called after onCreate()/getGameHelper(), this method
     * is a no-op.
     *
     * @param requestedClients A combination of the flags CLIENT_GAMES, CLIENT_PLUS
     *         and CLIENT_APPSTATE, or CLIENT_ALL to request all available clients.
     */

        protected void setRequestedClients(int requestedClients)
        {
            mRequestedClients = requestedClients;
        }

        public GameHelper getGameHelper()
        {
            try
            {

                if (mHelper == null)
                {
                    mHelper = new GameHelper(this, mRequestedClients);
                    mHelper.enableDebugLog(mDebugLog);
                }
            }
            catch (Exception ex)
            {
                ex.ToString();
            }
            return mHelper;
        }

        protected override void OnCreate(Bundle b)
        {
            try
            {
                base.OnCreate(b);
                if (mHelper == null)
                {
                    getGameHelper();
                }
                mHelper.setup(this as IGameHelperListener);
            }
            catch (Exception ex)
            {
                ex.ToString();
            }
        }

        protected override void OnStart()
        {
            try
            {
                base.OnStart();
                mHelper.onStart(this);
            }
            catch (Exception ex)
            {
                ex.ToString();
            }
        }

        protected override void OnStop()
        {
            base.OnStop();
            mHelper.onStop();
        }

        protected override void OnActivityResult(int request, Result response, Intent data)
        {
            base.OnActivityResult(request, response, data);
            mHelper.onActivityResult(request, (int)response, data);
        }

        protected IGoogleApiClient getApiClient()
        {
            return mHelper.getApiClient();
        }

        protected bool isSignedIn()
        {
            return mHelper.isSignedIn();
        }

        protected void beginUserInitiatedSignIn()
        {
            mHelper.beginUserInitiatedSignIn();
        }

        protected void signOut()
        {
            mHelper.signOut();
        }

        protected void showAlert(String message)
        {
            mHelper.makeSimpleDialog(message).Show();
        }

        protected void showAlert(String title, String message)
        {
            mHelper.makeSimpleDialog(title, message).Show();
        }

        protected void enableDebugLog(bool enabled)
        {
            mDebugLog = true;
            if (mHelper != null)
            {
                mHelper.enableDebugLog(enabled);
            }
        }

        [Deprecated]
        protected void enableDebugLog(bool enabled, String tag)
        {
            Log.Warn(TAG, "BaseGameActivity.enabledDebugLog(bool,String) is " +
                          "deprecated. Use enableDebugLog(bool)");
            enableDebugLog(enabled);
        }

        protected String getInvitationId()
        {
            return mHelper.getInvitationId();
        }

        protected void reconnectClient()
        {
            mHelper.reconnectClient();
        }

        protected bool hasSignInError()
        {
            return mHelper.hasSignInError();
        }

        protected SignInFailureReason getSignInError()
        {
            return mHelper.getSignInError();
        }
    }
}