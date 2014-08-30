using System;

namespace BaseGameUtils
{
    public class SignInFailureReason
    {
        public static int NO_ACTIVITY_RESULT_CODE = -100;
        private readonly int mServiceErrorCode;
        public int mActivityResultCode = NO_ACTIVITY_RESULT_CODE;

        public SignInFailureReason(int serviceErrorCode, int activityResultCode)
        {
            mServiceErrorCode = serviceErrorCode;
            mActivityResultCode = activityResultCode;
        }

        public SignInFailureReason(int serviceErrorCode) : this(serviceErrorCode, NO_ACTIVITY_RESULT_CODE)
        {
        }

        public int getServiceErrorCode()
        {
            return mServiceErrorCode;
        }

        public int getActivityResultCode()
        {
            return mActivityResultCode;
        }

        public override String ToString()
        {
            return "SignInFailureReason(serviceErrorCode:"
                   + GameHelperUtils.errorCodeToString(mServiceErrorCode)
                   + ((mActivityResultCode == NO_ACTIVITY_RESULT_CODE)
                       ? ")"
                       : (",activityResultCode:"
                          + GameHelperUtils.activityResponseCodeToString(mActivityResultCode) + ")"));
        }
    }
}