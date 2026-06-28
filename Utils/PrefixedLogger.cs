namespace ModsCommon.Utils {
    #region Using Statements

    using Colossal.Logging;

    #endregion

    public class PrefixedLogger {
        private readonly ILog m_Log;
        public string Prefix { get; set; }

        /// <summary>
        /// Gets or sets the default <see cref="ILog"/> used when a <see cref="PrefixedLogger"/> is
        /// created without an explicit logger. Set once during mod startup (see
        /// <c>ModsCommonBase.OnLoad</c>) so systems can keep using <c>new PrefixedLogger(nameof(X))</c>.
        /// </summary>
        public static ILog DefaultLog { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PrefixedLogger"/> class.
        /// </summary>
        /// <param name="prefix">The prefix to prepend to log messages.</param>
        /// <param name="log">The underlying logger instance.</param>
        public PrefixedLogger(string prefix, ILog log) {
            Prefix = prefix;
            m_Log  = log;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PrefixedLogger"/> class using
        /// <see cref="DefaultLog"/> as the underlying logger.
        /// </summary>
        /// <param name="prefix">The prefix to prepend to log messages.</param>
        public PrefixedLogger(string prefix) : this(prefix, DefaultLog) {
        }

        public void Info(string message) { Log("INFO", message); }

        public void Warn(string message) { Log("WARN", message); }

        public void Error(string message) { Log("ERROR", message); }

        public void Debug(string message) { Log("DEBUG", message); }

        private void Log(string level, string message) {
            var formattedMessage = $"[{Prefix}] {message}";

            switch (level) {
                case "ERROR":
                    m_Log.Error(formattedMessage);
                    break;
                case "WARN":
                    m_Log.Warn(formattedMessage);
                    break;
                case "DEBUG":
                    m_Log.Debug(formattedMessage);
                    break;
                case "INFO":
                default:
                    m_Log.Info(formattedMessage);
                    break;
            }
        }
    }
}
