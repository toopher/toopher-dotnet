using System;
using System.IO;
using System.Net;
using System.Collections.Generic;
using System.Collections.Specialized;
using Toopher;
using NUnit.Framework;

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

		private ToopherAPI getApi ()
		{
			return new ToopherAPI ("key", "secret", null, typeof (WebClientMock));
		}

		[Test()]
		public void ToopherVersionTest ()
		{
			Assert.IsTrue(ToopherAPI.VERSION is string);
			string[] strs = ToopherAPI.VERSION.Split('.');
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
			Assert.IsTrue(ToopherAPI.DEFAULT_BASE_URL is string);
			Assert.IsTrue(System.Uri.IsWellFormedUriString(ToopherAPI.DEFAULT_BASE_URL, System.UriKind.Absolute));
		}

		[Test]
		public void CreatePairingTest ()
		{
			var api = getApi();
			WebClientMock.ReturnValue = @"{""id"":""1"", ""enabled"":true, ""user"":{""id"":""1"",""name"":""some user""}}";
			PairingStatus pairing = api.Pair ("awkward turtle", "some user");
			Assert.AreEqual (WebClientMock.LastRequestMethod, "POST");
			Assert.AreEqual (WebClientMock.LastRequestData["pairing_phrase"], "awkward turtle");
			Assert.AreEqual (pairing.id, "1");
		}


		[Test]
		public void AuthenticateTest ()
		{
			var api = getApi ();
			WebClientMock.ReturnValue = @"{""id"":""1"", ""pending"":false, ""granted"":true, ""automated"":false, ""reason"":""its a test"", ""terminal"":{""id"":""1"", ""name"":""test terminal""}}";
			AuthenticationStatus auth = api.Authenticate("1", "test terminal");
			Assert.AreEqual(WebClientMock.LastRequestMethod, "POST");
			Assert.AreEqual (WebClientMock.LastRequestData["pairing_id"], "1");
			Assert.AreEqual (WebClientMock.LastRequestData["terminal_name"], "test terminal");
		}

		[Test]
		public void AuthenticateByUserNameTest ()
		{
			var api = getApi ();
			WebClientMock.ReturnValue = @"{""id"":""1"", ""pending"":false, ""granted"":true, ""automated"":false, ""reason"":""its a test"", ""terminal"":{""id"":""1"", ""name"":""test terminal""}}";
			AuthenticationStatus auth = api.AuthenticateByUserName ("some other user", "random string", extras: new Dictionary<String, String>() {{ "random_key" , "42" }});
			Assert.AreEqual (WebClientMock.LastRequestMethod, "POST");
			Assert.AreEqual (WebClientMock.LastRequestData["user_name"], "some other user");
			Assert.AreEqual (WebClientMock.LastRequestData["terminal_name_extra"], "random string");
			Assert.AreEqual (WebClientMock.LastRequestData["random_key"], "42");
			Assert.AreEqual (WebClientMock.LastRequestData["terminal_name"], "");
		}

		

		

		[Test]
		public void ArbitraryParametersOnPairTest ()
		{
			var api = getApi ();
			WebClientMock.ReturnValue = @"{""id"":""1"", ""enabled"":true, ""user"":{""id"":""1"",""name"":""some user""}}";
			PairingStatus pairing = api.Pair ("awkward turtle", "some user", extras: new Dictionary<string,string>(){{"test_param", "42"}});
			Assert.AreEqual (WebClientMock.LastRequestMethod, "POST");
			Assert.AreEqual (WebClientMock.LastRequestData["test_param"], "42");
		}

		[Test]
		public void ArbitraryParamtersOnAuthenticateTest ()
		{
			var api = getApi ();
			WebClientMock.ReturnValue = @"{""id"":""1"", ""pending"":false, ""granted"":true, ""automated"":false, ""reason"":""its a test"", ""terminal"":{""id"":""1"", ""name"":""test terminal""}}";
			AuthenticationStatus auth = api.Authenticate ("1", "test terminal", extras: new Dictionary<string, string> () { { "test_param", "42" } });
			Assert.AreEqual (WebClientMock.LastRequestMethod, "POST");
			Assert.AreEqual (WebClientMock.LastRequestData["test_param"], "42");
		}

		[Test]
		public void AccessArbitraryKeysInPairingStatusTest ()
		{
			var api = getApi ();
			WebClientMock.ReturnValue = @"{""id"":""1"", ""enabled"":true, ""user"":{""id"":""1"",""name"":""some user""}, ""random_key"":""84""}";
			PairingStatus pairing = api.GetPairingStatus ("1");
			Assert.AreEqual (WebClientMock.LastRequestMethod, "GET");
			Assert.AreEqual (pairing.id, "1");
			Assert.AreEqual (pairing.userName, "some user");
			Assert.AreEqual (pairing.userId, "1");
			Assert.IsTrue (pairing.enabled);
			Assert.AreEqual (pairing["random_key"], "84");
		}

		[Test]
		public void AccessArbitraryKeysInAuthenticationStatusTest ()
		{
			var api = getApi ();
			WebClientMock.ReturnValue = @"{""id"":""1"", ""pending"":false, ""granted"":true, ""automated"":false, ""reason"":""its a test"", ""terminal"":{""id"":""1"", ""name"":""test terminal""}, ""random_key"":""84""}";
			AuthenticationStatus auth = api.GetAuthenticationStatus ("1");
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
		[ExpectedException(typeof(UserDisabledError))]
		public void DisabledUserRaisesCorrectErrorTest ()
		{
			var api = getApi ();
			WebClientMock.ReturnException = makeError((HttpStatusCode)409,
				@"{""error_code"":704, ""error_message"":""The specified user has disabled Toopher authentication.""}");
			api.AuthenticateByUserName ("some disabled user", "some random string");
		}

		[Test]
		[ExpectedException (typeof (UserUnknownError))]
		public void UnknownUserRaisesCorrectErrorTest ()
		{
			var api = getApi ();
			WebClientMock.ReturnException = makeError ((HttpStatusCode)409,
				@"{""error_code"":705, ""error_message"":""No matching user exists.""}");
			api.AuthenticateByUserName ("some unknown user", "some random string");
		}

		[Test]
		[ExpectedException (typeof (TerminalUnknownError))]
		public void UnknownTerminalRaisesCorrectErrorTest ()
		{
			var api = getApi ();
			WebClientMock.ReturnException = makeError ((HttpStatusCode)409,
				@"{""error_code"":706, ""error_message"":""No matching terminal exists.""}");
			api.AuthenticateByUserName ("some unknown user", "some random string");
		}

		[Test]
		[ExpectedException (typeof (PairingDeactivatedError))]
		public void DeactivatedPairingRaisesCorrectErrorTest ()
		{
			var api = getApi ();
			WebClientMock.ReturnException = makeError ((HttpStatusCode)409,
				@"{""error_code"":601, ""error_message"":""This pairing has been deactivated.""}");
			api.AuthenticateByUserName ("some disabled user", "some random string");
		}

		[Test]
		[ExpectedException (typeof (PairingDeactivatedError))]
		public void UnauthorizedPairingRaisesCorrectErrorTest ()
		{
			var api = getApi ();
			WebClientMock.ReturnException = makeError ((HttpStatusCode)409,
				@"{""error_code"":601, ""error_message"":""This pairing has not been authorized to authenticate.""}");
			api.AuthenticateByUserName ("some unauthorized user", "some random string");
		}

		[Test]
		public void GetAuthenticationStatusTest ()
		{
			var api = getApi ();
			WebClientMock.ReturnValue = @"{""id"":""1"", ""pending"":false, ""granted"":true, ""automated"":false, ""reason"":""its a test"", ""terminal"":{""id"":""1"", ""name"":""test terminal""}}";
			AuthenticationStatus auth = api.GetAuthenticationStatus ("1");
			Assert.AreEqual (WebClientMock.LastRequestMethod, "GET");
			Assert.AreEqual (auth.id, "1");
			Assert.IsFalse (auth.pending);
			Assert.IsTrue (auth.granted);
			Assert.AreEqual (auth.reason, "its a test");
			Assert.AreEqual (auth.terminalId, "1");
			Assert.AreEqual (auth.terminalName, "test terminal");
		}

		[Test]
		public void GetAuthenticationStatusWIthOtpTest ()
		{
			var api = getApi ();
			WebClientMock.ReturnValue = @"{""id"":""1"", ""pending"":false, ""granted"":true, ""automated"":false, ""reason"":""its a test"", ""terminal"":{""id"":""1"", ""name"":""test terminal""}}";
			AuthenticationStatus auth = api.GetAuthenticationStatus ("1", otp: "123456");
			Assert.AreEqual (WebClientMock.LastRequestMethod, "POST");
			Assert.AreEqual (WebClientMock.LastRequestData["otp"], "123456");
		}


		[Test]
		public void PairingStatusTest ()
		{
			var api = getApi ();
			WebClientMock.ReturnValue = @"{""id"":""1"", ""enabled"":true, ""user"":{""id"":""1"",""name"":""some user""}}";
			PairingStatus pairing = api.GetPairingStatus ("1");
			Assert.AreEqual (WebClientMock.LastRequestMethod, "GET");
			Assert.AreEqual (pairing.id, "1");
			Assert.AreEqual (pairing.userName, "some user");
			Assert.AreEqual (pairing.userId, "1");
			Assert.IsTrue (pairing.enabled);
		}

		[Test]
		public void GeneratePairingLinkTest ()
		{
			var api = getApi ();
			WebClientMock.ReturnValue = @"{""reset_authorization"":""abcde"", ""url"":""http://testonly/pairings/1/reset?reset_authorization=abcde""}}";
			string pairingResetLink = api.GetPairingResetLink ("1");
			Assert.AreEqual (pairingResetLink, "http://testonly/pairings/1/reset?reset_authorization=abcde");
		}
	}
}

