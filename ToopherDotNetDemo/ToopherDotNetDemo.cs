using System;
using Toopher;

namespace Toopher
{
	class ToopherDotNetDemo
	{
		public const string DEFAULT_USERNAME = "demo@toopher.com";
		public const string DEFAULT_TERMINAL_NAME = "my computer";

		public static ToopherApi api;

		public static void Main (string[] args)
		{
			Console.WriteLine ();
			Console.WriteLine ("Toopher Library Demo");
			Console.WriteLine ("======================================");
			Console.WriteLine ("");
			string consumerKey = System.Environment.GetEnvironmentVariable ("TOOPHER_CONSUMER_KEY");
			string consumerSecret = System.Environment.GetEnvironmentVariable ("TOOPHER_CONSUMER_SECRET");
			if ((consumerKey == null) || (consumerSecret == null)) {
				Console.WriteLine ("Setup Credentials");
				Console.WriteLine ("--------------------------------------");
				Console.WriteLine ("Enter your requester credentials (from https://dev.toopher.com).");
				Console.WriteLine ("Hint: Set the TOOPHER_CONSUMER_SECRET and TOOPHER_CONSUMER_SECRET environment variables to avoid this prompt.");
				Console.Write ("Consumer Key: ");
				consumerKey = Console.ReadLine ();
				Console.Write ("Consumer Secret: ");
				consumerSecret = Console.ReadLine ();
			}
			string baseUrl = System.Environment.GetEnvironmentVariable ("TOOPHER_BASE_URL");

			api = new Toopher.ToopherApi (consumerKey, consumerSecret, baseUrl);

			Pairing pairing;
			string pairingId;
			while (true) {
				string pairingPhrase;
				while (true) {
					Console.WriteLine ("Step 1: Pair requester with phone");
					Console.WriteLine ("--------------------------------------");
					Console.WriteLine ("Pairing phrases are generated on the mobile app");
					Console.Write ("Enter pairing phrase: ");
					pairingPhrase = Console.ReadLine ();

					if (string.IsNullOrEmpty(pairingPhrase.Trim())) {
						Console.WriteLine ("Please enter a pairing phrase to continue");
					} else {
						break;
					}
				}

				Console.Write (String.Format ("Enter a username for this pairing [{0}]: ", DEFAULT_USERNAME));
				string userName = Console.ReadLine ();
				if (string.IsNullOrEmpty(userName.Trim())) {
					userName = DEFAULT_USERNAME;
				}

				Console.WriteLine ("Sending pairing request...");

				try {
					pairing = api.Pair (userName, pairingPhrase);
					pairingId = pairing.id;
					break;
				} catch (RequestError err) {
					System.Console.WriteLine (String.Format ("The pairing phrase was not accepted (Reason: {0})", err.Message));
				}
			}

			while (true) {
				Console.WriteLine ("Authorize pairing on phone and then press return to continue.");
				Console.ReadLine ();
				Console.WriteLine ("Checking status of pairing request...");

				try {
					pairing.RefreshFromServer();
					if (pairing.pending) {
						Console.WriteLine ("The pairing has not been authorized by the phone yet.");
					} else if (pairing.enabled) {
						Console.WriteLine ("Pairing complete");
						break;
					} else {
						Console.WriteLine ("The pairing has been denied.");
						Environment.Exit(0);
					}
				} catch (RequestError err) {
					Console.WriteLine (String.Format ("Could not check pairing status (Reason:{0})", err.Message));
				}
			}

			while (true) {
				Console.WriteLine ();
				Console.WriteLine ("Step 2: Authenticate log in");
				Console.WriteLine ("--------------------------------------");
				Console.Write (String.Format ("Enter a terminal name for this authentication request [\"{0}\"]: ", DEFAULT_TERMINAL_NAME));
				string terminalName = Console.ReadLine ();
				if (string.IsNullOrEmpty(terminalName.Trim())) {
					terminalName = DEFAULT_TERMINAL_NAME;
				}

				Console.WriteLine ("Sending authentication request...");

				AuthenticationRequest authRequest;
				try {
					authRequest = api.Authenticate (pairing.user.name, terminalName);
				} catch (RequestError err) {
					Console.WriteLine (String.Format ("Error initiating authentication (Reason:{0})", err.Message));
					continue;
				}

				while (true) {
					Console.WriteLine ("Respond to authentication request on phone and then press return to continue.");
					Console.ReadLine ();
					Console.WriteLine ("Checking status of authentication request...");

					try {
						authRequest.RefreshFromServer();
					} catch (RequestError err) {
						Console.WriteLine (String.Format ("Could not check authentication status (Reason:{0})", err.Message));
						continue;
					}

					if (authRequest.pending) {
						Console.WriteLine ("The authentication request has not received a response from the phone yet.");
					} else {
						string automation = authRequest.automated ? "automatically " : "";
						string result = authRequest.granted ? "granted" : "denied";
						Console.WriteLine ("The request was " + automation + result + "!");
						break;
					}
				}

				Console.Write ("Press return to authenticate again, or Ctrl-C to exit.");
				Console.ReadLine ();
			}
		}
	}
}
