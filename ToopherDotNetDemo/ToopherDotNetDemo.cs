using System;
using Toopher;

namespace Toopher
{
	class ToopherDotNetDemo
	{
		public const string DEFAULT_USERNAME = "demo@toopher.com";
		public const string DEFAULT_TERMINAL_NAME = "my computer";

		public static ToopherAPI api;

		public static void Main(string[] args)
		{
			Console.WriteLine ("======================================");
			Console.WriteLine ("Library Usage Demo");
			Console.WriteLine ("======================================");
			Console.WriteLine ("");
			Console.WriteLine ("Setup Credentials");
			Console.WriteLine ("--------------------------------------");
			string consumerKey = System.Environment.GetEnvironmentVariable ("TOOPHER_CONSUMER_KEY");
			string consumerSecret = System.Environment.GetEnvironmentVariable ("TOOPHER_CONSUMER_SECRET");
			if ((consumerKey == null) || (consumerSecret == null)) {
				Console.WriteLine ("Enter your requester credentials (from https://dev.toopher.com).");
				Console.WriteLine ("Hint: set the TOOPHER_CONSUMER_SECRET and TOOPHER_CONSUMER_SECRET environment variables to avoid this prompt.");
				Console.Write ("Consumer key: ");
				consumerKey = Console.ReadLine ();
				Console.Write ("Consumer secret: ");
				consumerSecret = Console.ReadLine ();
			}
			string baseUrl = System.Environment.GetEnvironmentVariable ("TOOPHER_BASE_URL");

			api = new Toopher.ToopherAPI (consumerKey, consumerSecret, baseUrl);

			string pairingId;
			while (true) {
				string pairingPhrase;
				while (true) {
					Console.WriteLine ("Step 1: Pair requester with phone");
					Console.WriteLine ("--------------------------------------");
					Console.WriteLine ("Pairing phrases are generated on the mobile app");
					Console.Write ("Enter pairing phrase: ");
					pairingPhrase = Console.ReadLine ();

					if (pairingPhrase.Length == 0) {
						Console.WriteLine ("Please enter a pairing phrase to continue");
					}
					else {
						break;
					}
				}

				Console.Write (String.Format ("Enter a username for this pairing [{0}]: ", DEFAULT_USERNAME));
				string userName = Console.ReadLine ();
				if (userName.Length == 0) {
					userName = DEFAULT_USERNAME;
				}

				Console.WriteLine ("Sending pairing request...");

				try {
					var pairingStatus = api.Pair (pairingPhrase, userName);
					pairingId = pairingStatus.id;
					break;
				}
				catch (RequestError err) {
					System.Console.WriteLine (String.Format ("The pairing phrase was not accepted (reason:{0})", err.Message));
				}
			}

			while (true) {
				Console.WriteLine ("Authorize pairing on phone and then press return to continue.");
				Console.ReadLine ();
				Console.WriteLine ("Checking status of pairing request...");

				try {
					var pairingStatus = api.GetPairingStatus (pairingId);
					if (pairingStatus.enabled) {
						Console.WriteLine ("Pairing complete");
						break;
					}
					else {
						Console.WriteLine ("The pairing has not been authorized by the phone yet.");
					}
				}
				catch (RequestError err) {
					Console.WriteLine (String.Format ("Could not check pairing status (reason:{0})", err.Message));
				}
			}

			while (true) {
				Console.WriteLine ("Step 2: Authenticate log in");
				Console.WriteLine ("--------------------------------------");
				Console.Write (String.Format ("Enter a terminal name for this authentication request [\"{0}\"]: ", DEFAULT_TERMINAL_NAME));
				string terminalName = Console.ReadLine ();
				if (terminalName.Length == 0) {
					terminalName = DEFAULT_TERMINAL_NAME;
				}

				Console.WriteLine ("Sending authentication request...");

				string requestId;
				try {
					var requestStatus = api.Authenticate (pairingId, terminalName);
					requestId = requestStatus.id;
				}
				catch (RequestError err) {
					Console.WriteLine (String.Format ("Error initiating authentication (reason:{0})", err.Message));
					continue;
				}

				while (true) {
					Console.WriteLine ("Respond to authentication request on phone and then press return to continue.");
					Console.ReadLine ();
					Console.WriteLine ("Checking status of authentication request...");

					AuthenticationStatus requestStatus;
					try {
						requestStatus = api.GetAuthenticationStatus (requestId);
					}
					catch (RequestError err) {
						Console.WriteLine (String.Format ("Could not check authentication status (reason:{0})", err.Message));
						continue;
					}

					if (requestStatus.pending) {
						Console.WriteLine ("The authentication request has not received a response from the phone yet.");
					}
					else {
						string automation = requestStatus.automated ? "automatically " : "";
						string result = requestStatus.granted ? "granted" : "denied";
						Console.WriteLine ("The request was " + automation + result + "!");
						Console.WriteLine ("This request " + ((bool)requestStatus["totp_valid"] ? "had" : "DID NOT HAVE") + " a valid authenticator OTP.");
						break;
					}
				}

				Console.WriteLine ("Press return to authenticate again, or Ctrl-C to exit");
				Console.ReadLine ();
			}
		}
	}
}
