using System;
using System.IO;
using System.Net;
using System.Collections.Generic;
using System.Collections.Specialized;
using Toopher;
using NUnit.Framework;
using SimpleJson;

namespace ToopherDotNetTests
{
	[TestFixture()]
	public class Test
	{
		class MockWebResponse : WebResponse, IHttpWebResponse
		{
			HttpStatusCode errorCode;
			Stream responseStream;

			public MockWebResponse (HttpStatusCode errorCode, string responseBody)
			{
				this.errorCode = errorCode;
				this.responseStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(responseBody));
			}

			override public Stream GetResponseStream ()
			{
				return responseStream;
			}

			public HttpStatusCode StatusCode
			{
				get { return errorCode; }
			}

			public string StatusDescription
			{
				get { return ""; }
			}
		}
		WebException makeError (HttpStatusCode errorCode, string responseBody)
		{
			WebResponse response = new MockWebResponse (errorCode, responseBody);
			return new WebException ("", null, WebExceptionStatus.ProtocolError, response);
		}
		class WebClientMock : WebClientProxy
		{
			static public String LastRequestMethod { get; set; }
			static public NameValueCollection LastRequestData { get; set; }
			static public Exception ReturnException { get; set; }
			static public string ReturnValue { get; set; }
			string doit ()
			{
				if (ReturnException != null) {
					throw ReturnException;
				} else {
					return ReturnValue;
				}
			}

			override public byte[] UploadValues(string requestUri, string method, NameValueCollection parameters) {
				LastRequestMethod = "POST";
				LastRequestData = parameters;
				return System.Text.Encoding.UTF8.GetBytes (doit());
			}
			override public string DownloadString (string requestUri)
			{
				LastRequestMethod = "GET";
				LastRequestData = this.QueryString;
				return doit();
			}
		}

		[SetUp]
		public void Init ()
		{
			WebClientMock.ReturnException = null;
			WebClientMock.ReturnValue = null;
			WebClientMock.LastRequestMethod = null;
			WebClientMock.LastRequestData = null;
		}

		private ToopherApi getApi ()
		{
			return new ToopherApi ("key", "secret", null, typeof (WebClientMock));
		}

		[Test()]
		public void ToopherVersionTest ()
		{
			string[] strs = ToopherApi.VERSION.Split('.');
			int major = int.Parse(strs[0]);
			int minor = int.Parse(strs[1]);
			int patchLevel = int.Parse(strs[2]);
			Assert.IsTrue(major >= 1);
			Assert.IsTrue(minor >= 0);
			Assert.IsTrue(patchLevel >= 0);
		}

		[Test()]
		public void ToopherBaseUrlTest ()
		{
			StringAssert.Contains ("https", ToopherApi.DEFAULT_BASE_URL);
			Assert.IsTrue(System.Uri.IsWellFormedUriString(ToopherApi.DEFAULT_BASE_URL, System.UriKind.Absolute));
		}

		[Test]
		public void CreatePairingWithPhraseTest ()
		{
			var api = getApi();
			WebClientMock.ReturnValue = @"{""id"":""1"", ""pending"":false, ""enabled"":true, ""user"":{""id"":""1"",""name"":""some user"", ""toopher_authentication_enabled"":true}}";
			Pairing pairing = api.Pair ("some user", "awkward turtle");
			Assert.AreEqual (WebClientMock.LastRequestMethod, "POST");
			Assert.AreEqual (WebClientMock.LastRequestData["pairing_phrase"], "awkward turtle");
			Assert.AreEqual (WebClientMock.LastRequestData["user_name"], "some user");
			Assert.AreEqual (pairing.id, "1");
		}

		[Test]
		public void CreateSmsPairingTest ()
		{
			var api = getApi();
			WebClientMock.ReturnValue = @"{""id"":""1"", ""pending"":false, ""enabled"":true, ""user"":{""id"":""1"",""name"":""some user"", ""toopher_authentication_enabled"":true}}";
			Pairing pairing = api.Pair ("some user", "555-555-5555");
			Assert.AreEqual (WebClientMock.LastRequestMethod, "POST");
			Assert.AreEqual (WebClientMock.LastRequestData["phone_number"], "555-555-5555");
			Assert.AreEqual (WebClientMock.LastRequestData["user_name"], "some user");
			Assert.AreEqual (pairing.id, "1");
		}

		[Test]
		public void CreateQrPairingTest ()
		{
			var api = getApi();
			WebClientMock.ReturnValue = @"{""id"":""1"", ""pending"":false, ""enabled"":true, ""user"":{""id"":""1"",""name"":""some user"", ""toopher_authentication_enabled"":true}}";
			Pairing pairing = api.Pair ("some user");
			Assert.AreEqual (WebClientMock.LastRequestMethod, "POST");
			Assert.AreEqual (WebClientMock.LastRequestData["user_name"], "some user");
			Assert.AreEqual (pairing.id, "1");
		}

		[Test]
		public void AuthenticateWithPairingIdTest ()
		{
			var api = getApi ();
			string pairingId = Guid.NewGuid().ToString();
			WebClientMock.ReturnValue = @"{""id"":""" + pairingId + @""", ""pending"":false, ""granted"":true, ""automated"":false, ""reason"":""its a test"", ""terminal"":{""id"":""1"", ""name"":""test terminal""}}";
			AuthenticationRequest auth = api.Authenticate(pairingId, "test terminal");
			Assert.AreEqual(WebClientMock.LastRequestMethod, "POST");
			Assert.AreEqual (WebClientMock.LastRequestData["pairing_id"], pairingId);
			Assert.AreEqual (WebClientMock.LastRequestData["terminal_name"], "test terminal");
			Assert.AreEqual (auth.id, pairingId);
		}

		[Test]
		public void AuthenticateWithUsernameAndExtrasTest ()
		{
			var api = getApi ();
			WebClientMock.ReturnValue = @"{""id"":""1"", ""pending"":false, ""granted"":true, ""automated"":false, ""reason"":""its a test"", ""terminal"":{""id"":""1"", ""name"":""test terminal""}}";
			AuthenticationRequest auth = api.Authenticate ("some other user", "requester specified id", extras: new Dictionary<String, String>() {{ "random_key" , "42" }});
			Assert.AreEqual (WebClientMock.LastRequestMethod, "POST");
			Assert.AreEqual (WebClientMock.LastRequestData["user_name"], "some other user");
			Assert.AreEqual (WebClientMock.LastRequestData["terminal_name_extra"], "requester specified id");
			Assert.AreEqual (WebClientMock.LastRequestData["random_key"], "42");
			Assert.AreEqual (auth.id, "1");
		}

		[Test]
		public void ArbitraryParametersOnPairTest ()
		{
			var api = getApi ();
			WebClientMock.ReturnValue = @"{""id"":""1"", ""pending"":false, ""enabled"":true, ""user"":{""id"":""1"",""name"":""some user"", ""toopher_authentication_enabled"":true}}";
			Pairing pairing = api.Pair ("awkward turtle", "some user", extras: new Dictionary<string,string>(){{"test_param", "42"}});
			Assert.AreEqual (WebClientMock.LastRequestMethod, "POST");
			Assert.AreEqual (WebClientMock.LastRequestData["test_param"], "42");
			Assert.AreEqual (pairing.id, "1");
		}

		[Test]
		public void ArbitraryParamtersOnAuthenticateTest ()
		{
			var api = getApi ();
			WebClientMock.ReturnValue = @"{""id"":""1"", ""pending"":false, ""granted"":true, ""automated"":false, ""reason"":""its a test"", ""terminal"":{""id"":""1"", ""name"":""test terminal""}}";
			AuthenticationRequest auth = api.Authenticate ("1", "test terminal", extras: new Dictionary<string, string> () { { "test_param", "42" } });
			Assert.AreEqual (WebClientMock.LastRequestMethod, "POST");
			Assert.AreEqual (WebClientMock.LastRequestData["test_param"], "42");
			Assert.AreEqual (auth.id, "1");
		}

		[Test]
		public void AccessArbitraryKeysInAdvancedPairingsGetByIdTest ()
		{
			var api = getApi ();
			WebClientMock.ReturnValue = @"{""id"":""1"", ""pending"":false, ""enabled"":true, ""user"":{""id"":""1"",""name"":""some user"", ""toopher_authentication_enabled"":true}, ""random_key"":""84""}";
			Pairing pairing = api.advanced.pairings.GetById  ("1");
			Assert.AreEqual (WebClientMock.LastRequestMethod, "GET");
			Assert.AreEqual (pairing.id, "1");
			Assert.AreEqual (pairing.user.name, "some user");
			Assert.AreEqual (pairing.user.id, "1");
			Assert.IsTrue (pairing.enabled);
			Assert.AreEqual (pairing["random_key"], "84");
		}

		[Test]
		public void AccessArbitraryKeysInAdvancedAuthenticationRequestsGetByIdTest ()
		{
			var api = getApi ();
			WebClientMock.ReturnValue = @"{""id"":""1"", ""pending"":false, ""granted"":true, ""automated"":false, ""reason"":""its a test"", ""terminal"":{""id"":""1"", ""name"":""test terminal""}, ""random_key"":""84""}";
			AuthenticationRequest auth = api.advanced.authenticationRequests.GetById ("1");
			Assert.AreEqual (WebClientMock.LastRequestMethod, "GET");
			Assert.AreEqual (auth.id, "1");
			Assert.IsFalse (auth.pending);
			Assert.IsTrue (auth.granted);
			Assert.AreEqual (auth.reason, "its a test");
			Assert.AreEqual (auth.terminalId, "1");
			Assert.AreEqual (auth.terminalName, "test terminal");
			Assert.AreEqual (auth["random_key"], "84");
		}

		[Test]
		public void AccessArbitraryKeysInAdvancedUsersGetByIdTest ()
		{
			var api = getApi ();
			WebClientMock.ReturnValue = @"{""id"":""1"", ""name"":""username"", ""toopher_authentication_enabled"":true, ""random_key"":""84""}";
			User user = api.advanced.users.GetById  ("1");
			Assert.AreEqual (WebClientMock.LastRequestMethod, "GET");
			Assert.AreEqual (user.id, "1");
			Assert.AreEqual (user.name, "username");
			Assert.IsTrue (user.toopherAuthenticationEnabled);
			Assert.AreEqual (user["random_key"], "84");
		}

		[Test]
		[ExpectedException(typeof(UserDisabledError))]
		public void DisabledUserRaisesCorrectErrorTest ()
		{
			var api = getApi ();
			WebClientMock.ReturnException = makeError((HttpStatusCode)409,
				@"{""error_code"":704, ""error_message"":""The specified user has disabled Toopher authentication.""}");
			api.Authenticate ("some disabled user", "some random string");
		}

		[Test]
		[ExpectedException (typeof (UserUnknownError))]
		public void UnknownUserRaisesCorrectErrorTest ()
		{
			var api = getApi ();
			WebClientMock.ReturnException = makeError ((HttpStatusCode)409,
				@"{""error_code"":705, ""error_message"":""No matching user exists.""}");
			api.Authenticate ("some unknown user", "some random string");
		}

		[Test]
		[ExpectedException (typeof (TerminalUnknownError))]
		public void UnknownTerminalRaisesCorrectErrorTest ()
		{
			var api = getApi ();
			WebClientMock.ReturnException = makeError ((HttpStatusCode)409,
				@"{""error_code"":706, ""error_message"":""No matching terminal exists.""}");
			api.Authenticate ("some unknown user", "some random string");
		}

		[Test]
		[ExpectedException (typeof (PairingDeactivatedError))]
		public void DeactivatedPairingRaisesCorrectErrorTest ()
		{
			var api = getApi ();
			WebClientMock.ReturnException = makeError ((HttpStatusCode)409,
				@"{""error_code"":601, ""error_message"":""This pairing has been deactivated.""}");
			api.Authenticate ("some disabled user", "some random string");
		}

		[Test]
		[ExpectedException (typeof (PairingDeactivatedError))]
		public void UnauthorizedPairingRaisesCorrectErrorTest ()
		{
			var api = getApi ();
			WebClientMock.ReturnException = makeError ((HttpStatusCode)409,
				@"{""error_code"":601, ""error_message"":""This pairing has not been authorized to authenticate.""}");
			api.Authenticate ("some unauthorized user", "some random string");
		}

		[Test]
		public void AdvancedAuthenticationRequestsGetByIdTest ()
		{
			var api = getApi ();
			WebClientMock.ReturnValue = @"{""id"":""1"", ""pending"":false, ""granted"":true, ""automated"":false, ""reason"":""its a test"", ""terminal"":{""id"":""1"", ""name"":""test terminal""}}";
			AuthenticationRequest auth = api.advanced.authenticationRequests.GetById ("1");
			Assert.AreEqual (WebClientMock.LastRequestMethod, "GET");
			Assert.AreEqual (auth.id, "1");
			Assert.IsFalse (auth.pending);
			Assert.IsTrue (auth.granted);
			Assert.AreEqual (auth.reason, "its a test");
			Assert.AreEqual (auth.terminalId, "1");
			Assert.AreEqual (auth.terminalName, "test terminal");
		}

		[Test]
		public void GetAuthenticationRequestWIthOtpTest ()
		{
			var api = getApi ();
			WebClientMock.ReturnValue = @"{""id"":""1"", ""pending"":false, ""granted"":true, ""automated"":false, ""reason"":""its a test"", ""terminal"":{""id"":""1"", ""name"":""test terminal""}}";
			AuthenticationRequest auth = api.GetAuthenticationRequest ("1", otp: "123456");
			Assert.AreEqual (WebClientMock.LastRequestMethod, "POST");
			Assert.AreEqual (WebClientMock.LastRequestData["otp"], "123456");
			Assert.AreEqual (auth.id, "1");
		}


		[Test]
		public void AdvancedPairingsGetByIdTest ()
		{
			var api = getApi ();
			WebClientMock.ReturnValue = @"{""id"":""1"", ""pending"":false, ""enabled"":true, ""user"":{""id"":""1"",""name"":""some user"", ""toopher_authentication_enabled"":true}}";
			Pairing pairing = api.advanced.pairings.GetById ("1");
			Assert.IsInstanceOf<Pairing> (pairing);
			Assert.AreEqual (WebClientMock.LastRequestMethod, "GET");
			Assert.AreEqual (pairing.id, "1");
			Assert.AreEqual (pairing.user.name, "some user");
			Assert.AreEqual (pairing.user.id, "1");
			Assert.IsTrue (pairing.enabled);
		}

		[Test]
		public void AdvancedUsersGetByIdTest ()
		{
			var api = getApi ();
			WebClientMock.ReturnValue = @"{""id"":""1"", ""name"":""username"", ""toopher_authentication_enabled"":true}";
			User user = api.advanced.users.GetById ("1");
			Assert.IsInstanceOf<User> (user);
			Assert.AreEqual (WebClientMock.LastRequestMethod, "GET");
			Assert.AreEqual (user.id, "1");
			Assert.AreEqual (user.name, "username");
			Assert.IsTrue (user.toopherAuthenticationEnabled);
		}

		[Test]
		public void AdvancedUsersCreateTest ()
		{
			var api = getApi ();
			WebClientMock.ReturnValue = @"{""id"":""1"", ""name"":""username"", ""toopher_authentication_enabled"":true}";
			User user = api.advanced.users.Create ("username");
			Assert.IsInstanceOf<User> (user);
			Assert.AreEqual (WebClientMock.LastRequestMethod, "POST");
			Assert.AreEqual (user.id, "1");
			Assert.AreEqual (user.name, "username");
			Assert.IsTrue (user.toopherAuthenticationEnabled);
		}

		[Test]
		public void AdvancedUsersCreateWithParamsTest ()
		{
			var api = getApi();
			WebClientMock.ReturnValue = @"{""id"":""1"", ""name"":""username"", ""toopher_authentication_enabled"":true}";
			NameValueCollection parameters = new NameValueCollection();
			parameters.Add ("foo", "bar");
			User user = api.advanced.users.Create ("username", parameters);
			Assert.IsInstanceOf<User> (user);
			Assert.AreEqual (WebClientMock.LastRequestMethod, "POST");
			Assert.AreEqual (WebClientMock.LastRequestData["foo"], "bar");
			Assert.AreEqual (user.id, "1");
			Assert.AreEqual (user.name, "username");
			Assert.IsTrue (user.toopherAuthenticationEnabled);
		}

		[Test]
		public void AdvancedUserTerminalsGetByIdTest ()
		{
			var api = getApi ();
			WebClientMock.ReturnValue = @"{""id"":""1"", ""name"":""userTerminalName"", ""requester_specified_id"":""requesterSpecifiedId"", ""user"":{""id"":""1"", ""name"":""userName"", ""toopher_authentication_enabled"":true}}";
			UserTerminal userTerminal = api.advanced.userTerminals.GetById ("1");
			Assert.IsInstanceOf<UserTerminal> (userTerminal);
			Assert.AreEqual (WebClientMock.LastRequestMethod, "GET");
			Assert.AreEqual (userTerminal.id, "1");
			Assert.AreEqual (userTerminal.name, "userTerminalName");
			Assert.AreEqual (userTerminal.requesterSpecifiedId, "requesterSpecifiedId");
			Assert.IsInstanceOf<User> (userTerminal.user);
		}

		[Test]
		public void AdvancedUserTerminalsCreateTest ()
		{
			var api = getApi ();
			WebClientMock.ReturnValue = @"{""id"":""1"", ""name"":""userTerminalName"", ""requester_specified_id"":""requesterSpecifiedId"", ""user"":{""id"":""1"", ""name"":""userName"", ""toopher_authentication_enabled"":true}}";
			UserTerminal userTerminal = api.advanced.userTerminals.Create ("userName", "userTerminalName", "requesterSpecifiedId");
			Assert.AreEqual (WebClientMock.LastRequestMethod, "POST");
			Assert.AreEqual (userTerminal.id, "1");
			Assert.AreEqual (userTerminal.name, "userTerminalName");
			Assert.AreEqual (userTerminal.requesterSpecifiedId, "requesterSpecifiedId");
			Assert.IsInstanceOf<User> (userTerminal.user);
		}

		[Test]
		public void AdvancedUserTerminalsCreateWithParamsTest ()
		{
			var api = getApi ();
			WebClientMock.ReturnValue = @"{""id"":""1"", ""name"":""userTerminalName"", ""requester_specified_id"":""requesterSpecifiedId"", ""user"":{""id"":""1"", ""name"":""userName"", ""toopher_authentication_enabled"":true}}";
			NameValueCollection parameters = new NameValueCollection();
			parameters.Add ("foo", "bar");
			UserTerminal userTerminal = api.advanced.userTerminals.Create ("userName", "userTerminalName", "requesterSpecifiedId", parameters);
			Assert.AreEqual (WebClientMock.LastRequestMethod, "POST");
			Assert.AreEqual (WebClientMock.LastRequestData["foo"], "bar");
			Assert.AreEqual (userTerminal.id, "1");
			Assert.AreEqual (userTerminal.name, "userTerminalName");
			Assert.AreEqual (userTerminal.requesterSpecifiedId, "requesterSpecifiedId");
			Assert.IsInstanceOf<User> (userTerminal.user);
		}

		[Test]
		public void GeneratePairingLinkTest ()
		{
			var api = getApi ();
			WebClientMock.ReturnValue = @"{""reset_authorization"":""abcde"", ""url"":""http://testonly/pairings/1/reset?reset_authorization=abcde""}}";
			string pairingResetLink = api.GetPairingResetLink ("1");
			Assert.AreEqual (pairingResetLink, "http://testonly/pairings/1/reset?reset_authorization=abcde");
		}

		[Test]
		public void GenerateAdvancedApiUsageFactory ()
		{
			var api = getApi();
			Assert.IsInstanceOf<ToopherApi.AdvancedApiUsageFactory> (api.advanced);
		}

		[Test]
		public void GenerateAdvancedPairings ()
		{
			var api = getApi();
			Assert.IsInstanceOf<ToopherApi.AdvancedApiUsageFactory.Pairings> (api.advanced.pairings);
		}

		[Test]
		public void GenerateAdvancedRaw ()
		{
			var api = getApi();
			Assert.IsInstanceOf<ToopherApi.AdvancedApiUsageFactory.ApiRawRequester> (api.advanced.raw);
		}

		[Test]
		public void GenerateAdvancedAuthenticationRequests ()
		{
			var api = getApi();
			Assert.IsInstanceOf<ToopherApi.AdvancedApiUsageFactory.AuthenticationRequests> (api.advanced.authenticationRequests);
		}

		[Test]
		public void GenerateAdvancedUsers ()
		{
			var api = getApi();
			Assert.IsInstanceOf<ToopherApi.AdvancedApiUsageFactory.Users> (api.advanced.users);
		}

		[Test]
		public void GenerateAdvancedUserTerminals ()
		{
			var api = getApi();
			Assert.IsInstanceOf<ToopherApi.AdvancedApiUsageFactory.UserTerminals> (api.advanced.userTerminals);
		}

		[Test]
		public void PairingRefreshFromServerTest ()
		{
			var api = getApi();
			var response = (IDictionary<string, object>)SimpleJson.SimpleJson.DeserializeObject(@"{""id"":""1"", ""pending"":false, ""enabled"":true, ""user"":{""id"":""1"",""name"":""userName"", ""toopher_authentication_enabled"":true}}");
			WebClientMock.ReturnValue = @"{""id"":""1"", ""pending"":true, ""enabled"":false, ""user"":{""id"":""1"",""name"":""userNameChanged"", ""toopher_authentication_enabled"":true}}";
			Pairing pairing = new Pairing (response, api);
			pairing.RefreshFromServer ();
			Assert.IsInstanceOf<Pairing> (pairing);
			Assert.AreEqual (WebClientMock.LastRequestMethod, "GET");
			Assert.AreEqual (pairing.id, "1");
			Assert.AreEqual (pairing.user.name, "userNameChanged");
			Assert.IsFalse (pairing.enabled);
			Assert.IsTrue (pairing.pending);
		}

		[Test]
		public void UserRefreshFromServerTest ()
		{
			var api = getApi();
			var response = (IDictionary<string, object>)SimpleJson.SimpleJson.DeserializeObject(@"{""id"":""1"", ""name"":""username"", ""toopher_authentication_enabled"":false}");
			WebClientMock.ReturnValue = @"{""id"":""1"", ""name"":""userNameChanged"", ""toopher_authentication_enabled"":true}";
			User user = new User (response, api);
			user.RefreshFromServer ();
			Assert.IsInstanceOf<User> (user);
			Assert.AreEqual (WebClientMock.LastRequestMethod, "GET");
			Assert.AreEqual (user.id, "1");
			Assert.AreEqual (user.name, "userNameChanged");
			Assert.IsTrue (user.toopherAuthenticationEnabled);
		}

		[Test]
		public void UserTerminalRefreshFromServerTest ()
		{
			var api = getApi();
			var response = (IDictionary<string, object>)SimpleJson.SimpleJson.DeserializeObject(@"{""id"":""1"", ""name"":""userTerminalName"", ""requester_specified_id"":""requesterSpecifiedId"", ""user"":{""id"":""1"", ""name"":""userName"", ""toopher_authentication_enabled"":true}}");
			WebClientMock.ReturnValue = @"{""id"":""1"", ""name"":""userTerminalNameChanged"", ""requester_specified_id"":""requesterSpecifiedId"", ""user"":{""id"":""1"", ""name"":""userNameChanged"", ""toopher_authentication_enabled"":false}}";
			UserTerminal userTerminal = new UserTerminal (response, api);
			userTerminal.RefreshFromServer ();
			Assert.IsInstanceOf<UserTerminal> (userTerminal);
			Assert.AreEqual (WebClientMock.LastRequestMethod, "GET");
			Assert.AreEqual (userTerminal.id, "1");
			Assert.AreEqual (userTerminal.name, "userTerminalNameChanged");
			Assert.AreEqual (userTerminal.user.name, "userNameChanged");
			Assert.IsFalse (userTerminal.user.toopherAuthenticationEnabled);
		}
	}
}
