namespace CoralpayInterbankPayments.Model
{
    public static class CoralPayResponseCodes
    {
        public const string Success = "00";
        public const string Ready = "10";
        public const string Pending = "07";
        public const string SystemMalfunction = "96";
        public const string Timeout = "97";
        public const string InvalidPayload = "05";
        public const string UnauthorizedRequest = "90";
        public const string InvalidAccount = "02";
        public const string CannotResolveAccount = "03";
        public const string FailedGeneric = "01";
        public const string BankAccountRestricted = "05";
        public const string InvalidTransaction = "12";
        public const string InvalidAmount = "13";
        public const string AccountNameMismatch = "14";
        public const string InvalidNameEnquiryRef = "39";
        public const string InsufficientFunds = "51";
        public const string NotPermittedChannel = "57";
        public const string CreditLimitExceeded = "61";
        public const string DuplicateTransaction = "94";
        public const string TransactionNotFound = "25";
        public const string UnknownError = "99";
    }
}
