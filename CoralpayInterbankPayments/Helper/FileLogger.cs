namespace CoralpayInterbankPayments.Helper
{
    public static class FileLogger
    {
        private static readonly string logFilePath = @"C:\Logs\ErrorLog.txt";

        public static void Log(Exception ex)
        {
            try
            {
                string errorMessage = $"[{DateTime.Now}] {ex.Message}{Environment.NewLine}{ex.StackTrace}{Environment.NewLine}";

                var logDir = Path.GetDirectoryName(logFilePath);
                if (!string.IsNullOrEmpty(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }

                File.AppendAllText(logFilePath, errorMessage);
            }
            catch
            {
                Console.WriteLine("Failed to log error");
            }
        }

        public static void Log(string message)
        {
            try
            {
                string logMessage = $"[{DateTime.Now}] {message}{Environment.NewLine}";

                var logDir = Path.GetDirectoryName(logFilePath);
                if (!string.IsNullOrEmpty(logDir))
                {
                    Directory.CreateDirectory(logDir);
                }
                File.AppendAllText(logFilePath, logMessage);
            }
            catch
            {
                Console.WriteLine("Failed to log message");
            }
        }
    }
}
