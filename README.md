#ToopherDotNet [![Build Status](https://travis-ci.org/toopher/toopher-dotnet.png?branch=master)](https://travis-ci.org/toopher/toopher-dotnet)

ToopherDotNet is a Toopher API library that simplifies the task of interfacing with the Toopher API from DotNet programs.  It does not depend on any external libraries, and preconfigures the required OAuth and JSON functionality so you can focus on just using the API.

### .NET Framework Version
\>=4.5

### C# Version
\>=5.0

### Documentation
Make sure you visit [https://dev.toopher.com](https://dev.toopher.com) to get acquainted with the Toopher API fundamentals.  The documentation there will tell you the details about the operations this API wrapper library provides.

## ToopherApi Workflow

### Step 1: Pair
Before you can enhance your website's actions with Toopher, your customers will need to pair their mobile device's Toopher app with your website.  To do this, they generate a unique pairing phrase from within the app on their mobile device.  You will need to prompt them for a pairing phrase as part of the Toopher enrollment process.  Once you have a pairing phrase, just send it to the Toopher API along with your requester credentials and we'll return a pairing ID that you can use whenever you want to authenticate an action for that user.

```csharp
using Toopher;

// Create an API object using your credentials
ToopherApi api = new ToopherApi("<your consumer key>", "<your consumer secret>");

// Step 1 - Pair with their mobile device's Toopher app
Pairing pairing = api.Pair("username@yourservice.com", "pairing phrase");
```

### Step 2: Authenticate
You have complete control over what actions you want to authenticate using Toopher (logging in, changing account information, making a purchase, etc.).  Just send us the username or pairing ID and we'll make sure they actually want it to happen. You can also choose to provide the following optional parameters: terminal name, requester specified ID and action name (*default: "Log in"*).


```csharp
// Step 2 - Authenticate a log in
AuthenticationRequest authenticationRequest = api.Authenticate("username@yourservice.com", "terminal name");

// Once they've responded you can then check the status
authenticationRequest.RefreshFromServer();
if (authenticationRequest.pending == false && authenticationRequest.granted == true) {
    // Success!
}
```

## ToopherIframe Workflow

### Step 1: Embed a request in an IFRAME
1. Generate an authentication URL by providing a username.
2. Display a webpage to your user that embeds this URL within an `<iframe>` element.

```csharp
using Toopher;

// Create an API object using your credentials
ToopherIframe iframeApi = new ToopherIframe("<your consumer key>", "<your consumer secret>");

string authenticationIframeUrl = iframeApi.GetAuthenticationUrl("username@yourservice.com");

// Add an <iframe> element to your HTML:
// <iframe id="toopher-iframe" src=authenticationIframeUrl />
```

### Step 2: Validate the postback data

The simplest way to validate the postback data is to call `IsAuthenticationGranted` to check if the authentication request was granted.

```csharp
// Retrieve the postback data as a string from POST parameter 'iframe_postback_data'

// Returns boolean indicating if authentication request was granted by user
bool authenticationRequestGranted = iframeApi.IsAuthenticationGranted(postbackData);

if (authenticationRequestGranted) {
    // Success!
}
```

### Handling Errors
If any request runs into an error a `RequestError` will be thrown with more details on what went wrong.

### Demo
Check out the `ToopherDotNetDemo` for an example program that walks you through the whole process!  Simply run the commands below:
```shell
$ xbuild
$ mono ToopherDotNetDemo/bin/Debug/ToopherDotNetDemo.exe
```

## Contributing
### Dependencies
This library was developed on Windows and OS X. To run .NET on OS X, we use [Mono](http://www.mono-project.com/). To install Mono with Homebrew run:
```shell
$ brew install mono
```

### Tests
To run the tests enter:

```shell
$ xbuild
$ nunit-console ./ToopherDotNetTests/bin/Debug/ToopherDotNetTests.dll -exclude Integration,NotWorkingOnMono
```


## License
ToopherDotNet is licensed under the MIT License. See LICENSE.txt for the full text.
