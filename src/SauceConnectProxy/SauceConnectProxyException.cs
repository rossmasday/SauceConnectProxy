namespace SauceConnectProxy
{
    using System;
    using System.Runtime.Serialization;

    [Serializable]
    public class SauceConnectProxyException : Exception
    {
        public SauceConnectProxyException()
        {
        }

        public SauceConnectProxyException(string message)
            : base($"{Environment.NewLine}{message}")
        {
        }

        public SauceConnectProxyException(string message, Exception innerException) 
            : base(message, innerException)
        {
        }

        // A constructor is needed for serialization when an
        // exception propagates from a remoting server to the client. 
        protected SauceConnectProxyException(SerializationInfo info, StreamingContext context)
        {
        }
    }
}