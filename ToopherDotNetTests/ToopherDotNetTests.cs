using System;
using OAuth;
using System.IO;
using System.Net;
using System.Web;
using System.Collections.Generic;
using System.Collections.Specialized;
using Toopher;
using NUnit.Framework;
using SimpleJson;

namespace ToopherDotNetTests
{
	[TestFixture()]
	public class TestBase
	{
		public const string DEFAULT_BASE_URL = "https://api.toopher.test/v1/";
		public const string TOOPHER_CONSUMER_KEY = "abcdefg";
		public const string TOOPHER_CONSUMER_SECRET = "hijklmnop";

		public const string PAIRING_RESPONSE = @"{""id"":""1"", ""pending"":false, ""enabled"":true, ""user"":{""id"":""1"",""name"":""some user"", ""toopher_authentication_enabled"":true}}";
		public const string AUTH_REQUEST_RESPONSE = @"{""id"":""1"", ""pending"":false, ""granted"":true, ""automated"":false, ""reason_code"":1, ""reason"":""its a test"", ""terminal"":{""id"":""1"", ""name"":""test terminal"", ""requester_specified_id"":""requesterSpecifiedId"", ""user"":{""id"":""1"",""name"":""some user"", ""toopher_authentication_enabled"":true}}, ""user"":{""id"":""1"",""name"":""some user"", ""toopher_authentication_enabled"":true}, ""action"": {""id"":""1"", ""name"":""actionName""}}";
		public const string USER_RESPONSE = @"{""id"":""1"", ""name"":""userName"", ""toopher_authentication_enabled"":true}";
		public const string USER_TERMINAL_RESPONSE = @"{""id"":""1"", ""name"":""userTerminalName"", ""requester_specified_id"":""requesterSpecifiedId"", ""user"":{""id"":""1"", ""name"":""userName"", ""toopher_authentication_enabled"":true}}";

		class MockWebResponse : WebResponse, IHttpWebResponse
		{
			HttpStatusCode errorCode;
			Stream responseStream;

			public MockWebResponse(HttpStatusCode errorCode, string responseBody)
			{
				this.errorCode = errorCode;
				this.responseStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(responseBody));
			}

			override public Stream GetResponseStream()
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
		public WebException makeError(HttpStatusCode errorCode, string responseBody)
		{
			WebResponse response = new MockWebResponse(errorCode, responseBody);
			return new WebException("", null, WebExceptionStatus.ProtocolError, response);
		}
		public class WebClientMock : WebClientProxy
		{
			static public String LastRequestMethod { get; set; }
			static public NameValueCollection LastRequestData { get; set; }
			static public Exception ReturnException { get; set; }
			static public string ReturnValue { get; set; }
			static public string ReturnValueArray { get; set; }
			string doit()
			{
				if (ReturnException != null)
				{
					throw ReturnException;
				} else if (ReturnValueArray != null)
				{
					var values = ReturnValueArray;
					ReturnValueArray = null;
					return values;
				} else
				{
					return ReturnValue;
				}
			}

			override public byte[] UploadValues(string requestUri, string method, NameValueCollection parameters)
			{
				LastRequestMethod = "POST";
				LastRequestData = parameters;
				return System.Text.Encoding.UTF8.GetBytes(doit());
			}
			override public string DownloadString(string requestUri)
			{
				LastRequestMethod = "GET";
				LastRequestData = this.QueryString;
				return doit();
			}
		}

		[SetUp]
		public virtual void Init()
		{
			WebClientMock.ReturnException = null;
			WebClientMock.ReturnValue = null;
			WebClientMock.LastRequestMethod = null;
			WebClientMock.LastRequestData = null;
		}

		public ToopherApi getToopherApi()
		{
			return new ToopherApi("key", "secret", null, typeof(WebClientMock));
		}
	}

	[TestFixture()]
	public class ToopherIframeTests : TestBase
	{
		private const string REQUEST_TOKEN = "s9s7vsb";
		private const string OAUTH_NONCE = "12345678";
		private DateTime TEST_DATE = new DateTime(1970, 1, 1, 0, 16, 40, 0);

		private ToopherIframe getToopherIframeApi()
		{
			return new ToopherIframe(TOOPHER_CONSUMER_KEY, TOOPHER_CONSUMER_SECRET, DEFAULT_BASE_URL);
		}

		[Test]
		public void ToopherIframeVersionTest()
		{
			Assert.IsTrue(int.Parse(ToopherIframe.IFRAME_VERSION) >= 1);
		}

		[Test]
		public void ToopherBaseUrlTest()
		{
			StringAssert.Contains("https", ToopherIframe.DEFAULT_BASE_URL);
			Assert.IsTrue(System.Uri.IsWellFormedUriString(ToopherIframe.DEFAULT_BASE_URL, System.UriKind.Absolute));
		}

		[Test]
		public void GetAuthenticationUrlTest()
		{
			var api = getToopherIframeApi();
			ToopherIframe.SetDateOverride(TEST_DATE);
			OAuthTools.SetNonceOverride(OAUTH_NONCE);
			OAuthTools.SetDateOverride(TEST_DATE);
			string expected = "https://api.toopher.test/v1/web/authenticate?action_name=Log+In&expires=1300&requester_metadata=None&reset_email=jdoe%40example.com&session_token=s9s7vsb&username=jdoe&v=2&oauth_consumer_key=abcdefg&oauth_nonce=12345678&oauth_signature=YN%2BkKNTaoypsB37fsjvMS8vsG5A%3D&oauth_signature_method=HMAC-SHA1&oauth_timestamp=1000&oauth_version=1.0&";
			var authUrl = api.GetAuthenticationUrl("jdoe", "jdoe@example.com", REQUEST_TOKEN);
			Assert.AreEqual(expected, authUrl);
		}

		[Test]
		public void GetAuthenticationUrlWithExtrasTest()
		{
			Dictionary<string, string> extras = new Dictionary<string, string>();
			extras.Add("allow_inline_pairing", "false");
			var api = getToopherIframeApi();
			ToopherIframe.SetDateOverride(TEST_DATE);
			OAuthTools.SetNonceOverride(OAUTH_NONCE);
			OAuthTools.SetDateOverride(TEST_DATE);
			string expected = "https://api.toopher.test/v1/web/authenticate?action_name=it+is+a+test&allow_inline_pairing=false&expires=1300&requester_metadata=None&reset_email=jdoe%40example.com&session_token=s9s7vsb&username=jdoe&v=2&oauth_consumer_key=abcdefg&oauth_nonce=12345678&oauth_signature=W%2F2dcdsVc7YgdSCZuEo8ViHLlOo%3D&oauth_signature_method=HMAC-SHA1&oauth_timestamp=1000&oauth_version=1.0&";
			var authUrl = api.GetAuthenticationUrl("jdoe", "jdoe@example.com", REQUEST_TOKEN, "it is a test", "None", extras);
			Assert.AreEqual(expected, authUrl);
		}

		[Test]
		public void GetUserManagementUrlTest()
		{
			var api = getToopherIframeApi();
			ToopherIframe.SetDateOverride(TEST_DATE);
			OAuthTools.SetNonceOverride(OAUTH_NONCE);
			OAuthTools.SetDateOverride(TEST_DATE);
			string expected = "https://api.toopher.test/v1/web/manage_user?expires=1300&reset_email=jdoe%40example.com&username=jdoe&v=2&oauth_consumer_key=abcdefg&oauth_nonce=12345678&oauth_signature=NjwH5yWPE2CCJL8v%2FMNknL%2BeTpE%3D&oauth_signature_method=HMAC-SHA1&oauth_timestamp=1000&oauth_version=1.0&";
			var userManagementUrl = api.GetUserManagementUrl("jdoe", "jdoe@example.com");
			Assert.AreEqual(expected, userManagementUrl);
		}

		[Test]
		public void GetUserManagementUrlWithExtrasTest()
		{
			Dictionary<string, string> extras = new Dictionary<string, string>();
			extras.Add("ttl", "100");
			var api = getToopherIframeApi();
			ToopherIframe.SetDateOverride(TEST_DATE);
			OAuthTools.SetNonceOverride(OAUTH_NONCE);
			OAuthTools.SetDateOverride(TEST_DATE);
			string expected = "https://api.toopher.test/v1/web/manage_user?expires=1100&reset_email=jdoe%40example.com&username=jdoe&v=2&oauth_consumer_key=abcdefg&oauth_nonce=12345678&oauth_signature=sV8qoKnxJ3fxfP6AHNa0eNFxzJs%3D&oauth_signature_method=HMAC-SHA1&oauth_timestamp=1000&oauth_version=1.0&";
			var userManagementUrl = api.GetUserManagementUrl("jdoe", "jdoe@example.com", extras);
			Assert.AreEqual(expected, userManagementUrl);
		}

		[Test]
		public void ValidatePostbackWithGoodSignatureIsSuccessfulTest()
		{
			var api = getToopherIframeApi();
			ToopherIframe.SetDateOverride(TEST_DATE);
			Dictionary<string, string[]> data = new Dictionary<string, string[]>();
			data.Add("foo", new string[]{"bar"});
			data.Add("timestamp", new string[]{((int)(TEST_DATE - new DateTime(1970, 1, 1)).TotalSeconds).ToString()});
			data.Add("session_token", new string[]{REQUEST_TOKEN});
			data.Add("toopher_sig", new string[]{"6d2c7GlQssGmeYYGpcf+V/kirOI="});
			try
			{
				Assert.IsNotNull(api.ValidatePostback(data, REQUEST_TOKEN, 5));
			} catch (Exception)
			{
				Assert.Fail("Valid signature, timestamp, and session token did not return validated data");
			}
		}

		[Test]
		public void ValidatePostbackWithBadSignatureFailsTest()
		{
			var api = getToopherIframeApi();
			ToopherIframe.SetDateOverride(TEST_DATE);
			Dictionary<string, string[]> data = new Dictionary<string, string[]>();
			data.Add("foo", new string[]{"bar"});
			data.Add("timestamp", new string[]{((int)(TEST_DATE - new DateTime(1970, 1, 1)).TotalSeconds).ToString()});
			data.Add("session_token", new string[]{REQUEST_TOKEN});
			data.Add("toopher_sig", new string[]{"invalid"});
			var ex = Assert.Throws<Toopher.SignatureValidationError>(() => api.ValidatePostback(data, REQUEST_TOKEN, 5));
			Assert.That(ex.Message, Is.StringContaining("Computed signature does not match"));
		}

		[Test]
		public void ValidatePostbackWithExpiredSignatureFailsTest()
		{
			var api = getToopherIframeApi();
			ToopherIframe.SetDateOverride(new DateTime(1970, 2, 1, 0, 16, 40, 0));
			Dictionary<string, string[]> data = new Dictionary<string, string[]>();
			data.Add("foo", new string[]{"bar"});
			data.Add("timestamp", new string[]{((int)(TEST_DATE - new DateTime(1970, 1, 1)).TotalSeconds).ToString()});
			data.Add("session_token", new string[]{REQUEST_TOKEN});
			data.Add("toopher_sig", new string[]{"6d2c7GlQssGmeYYGpcf+V/kirOI="});
			var ex = Assert.Throws<Toopher.SignatureValidationError>(() => api.ValidatePostback(data, REQUEST_TOKEN, 5));
			Assert.That(ex.Message, Is.StringContaining("TTL Expired"));
		}

		[Test]
		public void ValidatePostbackWithInvalidSessionTokenFailsTest()
		{
			var api = getToopherIframeApi();
			ToopherIframe.SetDateOverride(TEST_DATE);
			Dictionary<string, string[]> data = new Dictionary<string, string[]>();
			data.Add("foo", new string[]{"bar"});
			data.Add("timestamp", new string[]{((int)(TEST_DATE - new DateTime(1970, 1, 1)).TotalSeconds).ToString()});
			data.Add("session_token", new string[]{"invalid token"});
			data.Add("toopher_sig", new string[]{"6d2c7GlQssGmeYYGpcf+V/kirOI="});
			var ex = Assert.Throws<Toopher.SignatureValidationError>(() => api.ValidatePostback(data, REQUEST_TOKEN, 5));
			Assert.That(ex.Message, Is.StringContaining("Session token does not match expected value"));
		}

		[Test]
		public void ValidatePostbackMissingTimestampFailsTest()
		{
			var api = getToopherIframeApi();
			ToopherIframe.SetDateOverride(TEST_DATE);
			Dictionary<string, string[]> data = new Dictionary<string, string[]>();
			data.Add("foo", new string[]{"bar"});
			data.Add("session_token", new string[]{REQUEST_TOKEN});
			data.Add("toopher_sig", new string[]{"6d2c7GlQssGmeYYGpcf+V/kirOI="});
			var ex = Assert.Throws<Toopher.SignatureValidationError>(() => api.ValidatePostback(data, REQUEST_TOKEN, 5));
			Assert.That(ex.Message, Is.StringContaining("Missing required keys: timestamp"));
		}

		[Test]
		public void ValidatePostbackMissingSignatureFailsTest()
		{
			var api = getToopherIframeApi();
			ToopherIframe.SetDateOverride(TEST_DATE);
			Dictionary<string, string[]> data = new Dictionary<string, string[]>();
			data.Add("foo", new string[]{"bar"});
			data.Add("timestamp", new string[]{((int)(TEST_DATE - new DateTime(1970, 1, 1)).TotalSeconds).ToString()});
			data.Add("session_token", new string[]{REQUEST_TOKEN});
			var ex = Assert.Throws<Toopher.SignatureValidationError>(() => api.ValidatePostback(data, REQUEST_TOKEN, 5));
			Assert.That(ex.Message, Is.StringContaining("Missing required keys: toopher_sig"));
		}

		[Test]
		public void ValidatePostbackMissingSessionTokenFailsTest()
		{
			var api = getToopherIframeApi();
			ToopherIframe.SetDateOverride(TEST_DATE);
			Dictionary<string, string[]> data = new Dictionary<string, string[]>();
			data.Add("foo", new string[]{"bar"});
			data.Add("timestamp", new string[]{((int)(TEST_DATE - new DateTime(1970, 1, 1)).TotalSeconds).ToString()});
			data.Add("toopher_sig", new string[]{"6d2c7GlQssGmeYYGpcf+V/kirOI="});
			var ex = Assert.Throws<Toopher.SignatureValidationError>(() => api.ValidatePostback(data, REQUEST_TOKEN, 5));
			Assert.That(ex.Message, Is.StringContaining("Missing required keys: session_token"));
		}

		[Test]
		public void ValidatePostbackWithoutStringArrayIsSuccessfulTest()
		{
			var api = getToopherIframeApi();
			ToopherIframe.SetDateOverride(TEST_DATE);
			Dictionary<string, string> data = new Dictionary<string, string>()
			{
				{"foo", "bar"},
				{"timestamp", ((int)(TEST_DATE - new DateTime(1970, 1, 1)).TotalSeconds).ToString()},
				{"session_token", REQUEST_TOKEN},
				{"toopher_sig", "6d2c7GlQssGmeYYGpcf+V/kirOI="}
			};
			try
			{
				Assert.IsNotNull(api.ValidatePostback(data, REQUEST_TOKEN, 5));
			} catch (Exception)
			{
				Assert.Fail("ValidatePostback overload for Dictionary<string, string> was not successful");
			}
		}
	}

	[TestFixture()]
	public class ToopherApiTests : TestBase
	{
		[Test()]
		public void ToopherVersionTest()
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
		public void ToopherBaseUrlTest()
		{
			StringAssert.Contains("https", ToopherApi.DEFAULT_BASE_URL);
			Assert.IsTrue(System.Uri.IsWellFormedUriString(ToopherApi.DEFAULT_BASE_URL, System.UriKind.Absolute));
		}

		[Test]
		public void CreatePairingWithPhraseTest()
		{
			var api = getToopherApi();
			WebClientMock.ReturnValue = PAIRING_RESPONSE;
			Pairing pairing = api.Pair("some user", "awkward turtle");
			Assert.AreEqual(WebClientMock.LastRequestMethod, "POST");
			Assert.AreEqual(WebClientMock.LastRequestData["pairing_phrase"], "awkward turtle");
			Assert.AreEqual(WebClientMock.LastRequestData["user_name"], "some user");
			Assert.AreEqual(pairing.id, "1");
		}

		[Test]
		public void CreateSmsPairingTest()
		{
			var api = getToopherApi();
			WebClientMock.ReturnValue = PAIRING_RESPONSE;
			Pairing pairing = api.Pair("some user", "555-555-5555");
			Assert.AreEqual(WebClientMock.LastRequestMethod, "POST");
			Assert.AreEqual(WebClientMock.LastRequestData["phone_number"], "555-555-5555");
			Assert.AreEqual(WebClientMock.LastRequestData["user_name"], "some user");
			Assert.AreEqual(pairing.id, "1");
		}

		[Test]
		public void CreateQrPairingTest()
		{
			var api = getToopherApi();
			WebClientMock.ReturnValue = PAIRING_RESPONSE;
			Pairing pairing = api.Pair("some user");
			Assert.AreEqual(WebClientMock.LastRequestMethod, "POST");
			Assert.AreEqual(WebClientMock.LastRequestData["user_name"], "some user");
			Assert.AreEqual(pairing.id, "1");
		}

		[Test]
		public void ArbitraryParametersOnPairTest()
		{
			var api = getToopherApi();
			WebClientMock.ReturnValue = PAIRING_RESPONSE;
			Pairing pairing = api.Pair("some user", "awkward turtle", extras: new Dictionary<string,string>(){{"test_param", "42"}});
			Assert.AreEqual(WebClientMock.LastRequestMethod, "POST");
			Assert.AreEqual(WebClientMock.LastRequestData["test_param"], "42");
			Assert.AreEqual(WebClientMock.LastRequestData["user_name"], "some user");
			Assert.AreEqual(WebClientMock.LastRequestData["pairing_phrase"], "awkward turtle");
			Assert.AreEqual(pairing.id, "1");
		}

		[Test]
		public void AuthenticateWithPairingIdTest()
		{
			var api = getToopherApi();
			string pairingId = Guid.NewGuid().ToString();
			WebClientMock.ReturnValue = @"{""id"":""" + pairingId + @""", ""pending"":false, ""granted"":true, ""automated"":false, ""reason_code"":1, ""reason"":""its a test"", ""terminal"":{""id"":""1"", ""name"":""test terminal"",""requester_specified_id"":""requesterSpecifiedId"",""user"":{""id"":""1"",""name"":""some user"", ""toopher_authentication_enabled"":true}}, ""user"":{""id"":""1"",""name"":""some user"", ""toopher_authentication_enabled"":true}, ""action"": {""id"":""1"", ""name"":""actionName""}}";
			AuthenticationRequest auth = api.Authenticate(pairingId, "test terminal");
			Assert.AreEqual(WebClientMock.LastRequestMethod, "POST");
			Assert.AreEqual(WebClientMock.LastRequestData["pairing_id"], pairingId);
			Assert.AreEqual(WebClientMock.LastRequestData["terminal_name"], "test terminal");
			Assert.AreEqual(auth.id, pairingId);
		}

		[Test]
		public void AuthenticateWithUsernameAndExtrasTest()
		{
			var api = getToopherApi();
			WebClientMock.ReturnValue = AUTH_REQUEST_RESPONSE;
			AuthenticationRequest auth = api.Authenticate("some other user", requesterSpecifiedId: "requester specified id", extras: new Dictionary<String, String>(){{ "random_key" , "42" }});
			Assert.AreEqual(WebClientMock.LastRequestMethod, "POST");
			Assert.AreEqual(WebClientMock.LastRequestData["user_name"], "some other user");
			Assert.AreEqual(WebClientMock.LastRequestData["requester_specified_terminal_id"], "requester specified id");
			Assert.AreEqual(WebClientMock.LastRequestData["random_key"], "42");
			Assert.AreEqual(auth.id, "1");
		}

		[Test]
		public void ArbitraryParamtersOnAuthenticateTest()
		{
			var api = getToopherApi();
			WebClientMock.ReturnValue = AUTH_REQUEST_RESPONSE;
			AuthenticationRequest auth = api.Authenticate("1", "test terminal", extras: new Dictionary<string, string>(){{ "test_param", "42" }});
			Assert.AreEqual(WebClientMock.LastRequestMethod, "POST");
			Assert.AreEqual(WebClientMock.LastRequestData["test_param"], "42");
			Assert.AreEqual(auth.id, "1");
		}

		[Test]
		public void GenerateAdvancedApiUsageFactory()
		{
			var api = getToopherApi();
			Assert.IsInstanceOf<ToopherApi.AdvancedApiUsageFactory>(api.advanced);
		}

		[Test]
		public void GenerateAdvancedPairings()
		{
			var api = getToopherApi();
			Assert.IsInstanceOf<ToopherApi.AdvancedApiUsageFactory.Pairings>(api.advanced.pairings);
		}

		[Test]
		public void GenerateAdvancedRaw()
		{
			var api = getToopherApi();
			Assert.IsInstanceOf<ToopherApi.AdvancedApiUsageFactory.ApiRawRequester>(api.advanced.raw);
		}

		[Test]
		public void GenerateAdvancedAuthenticationRequests()
		{
			var api = getToopherApi();
			Assert.IsInstanceOf<ToopherApi.AdvancedApiUsageFactory.AuthenticationRequests>(api.advanced.authenticationRequests);
		}

		[Test]
		public void GenerateAdvancedUsers()
		{
			var api = getToopherApi();
			Assert.IsInstanceOf<ToopherApi.AdvancedApiUsageFactory.Users>(api.advanced.users);
		}

		[Test]
		public void GenerateAdvancedUserTerminals()
		{
			var api = getToopherApi();
			Assert.IsInstanceOf<ToopherApi.AdvancedApiUsageFactory.UserTerminals>(api.advanced.userTerminals);
		}

		[Test]
		public void AdvancedPairingsGetByIdTest()
		{
			var api = getToopherApi();
			WebClientMock.ReturnValue = PAIRING_RESPONSE;
			Pairing pairing = api.advanced.pairings.GetById("1");
			Assert.IsInstanceOf<Pairing>(pairing);
			Assert.AreEqual(WebClientMock.LastRequestMethod, "GET");
			Assert.AreEqual(pairing.id, "1");
			Assert.AreEqual(pairing.user.name, "some user");
			Assert.AreEqual(pairing.user.id, "1");
			Assert.IsTrue(pairing.enabled);
		}

		[Test]
		public void AccessArbitraryKeysInAdvancedPairingsGetByIdTest()
		{
			var api = getToopherApi();
			WebClientMock.ReturnValue = @"{""id"":""1"", ""pending"":false, ""enabled"":true, ""user"":{""id"":""1"",""name"":""some user"", ""toopher_authentication_enabled"":true}, ""random_key"":""84""}";
			Pairing pairing = api.advanced.pairings.GetById("1");
			Assert.AreEqual(WebClientMock.LastRequestMethod, "GET");
			Assert.AreEqual(pairing.id, "1");
			Assert.AreEqual(pairing.user.name, "some user");
			Assert.AreEqual(pairing.user.id, "1");
			Assert.IsTrue(pairing.enabled);
			Assert.AreEqual(pairing["random_key"], "84");
		}

		[Test]
		public void AdvancedAuthenticationRequestsGetByIdTest()
		{
			var api = getToopherApi();
			WebClientMock.ReturnValue = AUTH_REQUEST_RESPONSE;
			AuthenticationRequest auth = api.advanced.authenticationRequests.GetById("1");
			Assert.AreEqual(WebClientMock.LastRequestMethod, "GET");
			Assert.AreEqual(auth.id, "1");
			Assert.IsFalse(auth.pending);
			Assert.IsTrue(auth.granted);
			Assert.AreEqual(auth.reason, "its a test");
			Assert.AreEqual(auth.terminal.id, "1");
			Assert.AreEqual(auth.terminal.name, "test terminal");
		}

		[Test]
		public void AccessArbitraryKeysInAdvancedAuthenticationRequestsGetByIdTest()
		{
			var api = getToopherApi();
			WebClientMock.ReturnValue = @"{""id"":""1"", ""pending"":false, ""granted"":true, ""automated"":false, ""reason_code"":1, ""reason"":""its a test"", ""terminal"":{""id"":""1"", ""name"":""test terminal"", ""requester_specified_id"": ""requesterSpecifiedId"", ""user"":{""id"":""1"",""name"":""some user"", ""toopher_authentication_enabled"":true}}, ""user"":{""id"":""1"",""name"":""some user"", ""toopher_authentication_enabled"":true}, ""action"": {""id"":""1"", ""name"":""actionName""}, ""random_key"":""84""}";
			AuthenticationRequest auth = api.advanced.authenticationRequests.GetById("1");
			Assert.AreEqual(WebClientMock.LastRequestMethod, "GET");
			Assert.AreEqual(auth.id, "1");
			Assert.IsFalse(auth.pending);
			Assert.IsTrue(auth.granted);
			Assert.AreEqual(auth.reason, "its a test");
			Assert.AreEqual(auth.terminal.id, "1");
			Assert.AreEqual(auth.terminal.name, "test terminal");
			Assert.AreEqual(auth["random_key"], "84");
		}

		[Test]
		public void AdvancedUsersGetByIdTest()
		{
			var api = getToopherApi();
			WebClientMock.ReturnValue = USER_RESPONSE;
			User user = api.advanced.users.GetById("1");
			Assert.IsInstanceOf<User>(user);
			Assert.AreEqual(WebClientMock.LastRequestMethod, "GET");
			Assert.AreEqual(user.id, "1");
			Assert.AreEqual(user.name, "userName");
			Assert.IsTrue(user.toopherAuthenticationEnabled);
		}

		[Test]
		public void AccessArbitraryKeysInAdvancedUsersGetByIdTest()
		{
			var api = getToopherApi();
			WebClientMock.ReturnValue = @"{""id"":""1"", ""name"":""userName"", ""toopher_authentication_enabled"":true, ""random_key"":""84""}";
			User user = api.advanced.users.GetById("1");
			Assert.AreEqual(WebClientMock.LastRequestMethod, "GET");
			Assert.AreEqual(user.id, "1");
			Assert.AreEqual(user.name, "userName");
			Assert.IsTrue(user.toopherAuthenticationEnabled);
			Assert.AreEqual(user["random_key"], "84");
		}

		[Test]
		public void AdvancedUsersGetByNameTest()
		{
			var api = getToopherApi();
			WebClientMock.ReturnValueArray = @"[{""id"":""1"", ""name"":""userName"", ""toopher_authentication_enabled"":true}]";
			WebClientMock.ReturnValue = USER_RESPONSE;
			User user = api.advanced.users.GetByName("userName");
			Assert.AreEqual(WebClientMock.LastRequestMethod, "GET");
			Assert.AreEqual(user.id, "1");
			Assert.AreEqual(user.name, "userName");
			Assert.IsTrue(user.toopherAuthenticationEnabled);
		}

		[Test]
		public void AdvancedUsersCreateTest()
		{
			var api = getToopherApi();
			WebClientMock.ReturnValue = USER_RESPONSE;
			User user = api.advanced.users.Create("userName");
			Assert.IsInstanceOf<User>(user);
			Assert.AreEqual(WebClientMock.LastRequestMethod, "POST");
			Assert.AreEqual(user.id, "1");
			Assert.AreEqual(user.name, "userName");
			Assert.IsTrue(user.toopherAuthenticationEnabled);
		}

		[Test]
		public void AdvancedUsersCreateWithParamsTest()
		{
			var api = getToopherApi();
			WebClientMock.ReturnValue = USER_RESPONSE;
			NameValueCollection parameters = new NameValueCollection();
			parameters.Add("foo", "bar");
			User user = api.advanced.users.Create("userName", parameters);
			Assert.IsInstanceOf<User>(user);
			Assert.AreEqual(WebClientMock.LastRequestMethod, "POST");
			Assert.AreEqual(WebClientMock.LastRequestData["foo"], "bar");
			Assert.AreEqual(user.id, "1");
			Assert.AreEqual(user.name, "userName");
			Assert.IsTrue(user.toopherAuthenticationEnabled);
		}

		[Test]
		public void AdvancedUserTerminalsGetByIdTest()
		{
			var api = getToopherApi();
			WebClientMock.ReturnValue = USER_TERMINAL_RESPONSE;
			UserTerminal userTerminal = api.advanced.userTerminals.GetById("1");
			Assert.IsInstanceOf<UserTerminal>(userTerminal);
			Assert.AreEqual(WebClientMock.LastRequestMethod, "GET");
			Assert.AreEqual(userTerminal.id, "1");
			Assert.AreEqual(userTerminal.name, "userTerminalName");
			Assert.AreEqual(userTerminal.requesterSpecifiedId, "requesterSpecifiedId");
			Assert.IsInstanceOf<User>(userTerminal.user);
		}

		[Test]
		public void AccessArbitraryKeysInAdvancedUserTerminalsGetByIdTest()
		{
			var api = getToopherApi();
			WebClientMock.ReturnValue = @"{""id"":""1"", ""name"":""userTerminalName"", ""requester_specified_id"":""requesterSpecifiedId"",""random_key"":""84"", ""user"":{""id"":""1"", ""name"":""userName"", ""toopher_authentication_enabled"":true}}";
			UserTerminal userTerminal = api.advanced.userTerminals.GetById("1");
			Assert.AreEqual(WebClientMock.LastRequestMethod, "GET");
			Assert.AreEqual(userTerminal.id, "1");
			Assert.AreEqual(userTerminal.name, "userTerminalName");
			Assert.IsTrue(userTerminal.user.toopherAuthenticationEnabled);
			Assert.AreEqual(userTerminal["random_key"], "84");
		}

		[Test]
		public void AdvancedUserTerminalsCreateTest()
		{
			var api = getToopherApi();
			WebClientMock.ReturnValue = USER_TERMINAL_RESPONSE;
			UserTerminal userTerminal = api.advanced.userTerminals.Create("userName", "userTerminalName", "requesterSpecifiedId");
			Assert.AreEqual(WebClientMock.LastRequestMethod, "POST");
			Assert.AreEqual(userTerminal.id, "1");
			Assert.AreEqual(userTerminal.name, "userTerminalName");
			Assert.AreEqual(userTerminal.requesterSpecifiedId, "requesterSpecifiedId");
			Assert.IsInstanceOf<User>(userTerminal.user);
		}

		[Test]
		public void AdvancedUserTerminalsCreateWithParamsTest()
		{
			var api = getToopherApi();
			WebClientMock.ReturnValue = USER_TERMINAL_RESPONSE;
			NameValueCollection parameters = new NameValueCollection();
			parameters.Add("foo", "bar");
			UserTerminal userTerminal = api.advanced.userTerminals.Create("userName", "userTerminalName", "requesterSpecifiedId", parameters);
			Assert.AreEqual(WebClientMock.LastRequestMethod, "POST");
			Assert.AreEqual(WebClientMock.LastRequestData["foo"], "bar");
			Assert.AreEqual(userTerminal.id, "1");
			Assert.AreEqual(userTerminal.name, "userTerminalName");
			Assert.AreEqual(userTerminal.requesterSpecifiedId, "requesterSpecifiedId");
			Assert.IsInstanceOf<User>(userTerminal.user);
		}

		[Test]
		[ExpectedException(typeof(UserDisabledError))]
		public void DisabledUserRaisesCorrectErrorTest()
		{
			var api = getToopherApi();
			WebClientMock.ReturnException = makeError((HttpStatusCode)409,
				@"{""error_code"":704, ""error_message"":""The specified user has disabled Toopher authentication.""}");
			api.Authenticate("some disabled user", requesterSpecifiedId: "some random string");
		}

		[Test]
		[ExpectedException(typeof(UserUnknownError))]
		public void UnknownUserRaisesCorrectErrorTest()
		{
			var api = getToopherApi();
			WebClientMock.ReturnException = makeError((HttpStatusCode)409,
				@"{""error_code"":705, ""error_message"":""No matching user exists.""}");
			api.Authenticate("some unknown user", requesterSpecifiedId: "some random string");
		}

		[Test]
		[ExpectedException(typeof(TerminalUnknownError))]
		public void UnknownTerminalRaisesCorrectErrorTest()
		{
			var api = getToopherApi();
			WebClientMock.ReturnException = makeError((HttpStatusCode)409,
				@"{""error_code"":706, ""error_message"":""No matching terminal exists.""}");
			api.Authenticate("some unknown user", requesterSpecifiedId: "some random string");
		}

		[Test]
		[ExpectedException(typeof(PairingDeactivatedError))]
		public void DeactivatedPairingRaisesCorrectErrorTest()
		{
			var api = getToopherApi();
			WebClientMock.ReturnException = makeError((HttpStatusCode)409,
				@"{""error_code"":601, ""error_message"":""This pairing has been deactivated.""}");
			api.Authenticate("some disabled user", requesterSpecifiedId: "some random string");
		}

		[Test]
		[ExpectedException(typeof(PairingDeactivatedError))]
		public void UnauthorizedPairingRaisesCorrectErrorTest()
		{
			var api = getToopherApi();
			WebClientMock.ReturnException = makeError((HttpStatusCode)409,
				@"{""error_code"":601, ""error_message"":""This pairing has not been authorized to authenticate.""}");
			api.Authenticate("some unauthorized user", requesterSpecifiedId: "some random string");
		}
	}

	[TestFixture()]
	public class PairingTests : TestBase
	{
		private IDictionary<string, object> PAIRING_DICT = (IDictionary<string, object>)SimpleJson.SimpleJson.DeserializeObject(@"{""id"":""1"", ""pending"":false, ""enabled"":true, ""user"":{""id"":""1"",""name"":""userName"", ""toopher_authentication_enabled"":true}}");

		[Test]
		public void CreatePairingTest()
		{
			var api = getToopherApi();
			Pairing pairing = new Pairing(PAIRING_DICT, api);
			Assert.AreEqual(pairing.id, "1");
			Assert.IsFalse(pairing.pending);
			Assert.IsTrue(pairing.enabled);
			Assert.AreEqual(pairing.user.id, "1");
			Assert.AreEqual(pairing.user.name, "userName");
			Assert.IsTrue(pairing.user.toopherAuthenticationEnabled);
		}

		[Test]
		public void PairingRefreshFromServerTest()
		{
			var api = getToopherApi();
			WebClientMock.ReturnValue = @"{""id"":""1"", ""pending"":true, ""enabled"":false, ""user"":{""id"":""1"",""name"":""userNameChanged"", ""toopher_authentication_enabled"":true}}";
			Pairing pairing = new Pairing(PAIRING_DICT, api);
			pairing.RefreshFromServer();
			Assert.IsInstanceOf<Pairing>(pairing);
			Assert.AreEqual(WebClientMock.LastRequestMethod, "GET");
			Assert.AreEqual(pairing.id, "1");
			Assert.AreEqual(pairing.user.name, "userNameChanged");
			Assert.IsFalse(pairing.enabled);
			Assert.IsTrue(pairing.pending);
		}

		[Test]
		public void PairingGetResetLinkTest()
		{
			var api = getToopherApi();
			string link = "http://api.toopher.test/v1/pairings/1/reset?reset_authorization=abcde";
			WebClientMock.ReturnValue = @"{""url"":""" + link + @"""}";
			Pairing pairing = new Pairing(PAIRING_DICT, api);
			string returnedLink = pairing.GetResetLink();
			Assert.AreEqual(WebClientMock.LastRequestMethod, "POST");
			Assert.AreEqual(returnedLink, link);
		}

		[Test]
		public void PairingEmailResetLinkTest()
		{
			var api = getToopherApi();
			WebClientMock.ReturnValue = @"{}";
			Pairing pairing = new Pairing(PAIRING_DICT, api);
			pairing.EmailResetLink("test@test.com");
			Assert.AreEqual(WebClientMock.LastRequestMethod, "POST");
			Assert.AreEqual(WebClientMock.LastRequestData["reset_email"], "test@test.com");
		}

		[Test]
		public void PairingGetQrCodeImage()
		{
			var api = getToopherApi();
			WebClientMock.ReturnValue = @"{}";
			Pairing pairing = new Pairing(PAIRING_DICT, api);
			pairing.GetQrCodeImage();
			Assert.AreEqual(WebClientMock.LastRequestMethod, "GET");
		}
	}


	[TestFixture()]
	public class AuthenticationRequestTests : TestBase
	{
		private IDictionary<string, object> AUTH_REQUEST_DICT = (IDictionary<string, object>)SimpleJson.SimpleJson.DeserializeObject(@"{""id"":""1"", ""pending"":true, ""granted"":false, ""automated"":false, ""reason_code"":1, ""reason"":""its a test"", ""terminal"":{""id"":""1"", ""name"":""test terminal"", ""requester_specified_id"": ""requesterSpecifiedId"", ""user"":{""id"":""1"",""name"":""some user"", ""toopher_authentication_enabled"":true}}, ""user"":{""id"":""1"",""name"":""some user"", ""toopher_authentication_enabled"":true}, ""action"": {""id"":""1"", ""name"":""actionName""}}");

		[Test]
		public void CreateAuthenticationRequestTest()
		{
			var api = getToopherApi();
			AuthenticationRequest auth = new AuthenticationRequest(AUTH_REQUEST_DICT, api);
			Assert.AreEqual(auth.id, "1");
			Assert.IsTrue(auth.pending);
			Assert.IsFalse(auth.granted);
			Assert.IsFalse(auth.automated);
			Assert.AreEqual(auth.reasonCode, 1);
			Assert.AreEqual(auth.reason, "its a test");
			Assert.AreEqual(auth.terminal.id, "1");
			Assert.AreEqual(auth.terminal.name, "test terminal");
			Assert.AreEqual(auth.terminal.requesterSpecifiedId, "requesterSpecifiedId");
			Assert.AreEqual(auth.user.id, "1");
			Assert.AreEqual(auth.user.name, "some user");
			Assert.IsTrue(auth.user.toopherAuthenticationEnabled);
			Assert.AreEqual(auth.action.id, "1");
			Assert.AreEqual(auth.action.name, "actionName");
		}

		[Test]
		public void AuthenticationRequestRefreshFromServerTest()
		{
			var api = getToopherApi();
			WebClientMock.ReturnValue = @"{""id"":""1"", ""pending"":true, ""granted"":true, ""automated"":false, ""reason_code"":2, ""reason"":""its a test"", ""terminal"":{""id"":""1"", ""name"":""test terminal CHANGED"", ""requester_specified_id"": ""requesterSpecifiedId"", ""user"":{""id"":""1"",""name"":""some user"", ""toopher_authentication_enabled"":true}}, ""user"":{""id"":""1"",""name"":""some user CHANGED"", ""toopher_authentication_enabled"":true}, ""action"": {""id"":""1"", ""name"":""actionName CHANGED""}}";
			AuthenticationRequest auth = new AuthenticationRequest(AUTH_REQUEST_DICT, api);
			auth.RefreshFromServer();
			Assert.IsInstanceOf<AuthenticationRequest>(auth);
			Assert.AreEqual(WebClientMock.LastRequestMethod, "GET");
			Assert.IsTrue(auth.pending);
			Assert.AreEqual(auth.reasonCode, 2);
			Assert.AreEqual(auth.terminal.name, "test terminal CHANGED");
			Assert.AreEqual(auth.user.name, "some user CHANGED");
			Assert.AreEqual(auth.action.name, "actionName CHANGED");
		}

		[Test]
		public void AuthenticationRequestGrantWithOtpTest()
		{
			var api = getToopherApi();
			WebClientMock.ReturnValue = AUTH_REQUEST_RESPONSE;
			AuthenticationRequest auth = new AuthenticationRequest(AUTH_REQUEST_DICT, api);
			auth.GrantWithOtp("123456");
			Assert.AreEqual(WebClientMock.LastRequestMethod, "POST");
			Assert.AreEqual(WebClientMock.LastRequestData["otp"], "123456");
			Assert.AreEqual(auth.id, "1");
			Assert.IsTrue(auth.granted);
		}
	}


	[TestFixture()]
	public class UserTests : TestBase
	{
		private IDictionary<string, object> USER_DICT = (IDictionary<string, object>)SimpleJson.SimpleJson.DeserializeObject(@"{""id"":""1"", ""name"":""userName"", ""toopher_authentication_enabled"":true}");

		[Test]
		public void CreateUserTest()
		{
			var api = getToopherApi();
			Toopher.User user = new Toopher.User(USER_DICT, api);
			Assert.AreEqual(user.id, "1");
			Assert.AreEqual(user.name, "userName");
			Assert.IsTrue(user.toopherAuthenticationEnabled);
		}

		[Test]
		public void UserRefreshFromServerTest()
		{
			var api = getToopherApi();
			WebClientMock.ReturnValue = @"{""id"":""1"", ""name"":""userNameChanged"", ""toopher_authentication_enabled"":false}";
			User user = new User(USER_DICT, api);
			user.RefreshFromServer();
			Assert.IsInstanceOf<User>(user);
			Assert.AreEqual(WebClientMock.LastRequestMethod, "GET");
			Assert.AreEqual(user.id, "1");
			Assert.AreEqual(user.name, "userNameChanged");
			Assert.IsFalse(user.toopherAuthenticationEnabled);
		}

		[Test]
		public void UserEnableToopherAuthentication()
		{
			var api = getToopherApi();
			WebClientMock.ReturnValue = USER_RESPONSE;
			User user = new User(USER_DICT, api);
			user.EnableToopherAuthentication();
			Assert.AreEqual(WebClientMock.LastRequestMethod, "POST");
			Assert.AreEqual(WebClientMock.LastRequestData["toopher_authentication_enabled"], "true");
			Assert.AreEqual(user.id, "1");
			Assert.IsTrue(user.toopherAuthenticationEnabled);
		}

		[Test]
		public void UserDisableToopherAuthentication()
		{
			var api = getToopherApi();
			WebClientMock.ReturnValue = @"{""id"":""1"", ""name"":""userName"", ""toopher_authentication_enabled"":false}";
			User user = new User(USER_DICT, api);
			user.DisableToopherAuthentication();
			Assert.AreEqual(WebClientMock.LastRequestMethod, "POST");
			Assert.AreEqual(WebClientMock.LastRequestData["toopher_authentication_enabled"], "false");
			Assert.AreEqual(user.id, "1");
			Assert.IsFalse(user.toopherAuthenticationEnabled);
		}

		[Test]
		public void UserResetTest()
		{
			var api = getToopherApi();
			WebClientMock.ReturnValue = USER_RESPONSE;
			User user = new User(USER_DICT, api);
			user.Reset();
			Assert.AreEqual(WebClientMock.LastRequestMethod, "POST");
			Assert.AreEqual(WebClientMock.LastRequestData["name"], "userName");
		}
	}


	[TestFixture()]
	public class UserTerminalTests : TestBase
	{
		private IDictionary<string, object> USER_TERMINAL_DICT = (IDictionary<string, object>)SimpleJson.SimpleJson.DeserializeObject(@"{""id"":""1"", ""name"":""userTerminalName"", ""requester_specified_id"":""requesterSpecifiedId"", ""user"":{""id"":""1"", ""name"":""userName"", ""toopher_authentication_enabled"":true}}");

		[Test]
		public void CreateUserTerminalTest()
		{
			var api = getToopherApi();
			UserTerminal userTerminal = new UserTerminal(USER_TERMINAL_DICT, api);
			Assert.AreEqual(userTerminal.id, "1");
			Assert.AreEqual(userTerminal.name, "userTerminalName");
			Assert.AreEqual(userTerminal.requesterSpecifiedId, "requesterSpecifiedId");
			Assert.AreEqual(userTerminal.user.id, "1");
			Assert.AreEqual(userTerminal.user.name, "userName");
			Assert.IsTrue(userTerminal.user.toopherAuthenticationEnabled);
		}

		[Test]
		public void UserTerminalRefreshFromServerTest()
		{
			var api = getToopherApi();
			WebClientMock.ReturnValue = @"{""id"":""1"", ""name"":""userTerminalNameChanged"", ""requester_specified_id"":""requesterSpecifiedId"", ""user"":{""id"":""1"", ""name"":""userNameChanged"", ""toopher_authentication_enabled"":false}}";
			UserTerminal userTerminal = new UserTerminal(USER_TERMINAL_DICT, api);
			userTerminal.RefreshFromServer();
			Assert.IsInstanceOf<UserTerminal>(userTerminal);
			Assert.AreEqual(WebClientMock.LastRequestMethod, "GET");
			Assert.AreEqual(userTerminal.id, "1");
			Assert.AreEqual(userTerminal.name, "userTerminalNameChanged");
			Assert.AreEqual(userTerminal.user.name, "userNameChanged");
			Assert.IsFalse(userTerminal.user.toopherAuthenticationEnabled);
		}
	}

	[TestFixture()]
	public class ActionTests : TestBase
	{
		[Test]
		public void CreateActionTest()
		{
			var response = (IDictionary<string, object>)SimpleJson.SimpleJson.DeserializeObject(@"{""id"":""1"", ""name"":""actionName""}");
			Toopher.Action action = new Toopher.Action(response);
			Assert.AreEqual(action.id, "1");
			Assert.AreEqual(action.name, "actionName");
		}
	}
}
