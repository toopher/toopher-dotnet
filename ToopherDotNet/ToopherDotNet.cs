using System;
using OAuth;
using System.Net;
using System.Collections.Specialized;
using System.Text;
using System.Runtime.Serialization;
using SimpleJson;
using System.Collections.Generic;
using System.IO;

namespace Toopher
{
	public class ToopherAPI
	{
		public const string DEFAULT_BASE_URL = "https://api.toopher.com/v1/";

		string consumerKey;
		string consumerSecret;
		string baseUrl;

		// Create the ToopherAPI object tied to your requester credentials
		// 
		// Credentials are available on https://dev.toopher.com
		public ToopherAPI(string consumerKey, string consumerSecret, string baseUrl = null)
		{
			this.consumerKey = consumerKey;
			this.consumerSecret = consumerSecret;
			if (baseUrl != null) {
				this.baseUrl = baseUrl;
			}
			else {
				this.baseUrl = ToopherAPI.DEFAULT_BASE_URL;
			}
		}

		// Pair your requester with a user's Toopher application
		//
		// Must provide a pairing request (generated on their phone) and
		// the user's name
		public PairingStatus Pair(string pairingPhrase, string userName, Dictionary<string, string> extras = null)
		{
			string endpoint = "pairings/create";

			NameValueCollection parameters = new NameValueCollection ();
			parameters.Add ("pairing_phrase", pairingPhrase);
			parameters.Add ("user_name", userName);

			if (extras != null) {
				foreach (KeyValuePair<string, string> kvp in extras) {
					parameters.Add (kvp.Key, kvp.Value);
				}
			}


			var json = post (endpoint, parameters);
			return PairingStatus.fromJson (json);
		}

		// Check on status of a pairing request
		// 
		// Must provide the ID returned when the pairing request was initiated
		public PairingStatus GetPairingStatus(string pairingRequestId)
		{
			string endpoint = String.Format ("pairings/{0}", pairingRequestId);

			var json = get (endpoint);
			return PairingStatus.fromJson (json);
		}

		// Authenticate an action with Toopher
		//
		// Provide pairing ID, a name for the terminal (displayed to user) and
		// an option action name (displayed to user) [defaults to "log in"]
		public AuthenticationStatus Authenticate(string pairingId, string terminalName, string actionName = null, Dictionary<string, string> extras = null)
		{
			string endpoint = "authentication_requests/initiate";

			NameValueCollection parameters = new NameValueCollection ();
			parameters.Add ("pairing_id", pairingId);
			parameters.Add ("terminal_name", terminalName);
			if (actionName != null) {
				parameters.Add ("action_name", actionName);
			}
			if (extras != null) {
				foreach (KeyValuePair<string, string> kvp in extras) {
					parameters.Add (kvp.Key, kvp.Value);
				}
			}

			var json = post (endpoint, parameters);
			return AuthenticationStatus.fromJson (json);
		}

		// Check on status of authentication request
		//
		// Provide authentication request ID returned when authentication request was
		// started.
		public AuthenticationStatus GetAuthenticationStatus(string authenticationRequestId)
		{
			string endpoint = String.Format ("authentication_requests/{0}", authenticationRequestId);

			var json = get (endpoint);
			return AuthenticationStatus.fromJson (json);
		}

		private JsonObject request(string method, string endpoint, NameValueCollection parameters = null)
		{
			// Normalize method string
			method = method.ToUpper ();

			// Build an empty collection for parameters (if necessary)
			if (parameters == null) {
				parameters = new NameValueCollection ();
			}

			var client = OAuthRequest.ForRequestToken (this.consumerKey, this.consumerSecret);
			client.RequestUrl = this.baseUrl + endpoint;
			client.Method = method;

			string auth = client.GetAuthorizationHeader (parameters);
			// FIXME: OAuth library puts extraneous comma at end, workaround: remove it if present
			auth = auth.TrimEnd (new char[] { ',' });

			WebClient wClient = new WebClient ();
			wClient.Headers.Add ("Authorization", auth);
			if (parameters.Count > 0) {
				wClient.QueryString = parameters;
			}

			string response;
			try {
				if (method.Equals ("POST")) {
					var responseArray = wClient.UploadValues (client.RequestUrl, client.Method, parameters);
					response = Encoding.UTF8.GetString (responseArray);
				}
				else {
					response = wClient.DownloadString (client.RequestUrl);
				}
			}
			catch (WebException wex) {
				string error_message;
				using (Stream stream = wex.Response.GetResponseStream ()) {
					StreamReader reader = new StreamReader (stream, Encoding.UTF8);
					error_message = reader.ReadToEnd ();
				}

				try {
					// Attempt to parse JSON response
					var json = (JsonObject)SimpleJson.SimpleJson.DeserializeObject (error_message);
					error_message = (string)json["error_message"];
				}
				catch (Exception) { /* Ignore */ }

				throw new RequestError (error_message, wex);
			}

			try {
				return (JsonObject)SimpleJson.SimpleJson.DeserializeObject (response);
			}
			catch (Exception ex) {
				throw new RequestError ("Could not parse response", ex);
			}
		}

		private JsonObject get(string endpoint, NameValueCollection parameters = null)
		{
			return request ("GET", endpoint, parameters);
		}

		private JsonObject post(string endpoint, NameValueCollection parameters = null)
		{
			return request ("POST", endpoint, parameters);
		}
	}

	// Status information for a pairing request
	public class PairingStatus : Dictionary<String, Object>
	{
		public string id
		{
			get { return (string)this["id"]; }
		}
		public string userId
		{
			get { return (string)((JsonObject)this["user"])["id"]; }
		}
		public string userName
		{
			get { return (string)((JsonObject)this["user"])["name"]; }
		}
		public bool enabled
		{
			get { return (bool)this["enabled"]; }
		}

		public override string ToString()
		{
			return string.Format ("[PairingStatus: id={0}; userId={1}; userName={2}, enabled={3}]", id, userId, userName, enabled);
		}

		public static PairingStatus fromJson(JsonObject json)
		{
			try {
				// validate that the json has the minimum keys we need
				var id = (string)json["id"];
				var enabled = (bool)json["enabled"];
				var user = (JsonObject)json["user"];
				var userId = (string)user["id"];
				var userName = (string)user["name"];

				// construct and return the result
				PairingStatus result = new PairingStatus ();
				foreach (KeyValuePair<String, Object> kvp in json) {
					result.Add (kvp.Key, kvp.Value);
				}
				return result;
			}
			catch (Exception ex) {
				throw new RequestError ("Could not parse pairing status from response", ex);
			}
		}
	}

	// Status information for an authentication request
	public class AuthenticationStatus : Dictionary<String, Object>
	{
		// convenience property accessors
		public string id
		{
			get { return (string)this["id"]; }
		}
		public bool pending
		{
			get { return (bool)this["pending"]; }
		}
		public bool granted
		{
			get { return (bool)this["granted"]; }
		}
		public bool automated
		{
			get { return (bool)this["automated"]; }
		}
		public string reason
		{
			get { return (string)this["reason"]; }
		}
		public string terminalId
		{
			get { return (string)((JsonObject)this["terminal"])["id"]; }
		}
		public string terminalName
		{
			get { return (string)((JsonObject)this["terminal"])["name"]; }
		}

		public override string ToString()
		{
			return string.Format ("[AuthenticationStatus: id={0}; pending={1}; granted={2}; automated={3}; reason={4}; terminalId={5}; terminalName={6}]", id, pending, granted, automated, reason, terminalId, terminalName);
		}

		public static AuthenticationStatus fromJson(JsonObject json)
		{
			try {
				// validate that the json has the minimum keys we need
				var id = (string)json["id"];
				var pending = (bool)json["pending"];
				var granted = (bool)json["granted"];
				var automated = (bool)json["automated"];
				var reason = (string)json["reason"];

				var terminal = (JsonObject)json["terminal"];
				var terminalId = (string)terminal["id"];
				var terminalName = (string)terminal["name"];

				// construct and return the result
				AuthenticationStatus result = new AuthenticationStatus ();
				foreach (KeyValuePair<String, Object> kvp in json) {
					result.Add (kvp.Key, kvp.Value);
				}
				return result;

			}
			catch (Exception ex) {
				throw new RequestError ("Could not parse authentication status from response", ex);
			}
		}
	}

	// An exception class used to indicate an error in a request
	public class RequestError : System.ApplicationException
	{
		public RequestError(string message) : base (message) { }
		public RequestError(string message, System.Exception inner) : base (message, inner) { }
	}

}

