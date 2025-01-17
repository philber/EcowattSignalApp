﻿namespace E3Collector
{
    using System;
    using System.Runtime.Serialization;

    [DataContract]
    public class AuthToken
    {
        #region Properties

        [DataMember(Name = "access_token")]
        public string AccessToken { get; set; }

        [DataMember(Name = "token_type")]
        public string TokenType { get; set; }

        [DataMember(Name = "expires_in")]
        public string ExpiresIn { get; set; }

        #endregion Properties
    }
}
