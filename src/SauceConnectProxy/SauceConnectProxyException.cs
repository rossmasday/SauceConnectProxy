using System;
using System.Runtime.Serialization;

namespace SauceConnectProxy
{
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

        public SauceConnectProxyException(string message, Exception inner) 
            : base(message, inner)
        {
        }

        // A constructor is needed for serialization when an
        // exception propagates from a remoting server to the client. 
        protected SauceConnectProxyException(SerializationInfo info, StreamingContext context)
        {
        }
    }
}
