namespace BaseGameUtils
{
    public interface IGameHelperListener
    {
        /**
         * Called when sign-in fails. As a result, a "Sign-In" button can be
         * shown to the user; when that button is clicked, call
         *
         * @link{GamesHelper#beginUserInitiatedSignIn . Note that not all calls
         *                                            to this method mean an
         *                                            error; it may be a result
         *                                            of the fact that automatic
         *                                            sign-in could not proceed
         *                                            because user interaction
         *                                            was required (consent
         *                                            dialogs). So
         *                                            implementations of this
         *                                            method should NOT display
         *                                            an error message unless a
         *                                            call to @link{GamesHelper#
         *                                            hasSignInError} indicates
         *                                            that an error indeed
         *                                            occurred.
         */
        void OnSignInFailed();

        /** Called when sign-in succeeds. */
        void OnSignInSucceeded();
    }
}